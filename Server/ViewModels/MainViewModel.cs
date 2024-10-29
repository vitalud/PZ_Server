using ReactiveUI;
using Server.Models;
using Server.Service.Abstract;
using System.Reactive;

namespace Server.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        private readonly MainModel _mainModel;
        private readonly ServerViewModel _server;
        private readonly ClientsViewModel _clients;
        private readonly BursesViewModel _burses;
        private readonly QuikViewModel _quik;
        private readonly StrategiesViewModel _strategies;

        public ReactiveCommand<Unit, Unit> TestCommand { get; }
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

        public MainViewModel(MainModel mainModel, ServerViewModel server, ClientsViewModel clients, BursesViewModel burses, QuikViewModel quik, StrategiesViewModel strategies)
        {
            _mainModel = mainModel;
            _server = server;
            _clients = clients;
            _burses = burses;
            _quik = quik;
            _strategies = strategies;

            CurrentViewModel = _burses;

            TestCommand = ReactiveCommand.Create(() => _mainModel.Test());
            ShowServerCommand = ReactiveCommand.Create(() => CurrentViewModel = _server);
            ShowClientsCommand = ReactiveCommand.Create(() => CurrentViewModel = _clients);
            ShowBursesCommand = ReactiveCommand.Create(() => CurrentViewModel = _burses);
            ShowQuikCommand = ReactiveCommand.Create(() => CurrentViewModel = _quik);
            ShowStrategiesCommand = ReactiveCommand.Create(() => CurrentViewModel = _strategies);
        }
    }
}
