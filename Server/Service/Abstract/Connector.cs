using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;

namespace Server.Service.Abstract
{
    public abstract class Connector : ReactiveObject
    {
        protected readonly ClientsModel _clientDataBase;
        protected readonly Strategies _strategies;

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

        public Connector(ClientsModel clientDataBase, Strategies strategies)
        {
            _clientDataBase = clientDataBase;
            _strategies = strategies;

            foreach (var strat in _strategies.StrategiesList.Items) 
            {
                strat.WhenAnyValue(x => x.Signal)
                    .Skip(1)
                    .Subscribe(SendSignal);
            }

            _clientDataBase.Clients.Connect()
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
        private void OnStrategiesCountChanged(IChangeSet<ShortStrategyInfo> changes, Client client)
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

        protected abstract void SendStrategy(ShortStrategyInfo data, Client client);
        protected abstract void SendSignal(Signal signal);
        protected abstract void MessageHandler(Client client, string[] message);
        protected bool CheckStrategy(Client client, string code)
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
        public abstract void ConnectorStart();
        public abstract void ConnectorStop();
    }

    public class DataReceivedEventArgs(string data) : EventArgs
    {
        public string ReceivedData { get; } = data;
    }
}
