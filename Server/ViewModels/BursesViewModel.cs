using ReactiveUI;
using Server.Service.Abstract;
using Server.ViewModels.Burse;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;

namespace Server.ViewModels
{
    public partial class BursesViewModel : ReactiveObject
    {
        private readonly BurseViewModel _okxViewModel;
        private readonly BurseViewModel _binanceViewModel;
        private readonly BurseViewModel _bybitViewModel;
        private ReactiveObject _currentBurseViewModel;

        public BurseViewModel OkxViewModel => _okxViewModel;
        public BurseViewModel BinanceViewModel => _binanceViewModel;
        public BurseViewModel BybitViewModel => _bybitViewModel;
        public ReactiveObject CurrentBurseViewModel
        {
            get => _currentBurseViewModel;
            set => this.RaiseAndSetIfChanged(ref _currentBurseViewModel, value);
        }

        public ObservableCollection<BurseViewModel> BurseViewModels { get; set; } = [];

        public ReactiveCommand<Unit, Unit> ConnectAllCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowOkxCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowBinanceCommand { get; }
        public ReactiveCommand<Unit, ReactiveObject> ShowBybitCommand { get; }

        public BursesViewModel(OkxViewModel okxViewModel, BinanceViewModel binanceViewModel, BybitViewModel bybitViewModel)
        {
            _okxViewModel = okxViewModel;
            _binanceViewModel = binanceViewModel;
            _bybitViewModel = bybitViewModel;

            _currentBurseViewModel = OkxViewModel;

            BurseViewModels.Add(OkxViewModel);
            BurseViewModels.Add(BinanceViewModel);
            BurseViewModels.Add(BybitViewModel);

            ConnectAllCommand = ReactiveCommand.Create(() =>
            {
                foreach (var burse in BurseViewModels)
                    burse.Connect();
            });

            OpenLogsCommand = ReactiveCommand.Create(() =>
            {
                Process.Start("explorer.exe", Path.Combine(Environment.SpecialFolder.MyDocuments.ToString(), "Logging"));
            });

            ShowOkxCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _okxViewModel);
            ShowBinanceCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _binanceViewModel);
            ShowBybitCommand = ReactiveCommand.Create(() => CurrentBurseViewModel = _bybitViewModel);
        }
    }
}
