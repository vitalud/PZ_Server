using DynamicData;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;

namespace Server.Service.Abstract
{
    public class BurseViewModel : ReactiveObject
    {
        private readonly BurseModel _burseModel;

        private readonly ReadOnlyObservableCollection<Instrument> _instruments;
        public ReadOnlyObservableCollection<Instrument> Instruments => _instruments;

        private readonly ObservableAsPropertyHelper<bool> _isActive;
        public bool IsActive => _isActive.Value;

        public BurseName Name => _burseModel.Name;
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

        public BurseViewModel(BurseModel burseModel)
        {
            _burseModel = burseModel;

            ConnectCommand = ReactiveCommand.Create(Connect);

            _burseModel.Instruments.Connect()
                       .Bind(out _instruments)
                       .Subscribe();

            this.WhenAnyValue(x => x._burseModel.IsActive).ToProperty(this, x => x.IsActive, out _isActive);
        }

        public void Connect()
        {
            if (!IsActive) _burseModel.Connect();
            else _burseModel.Disconnect();
        }
    }
}
