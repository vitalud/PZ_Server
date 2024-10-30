using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using System.Reactive.Linq;

namespace Server.Service.Abstract
{
    public abstract class ClientsModel : ReactiveObject
    {
        protected readonly string _path = $"{Environment.CurrentDirectory}\\Clients\\Clients.xlsx";
        protected readonly object _locker = new();

        protected readonly SourceList<Client> _clients = new();
        public SourceList<Client> Clients => _clients;

        protected readonly SourceList<string> _clientLogs = new();
        public SourceList<string> ClientLogs => _clientLogs;

        protected abstract void GetClients();
        protected abstract void OnClientsCountChanged(IChangeSet<Client> changes);
        protected abstract void OnStrategiesCountChanged(IChangeSet<ShortStrategyInfo> changes, Client client);
        protected abstract void OnDepositChanged(Client client, int deposit);
        protected abstract void OnPaymentChanged(Client client, int payment);
        protected abstract void AddStrategyToClient(ShortStrategyInfo data, Client client);
        protected abstract void RemoveStrategyFromClient(ShortStrategyInfo data, Client client);
    }
}
