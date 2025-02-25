using DynamicData;
using ProjectZeroLib.Signal;
using ReactiveUI;
using Server.Service.UserClient;
using Strategies.Strategies;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;

namespace Server.Service.Abstract
{
    /// <summary>
    /// Абстрактный класс, описывающий методы реализации обмена данными с клиентом.
    /// </summary>
    public abstract class Connector : ReactiveObject
    {
        protected readonly ClientsModel _clients;
        protected readonly StrategiesRepository _strategies;

        protected readonly IPAddress address = Dns.GetHostAddresses(Dns.GetHostName()).First(x => x.AddressFamily.Equals(AddressFamily.InterNetwork));
        protected readonly int dataPort = 49107;
        protected readonly int authPort = 29019;

        private bool _isActive;
        private int _activeConnections;

        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }
        public int ActiveConnections
        {
            get => _activeConnections;
            set => this.RaiseAndSetIfChanged(ref _activeConnections, value);
        }

        protected readonly SourceList<string> _logs = new();
        public SourceList<string> Logs => _logs;

        public Connector(ClientsModel clientDataBase, StrategiesRepository strategies)
        {
            _clients = clientDataBase;
            _strategies = strategies;

            foreach (var strat in _strategies.StrategiesList.Items) 
            {
                strat.WhenAnyValue(x => x.Signal)
                    .Skip(1)
                    .Subscribe(SendSignal);
            }

            _clients.Clients.Connect()
                .Subscribe(OnClientsCountChanged);
        }

        private void OnClientsCountChanged(IChangeSet<Client> changes)
        {
            foreach (var change in changes)
            {
                if (change.Reason.Equals(ListChangeReason.Add))
                {
                    var item = change.Item.Current;
                    CreateClientSubscriptions(item);
                }
                if (change.Reason.Equals(ListChangeReason.AddRange))
                {
                    foreach (var item in change.Range)
                    {
                        CreateClientSubscriptions(item);
                    }
                }
            }
        }
       
        private void CreateClientSubscriptions(Client client)
        {
            client.Data.Strategies.Connect()
                .Subscribe(changes => OnStrategiesCountChanged(changes, client));
        }
        
        private void OnStrategiesCountChanged(IChangeSet<StrategySummary> changes, Client client)
        {
            foreach (var change in changes)
            {
                if (change.Reason.Equals(ListChangeReason.Add))
                {
                    var item = change.Item.Current;
                    SendStrategy(item, client);
                }
            }
        }

        protected static bool CheckStrategy(Client client, string code)
        {
            bool check = false;
            foreach (var strategy in client.Data.Strategies.Items)
            {
                if (strategy.Code.Equals(code))
                {
                    if (strategy.ActivatedByClient)
                        check = true;
                    break;
                }
            }
            return check;
        }

        /// <summary>
        /// Запускает обмен данными.
        /// </summary>
        public abstract void ConnectorStart();

        /// <summary>
        /// Приостанавливает обмен данными.
        /// </summary>
        public abstract void ConnectorStop();

        /// <summary>
        /// Отправляет приобретенную стратегию клиенту.
        /// </summary>
        /// <param name="data">Краткие данные стратегии для поиска полной информации в хранилище.</param>
        /// <param name="client">Получатель стратегии.</param>
        protected abstract Task SendStrategy(StrategySummary data, Client client);

        /// <summary>
        /// Отправляет стратегии клиенту при подключении к серверу.
        /// </summary>
        /// <param name="client">Получатель стратегии.</param>
        protected abstract Task SendStrategies(Client client);

        /// <summary>
        /// Отправляет сигнал по стратегии клиенту.
        /// </summary>
        /// <param name="signal">Сигнал стратегии.</param>
        protected abstract void SendSignal(SignalData signal);

        /// <summary>
        /// Обрабатывает сообщения от клиента.
        /// </summary>
        /// <param name="client">Отправитель сообщения.</param>
        /// <param name="message">Сообщение.</param>
        protected abstract void MessageHandler(Client client, string[] message);
    }
}
