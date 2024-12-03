using DynamicData;
using NUnit.Framework.Internal.Commands;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using Server.Models;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;

namespace Server.ViewModels
{
    public class QuikViewModel : ReactiveObject
    {
        private readonly QuikModel _quik;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }

        private readonly ReadOnlyObservableCollection<Instrument> _instruments;
        public ReadOnlyObservableCollection<Instrument> Instruments => _instruments;

        public QuikViewModel(QuikModel quik)
        {
            _quik = quik;

            IsConnected = _quik.IsConnected;
            _quik.WhenAnyValue(x => x.IsConnected)
                .Subscribe(value => IsConnected = value);

            _quik.Instruments.Connect()
                .Bind(out _instruments)
                .Subscribe();
        }
    }
}
