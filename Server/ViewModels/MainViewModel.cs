using ReactiveUI;
using Server.Models;
using Server.Service.Abstract;
using System.Diagnostics;
using System.Reactive;

namespace Server.ViewModels
{
    public partial class MainViewModel : ReactiveObject
    {
        private readonly MainModel _mainModel;
        private readonly ServerViewModel _server;
        private readonly ClientsViewModel _clients;
        private readonly BursesViewModel _burses;
        private readonly QuikViewModel _quik;
        private readonly StrategiesViewModel _strategies;

        public MainModel MainModel => _mainModel;
        public ServerViewModel Server => _server;
        public ClientsViewModel Clients => _clients;
        public BursesViewModel Burses => _burses;
        public QuikViewModel Quik => _quik;
        public StrategiesViewModel Strategies => _strategies;

        public ReactiveCommand<Unit, ReactiveObject> ShowServerCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowClientsCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowBursesCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowQuikCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowStrategiesCommand { get; }

        private ReactiveObject _currentViewModel;
        public ReactiveObject CurrentViewModel
        {
            get => _currentViewModel;
            set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
        }

        /// <summary>
        /// TODO: обработчики ошибок всех сервисов.
        /// </summary>
        /// <param name="mainModel"></param>
        /// <param name="server"></param>
        /// <param name="clients"></param>
        /// <param name="burses"></param>
        /// <param name="quik"></param>
        /// <param name="strategies"></param>
        public MainViewModel(MainModel mainModel, ServerViewModel server, ClientsViewModel clients, BursesViewModel burses, QuikViewModel quik, StrategiesViewModel strategies)
        {
            _mainModel = mainModel;
            _server = server;
            _clients = clients;
            _burses = burses;
            _quik = quik;
            _strategies = strategies;

            _currentViewModel = _burses;

            ShowServerCommand = ReactiveCommand.Create(() => CurrentViewModel = Server);
            ShowClientsCommand = ReactiveCommand.Create(() => CurrentViewModel = Clients);
            ShowBursesCommand = ReactiveCommand.Create(() => CurrentViewModel = Burses);
            ShowQuikCommand = ReactiveCommand.Create(() => CurrentViewModel = Quik);
            ShowStrategiesCommand = ReactiveCommand.Create(() => CurrentViewModel = Strategies);

            
            _quik.Exceptions.Subscribe(Server.SendErrorMessage);
        }
    }
}
