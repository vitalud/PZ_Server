using ProjectZeroLib;
using Server.Service;
using Server.Service.Abstract;
using System.Net.Sockets;
using System.Text;

namespace Server.Models
{
    public class TcpConnector : Connector
    {
        private readonly TcpListener _authListener;
        private readonly TcpListener _dataListener;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly int connectionNumber = 200;

        public TcpConnector(ClientsModel clientDataBase, Strategies strategies) : base(clientDataBase, strategies)
        {
            _connectionSemaphore = new SemaphoreSlim(connectionNumber);
            _authListener = new TcpListener(address, authPort);
            _dataListener = new TcpListener(address, dataPort);
        }
        public override void ConnectorStart()
        {
            _authListener.Start();
            _dataListener.Start();
            IsActive = true;
            StartAsync();
            StartDataListenerAsync();

        }
        public override void ConnectorStop()
        {
            IsActive = false;
            _authListener.Stop();
            _dataListener.Stop();
            Logger.AddLog(Logs, $"stop tcp-listener");
        }
        public async Task StartAsync()
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
        public async Task StartDataListenerAsync()
        {
            while (IsActive)
            {
                var dataClient = await _dataListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleDataClientAsync(dataClient));
            }
        }

        private async Task HandleAuthClientAsync(TcpClient authClient)
        {
            try
            {
                using var networkStream = authClient.GetStream();
                var receivedMessage = await TcpService.ReadMessageAsync(networkStream);
                string[] data = receivedMessage.Split('_');
                Logger.AddLog(Logs, $"authorization from {data[0]}");
                var client = _clientDataBase.Clients.Items.FirstOrDefault(x => (x.Data.Login, x.Data.Password) == (data[0], data[1]));
                if (client != null)
                {
                    client.SessionId = data[2];
                    var responseMessage = Converter.CreateMessage("auth_success");
                    var responseBytes = Encoding.UTF8.GetBytes(responseMessage);
                    await networkStream.WriteAsync(responseBytes);
                    Logger.AddLog(Logs, $"Авторизирован {data[0]}");
                }
                else
                {
                    var responseMessage = Converter.CreateMessage("auth_failure");
                    var responseBytes = Encoding.UTF8.GetBytes(responseMessage);
                    await networkStream.WriteAsync(responseBytes);
                    Logger.AddLog(Logs, $"Ошибка авторизации {data[0]}");
                }
            }
            finally
            {
                authClient.Close();
                _connectionSemaphore.Release();
            }
        }
        private async Task HandleDataClientAsync(TcpClient dataClient)
        {
            Client client = null;
            try
            {
                var networkStream = dataClient.GetStream();
                while (true)
                {
                    var receivedMessage = await TcpService.ReadMessageAsync(networkStream);
                    string[] data = receivedMessage.Split('_');
                    if (client == null)
                    {
                        client = _clientDataBase.Clients.Items.FirstOrDefault(x => x.SessionId.Equals(data[1]));
                        if (client != null)
                        {
                            ActiveConnections++;
                            Logger.AddLog(Logs, $"Запрос стратегий от {client.Data.Login}");
                            client.Stream = networkStream;
                            client.IsActive = true;
                            await SendStrategies(client);
                        }
                    }
                    else
                        MessageHandler(client, data);
                }
            }
            finally
            {
                dataClient.Close();
                _connectionSemaphore.Release();
            }
        }
        private async Task SendStrategies(Client client)
        {
            foreach (var clientStrategy in client.Data.Strategies.Items)
            {
                var strategy = _strategies.StrategiesList.Items.FirstOrDefault(x => x.Code.Equals(clientStrategy.Code));
                if (strategy != null)
                {
                    string jsonStrat = string.Empty;
                    lock (strategy)
                    {
                        strategy.TempLimit = clientStrategy.TradeLimit;
                        jsonStrat = Converter.CreateJson(strategy);
                        strategy.TempLimit = 0;
                    }
                    byte[] json = Encoding.UTF8.GetBytes("strategy_" + jsonStrat);
                    await client.Stream.WriteAsync(json);
                    Logger.AddLog(Logs, $"Отправил стратегию {strategy.Code} пользователю {client.Data.Login}");
                }
            }
        }

        protected override void SendSignal(Signal signal)
        {
            if (IsActive)
            {
                var message = Converter.CreateJson(signal);
                byte[] data = Encoding.UTF8.GetBytes("signal_" + message);
                lock (_clientDataBase.Clients)
                {
                    foreach (var client in _clientDataBase.Clients.Items)
                    {
                        if (client.IsActive)
                        {
                            if (CheckStrategy(client, signal.Code))
                            {
                                if (client.Stream.CanWrite)
                                {
                                    client.Stream?.Write(data, 0, data.Length);
                                }
                            }
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
                        Logger.AddLog(Logs, $"Пользователь {client.Data.Login} включил стратегию {strat.Code}");
                    }
                    else
                    {
                        strat.ActivatedByClient = false;
                        Logger.AddLog(Logs, $"Пользователь {client.Data.Login} выключил стратегию {strat.Code}");
                    }
                }
            }
            else if(data[0].Equals("disconnect"))
            {
                client.IsActive = false;
                client.Stream.Close();
                client.Stream.Dispose();
            }
        }
    }
}
