using DynamicData;
using ReactiveUI;
using Server.Models;
using Strategies.Instruments;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace Server.ViewModels
{
    public partial class QuikViewModel : ReactiveObject
    {
        private readonly QuikModel _quik;

        private readonly ReadOnlyObservableCollection<Instrument> _instruments;
        private bool _isConnected;

        public ReadOnlyObservableCollection<Instrument> Instruments => _instruments;
        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }
        public IObservable<Exception> Exceptions => _quik.Exceptions;

        public QuikViewModel(QuikModel quik)
        {
            _quik = quik;

            IsConnected = _quik.IsConnected;
            _quik.WhenAnyValue(x => x.IsConnected)
                .Subscribe(value => IsConnected = value);

            _quik.Instruments.Connect()
                .Bind(out _instruments)
                .Subscribe();

            _quik.Start();
        }
    }
}
