using ProjectZeroLib.Signal;
using ProjectZeroLib.Utils;
using Server.Service.Abstract;
using Server.Service.UserClient;
using Strategies.Strategies;
using System.Net.Sockets;
using System.Text;

namespace Server.Service
{
    /// <summary>
    /// Реализация обмена данными с клиентами через tcp соединение.
    /// </summary>
    public partial class TcpConnector : Connector
    {
        private readonly TcpListener _authListener;
        private readonly TcpListener _dataListener;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly int connectionNumber = 200;

        private readonly Dictionary<string, NetworkStream> _sessions = [];

        public TcpConnector(ClientsModel clientDataBase, StrategiesRepository strategies) : base(clientDataBase, strategies)
        {
            _connectionSemaphore = new SemaphoreSlim(connectionNumber);
            _authListener = new TcpListener(address, authPort);
            _dataListener = new TcpListener(address, dataPort);
        }

        /// <summary>
        /// Запускает прослушивание подключений на аутентификацию.
        /// </summary>
        /// <returns></returns>
        public async Task StartAuthListenerAsync()
        {
            Logger.AddLog(Logs, "start tcp-listener");
            while (IsActive)
            {
                await _connectionSemaphore.WaitAsync();
                var authClient = await _authListener.AcceptTcpClientAsync();
                Logger.AddLog(Logs, "new authorization request");
                _ = HandleAuthClientAsync(authClient);
            }
        }

        /// <summary>
        /// Запускает прослушивание подключений на обмен данными.
        /// </summary>
        /// <returns></returns>
        public async Task StartDataListenerAsync()
        {
            while (IsActive)
            {
                var dataClient = await _dataListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleDataClientAsync(dataClient));
            }
        }

        /// <summary>
        /// Авторизирует входящее подключение.
        /// </summary>
        /// <param name="authClient"></param>
        /// <returns></returns>
        private async Task HandleAuthClientAsync(TcpClient authClient)
        {
            try
            {
                using var networkStream = authClient.GetStream();
                var receivedMessage = await TcpService.ReadMessageAsync(networkStream);
                string[] data = receivedMessage.Split('_');
                Logger.AddLog(Logs, $"authorization from {data[0]}");

                var client = _clients.Clients.Items.FirstOrDefault(x => (x.Data.Login, x.Data.Password) == (data[0], data[1]));
                if (client != null)
                {
                    client.SessionId = data[2];
                    var responseMessage = Converter.CreateMessage("auth_success");
                    var responseBytes = Encoding.UTF8.GetBytes(responseMessage);
                    await networkStream.WriteAsync(responseBytes);
                    Logger.AddLog(Logs, $"auth {data[0]}");
                }
                else
                {
                    var responseMessage = Converter.CreateMessage("auth_failure");
                    var responseBytes = Encoding.UTF8.GetBytes(responseMessage);
                    await networkStream.WriteAsync(responseBytes);
                    Logger.AddLog(Logs, $"auth error {data[0]}");
                }
            }
            finally
            {
                authClient.Close();
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Обменивается данными с клиентом.
        /// </summary>
        /// <param name="dataClient"></param>
        /// <returns></returns>
        private async Task HandleDataClientAsync(TcpClient dataClient)
        {
            Client? client = null;
            try
            {
                var networkStream = dataClient.GetStream();
                while (true)
                {
                    var receivedMessage = await TcpService.ReadMessageAsync(networkStream);
                    var data = receivedMessage.Split('_');
                    if (client == null)
                    {
                        client = _clients.Clients.Items.FirstOrDefault(x => x.SessionId.Equals(data[1]));
                        if (client != null)
                        {
                            _sessions.Add(client.SessionId, networkStream);
                            ActiveConnections++;
                            client.IsActive = true;
                            Logger.AddLog(Logs, $"start data exchange with {client.Data.Login}");
                            await SendStrategies(client);
                        }
                    }
                    else
                        MessageHandler(client, data);
                }
            }
            finally
            {
                if (client != null)
                {
                    client.IsActive = false;
                }
                dataClient.Close();
                _connectionSemaphore.Release();
                ActiveConnections--;
            }
        }

        private NetworkStream? GetStreamFromSessions(Client client)
        {
            _sessions.TryGetValue(client.SessionId, out var stream);
            if (stream == null)
            {
                Logger.AddLog(Logs, $"connection lost with {client.Data.Login}");
            }
            return stream;
        }

        public override void ConnectorStart()
        {
            _authListener.Start();
            _dataListener.Start();
            IsActive = true;
            Task.Run(StartAuthListenerAsync);
            Task.Run(StartDataListenerAsync);
        }

        public override void ConnectorStop()
        {
            IsActive = false;
            _authListener.Stop();
            _dataListener.Stop();
            Logger.AddLog(Logs, $"stop tcp-listener");
        }

        protected override async Task SendStrategy(StrategySummary data, Client client)
        {
            if (client.IsActive)
            {
                var strategy = _strategies.StrategiesList.Items.FirstOrDefault(x => x.Code.Equals(data.Code));
                if (strategy != null)
                {
                    var jsonStrat = string.Empty;
                    lock (strategy)
                    {
                        strategy.ClientLimit = data.TradeLimit;
                        jsonStrat = Converter.CreateJson(strategy);
                        strategy.ClientLimit = 0;
                    }
                    var json = Encoding.UTF8.GetBytes("strategy_" + jsonStrat);
                    var stream = GetStreamFromSessions(client);
                    if (stream != null)
                    {
                        await stream.WriteAsync(json);
                        Logger.AddLog(Logs, $"send strategy {strategy.Code} to {client.Data.Login}");
                    }
                }
            }
        }

        protected override async Task SendStrategies(Client client)
        {
            var tasks = new List<Task>();
            foreach (var data in client.Data.Strategies.Items)
                tasks.Add(SendStrategy(data, client));

            await Task.WhenAll(tasks);
        }

        protected override void SendSignal(SignalData signal)
        {
            if (IsActive && signal.Signal != "ping")
            {
                var message = Converter.CreateJson(signal);
                byte[] data = Encoding.UTF8.GetBytes("signal_" + message);
                lock (_clients.Clients)
                {
                    foreach (var client in _clients.Clients.Items)
                    {
                        if (client.IsActive && CheckStrategy(client, signal.Code))
                        {
                            var stream = GetStreamFromSessions(client);
                            stream?.WriteAsync(data, 0, data.Length);
                        }
                    }
                }
            }
        }

        protected override void MessageHandler(Client client, string[] data)
        {
            if (data[0].Equals("status"))
            {
                var strat = client.Data.Strategies.Items.FirstOrDefault(x => x.Code.Equals(data[1]));
                if (strat != null)
                {
                    if (data[2].Equals("True"))
                    {
                        strat.ActivatedByClient = true;
                        Logger.AddLog(Logs, $"{client.Data.Login} turns on {strat.Code}");
                    }
                    else
                    {
                        strat.ActivatedByClient = false;
                        Logger.AddLog(Logs, $"{client.Data.Login} turns off {strat.Code}");
                    }
                }
            }
            else if (data[0].Equals("disconnect"))
            {
                client.IsActive = false;
                _sessions.Remove(client.SessionId);
                client.SessionId = string.Empty;
            }
        }
    }
}
