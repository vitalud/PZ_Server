using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace Server.Service.Abstract
{
    public class ClientsViewModel : ReactiveObject
    {
        private readonly ClientsModel _clientsModel;

        private readonly ReadOnlyObservableCollection<Client> _clients;
        public ReadOnlyObservableCollection<Client> Clients => _clients;

        private readonly ReadOnlyObservableCollection<string> _logs;
        public ReadOnlyObservableCollection<string> Logs => _logs;
        public ClientsViewModel(ClientsModel clients)
        {
            _clientsModel = clients;
            _clientsModel.Clients.Connect()
                    .Bind(out _clients)
                    .Subscribe();
            _clientsModel.ClientLogs.Connect()
                    .Bind(out _logs)
                    .Subscribe();
        }
    }
}
