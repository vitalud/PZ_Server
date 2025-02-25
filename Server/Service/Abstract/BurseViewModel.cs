using DynamicData;
using ProjectZeroLib.Enums;
using ReactiveUI;
using Strategies.Instruments;
using System.Collections.ObjectModel;
using System.Reactive;

namespace Server.Service.Abstract
{
    public partial class BurseViewModel : ReactiveObject
    {
        private readonly BurseModel _burseModel;

        private bool _isActive;
        private readonly ReadOnlyObservableCollection<Instrument> _instruments;

        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }
        public ReadOnlyObservableCollection<Instrument> Instruments => _instruments;
        public BurseName Name => _burseModel.Name;

        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

        public BurseViewModel(BurseModel burseModel)
        {
            _burseModel = burseModel;

            
            ConnectCommand = ReactiveCommand.Create(Connect);

            _burseModel.Instruments.Connect()
                       .Bind(out _instruments)
                       .Subscribe();

            _burseModel.WhenAnyValue(x => x.IsActive)
                .Subscribe(value => IsActive = value);
        }

        public void Connect()
        {
            _burseModel.Connect();
        }
    }
}
