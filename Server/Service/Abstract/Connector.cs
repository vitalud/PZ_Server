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
        }

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
        public void ClearLogs()
        {
            _logs.Clear();
        }
    }
    public class DataReceivedEventArgs(string data) : EventArgs
    {
        public string ReceivedData { get; } = data;
    }
}
