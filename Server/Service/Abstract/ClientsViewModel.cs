using DynamicData;
using ReactiveUI;
using Server.Service.UserClient;
using System.Collections.ObjectModel;

namespace Server.Service.Abstract
{
    public partial class ClientsViewModel : ReactiveObject
    {
        private readonly ClientsModel _clientsModel;

        private readonly ReadOnlyObservableCollection<Client> _clients;
        private readonly ReadOnlyObservableCollection<string> _logs;

        public ReadOnlyObservableCollection<Client> Clients => _clients;
        public ReadOnlyObservableCollection<string> Logs => _logs;

        public ClientsViewModel(ClientsModel clients)
        {
            _clientsModel = clients;

            _clientsModel.Clients.Connect()
                    .Bind(out _clients)
                    .Subscribe();
            _clientsModel.Logs.Connect()
                    .Bind(out _logs)
                    .Subscribe();
        }
    }
}
