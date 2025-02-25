using DynamicData;
using ReactiveUI;
using Server.Service.UserClient;
using System.IO;

namespace Server.Service.Abstract
{
    /// <summary>
    /// Абстрактный класс, описывающий методы реализации базы данных клиентов.
    /// </summary>
    public abstract class ClientsModel : ReactiveObject
    {
        protected readonly string _path = Path.Combine($"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}", "Clients.xlsx");
        protected readonly object _locker = new();

        protected readonly SourceList<Client> _clients = new();
        protected readonly SourceList<string> _logs = new();

        public SourceList<Client> Clients => _clients;
        public SourceList<string> Logs => _logs;

        protected abstract void GetClients();
        protected abstract void OnClientsCountChanged(IChangeSet<Client> changes);
        protected abstract void OnStrategiesCountChanged(IChangeSet<StrategySummary> changes, Client client);
        protected abstract void OnDepositChanged(Client client, int deposit);
        protected abstract void OnPaymentChanged(Client client, int payment);
        protected abstract void AddStrategyToClient(StrategySummary data, Client client);
        protected abstract void RemoveStrategyFromClient(StrategySummary data, Client client);
    }
}
