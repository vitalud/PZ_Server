using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using System.Reactive.Linq;

namespace Server.Service.Abstract
{
    public abstract class ClientsModel : ReactiveObject
    {
        protected readonly string _path = $"{Environment.CurrentDirectory}\\Clients\\Clients.xlsx";
        private readonly object locker = new();

        protected readonly SourceList<Client> _clients;
        public SourceList<Client> Clients => _clients;

        protected readonly SourceList<string> _clientLogs = new();
        public SourceList<string> ClientLogs => _clientLogs;

        public ClientsModel()
        {
            _clients = new();
            _clients.Connect()
                    .Subscribe(OnClientsCountChanged);
        }

        private void OnClientsCountChanged(IChangeSet<Client> changes)
        {
            lock (locker)
            {
                foreach (var change in changes)
                {
                    if (change.Reason.Equals(ListChangeReason.Add))
                    {
                        var item = change.Item.Current;
                        Logger.AddLog(_clientLogs, $"added client {item.Data.Login}");
                        item.WhenAnyValue(x => x.Data.Payment)
                            .Subscribe(payment => OnPaymentChanged(item, payment));
                        item.WhenAnyValue(x => x.Data.Deposit)
                            .Skip(1)
                            .Subscribe(deposit => OnDepositChanged(item, deposit));
                        item.Data.Strategies.Connect()
                            .Subscribe(changes => OnClientStrategyTradeLimitChanged(changes, item.Data.Login));
                    }
                }
            }
        }

        private void OnClientStrategyTradeLimitChanged(IChangeSet<ShortStrategyInfo> changes, string login)
        {
            foreach (var change in changes)
            {
                if (change.Reason == ListChangeReason.Add)
                {
                    var item = change.Item.Current;
                    item.WhenAnyValue(x => x.TradeLimit)
                        .Skip(1)
                        .Subscribe(limit => OnTradeLimitChanged(item, limit, login));
                }
            }
        }

        protected abstract void OnTradeLimitChanged(ShortStrategyInfo item, int limit, string login);
        protected abstract void OnStrategiesChanged(Client client, int count, int oldCount);
        protected abstract void OnDepositChanged(Client client, int deposit);
        protected abstract void OnPaymentChanged(Client client, int deposit);
        protected abstract void GetClients();
        public void ClearLogs()
        {
            _clientLogs.Clear();
        }
    }
}
