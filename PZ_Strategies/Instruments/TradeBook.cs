using ReactiveUI;

namespace Strategies.Instruments
{
    /// <summary>
    /// Данные торговых сделок по инструменту.
    /// </summary>
    public partial class TradeBook : ReactiveObject
    {
        private decimal _buy;
        private decimal _sell;
        private decimal _volume;

        public decimal Buy
        {
            get => _buy;
            set => this.RaiseAndSetIfChanged(ref _buy, value);
        }
        public decimal Sell
        {
            get => _sell;
            set => this.RaiseAndSetIfChanged(ref _sell, value);
        }
        public decimal Volume
        {
            get => _volume;
            set => this.RaiseAndSetIfChanged(ref _volume, value);
        }
    }
}
