using DynamicData;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using Server.Models;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace Server.ViewModels
{
    public class QuikViewModel : ReactiveObject
    {
        private readonly QuikModel _quik;

        private string _status;
        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        private readonly ReadOnlyObservableCollection<Instrument> _instruments;
        public ReadOnlyObservableCollection<Instrument> Instruments => _instruments;

        public QuikViewModel(QuikModel quik)
        {
            _quik = quik;

            Status = _quik.Status;
            this.WhenAnyValue(x => x.Status)
                .Skip(1)
                .Subscribe(value => _quik.Status = value);

            _quik.Instruments.Connect()
                .Bind(out _instruments)
                .Subscribe();
        }
    }
}
