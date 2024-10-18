using ProjectZeroLib.Instruments;
using ReactiveUI;
using Server.Service.Abstract;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;

namespace Server.ViewModels
{
    public class BursesViewModel : ReactiveObject
    {
        private readonly BurseViewModel _okxViewModel;
        private readonly BurseViewModel _binanceViewModel;
        private readonly BurseViewModel _bybitViewModel;
        private readonly BurseViewModel _quikViewModel;

        public BurseViewModel OkxViewModel => _okxViewModel;
        public BurseViewModel BinanceViewModel => _binanceViewModel;
        public BurseViewModel BybitViewModel => _bybitViewModel;
        public BurseViewModel QuikViewModel => _quikViewModel;

        public ObservableCollection<BurseViewModel> BurseViewModels { get; set; } = [];

        private readonly ReadOnlyObservableCollection<Instrument> _instruments;
        public ReadOnlyObservableCollection<Instrument> Instruments => _instruments;

        public ReactiveCommand<Unit, Unit> ConnectAllCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowOkxCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowBinanceCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowBybitCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowQuikCommand { get; }

        private ReactiveObject _currentBurseViewModel;
        public ReactiveObject CurrentBurseViewModel
        {
            get => _currentBurseViewModel;
            set => this.RaiseAndSetIfChanged(ref _currentBurseViewModel, value);
        }

        public BursesViewModel(OkxViewModel okxViewModel, BinanceViewModel binanceViewModel, BybitViewModel bybitViewModel, QuikViewModel quikViewModel)
        {
            _okxViewModel = okxViewModel;
            _binanceViewModel = binanceViewModel;
            _bybitViewModel = bybitViewModel;
            _quikViewModel = quikViewModel;

            BurseViewModels.Add(_okxViewModel);
            BurseViewModels.Add(_binanceViewModel);
            BurseViewModels.Add(_bybitViewModel);
            BurseViewModels.Add(_quikViewModel);

            ConnectAllCommand = ReactiveCommand.Create(() =>
            {
                foreach (var burse in BurseViewModels)
                    burse.Connect();
            });

            OpenLogsCommand = ReactiveCommand.Create(() =>
            {
                Process.Start("explorer.exe", Environment.CurrentDirectory);
            });

            CurrentBurseViewModel = _okxViewModel;

            ShowOkxCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _okxViewModel);
            ShowBinanceCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _binanceViewModel);
            ShowBybitCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _bybitViewModel);
            ShowQuikCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _quikViewModel);
        }
    }
}
