using ProjectZeroLib.Enums;
using ReactiveUI;

namespace Strategies.Instruments
{
    /// <summary>
    /// Данные свечи.
    /// </summary>
    /// <param name="interval">Интервал свечи.</param>
    public partial class Kline(KlineInterval interval) : ReactiveObject
    {
        public KlineInterval Interval { get; } = interval;

        private int _day = DateTime.UtcNow.Year * 10000 + DateTime.UtcNow.Month * 100 + DateTime.UtcNow.Day;
        private int _time = DateTime.UtcNow.Hour * 10000 + DateTime.UtcNow.Minute * 100;
        private decimal _open;
        private decimal _high;
        private decimal _low;
        private decimal _close;

        public int Day
        {
            get => _day;
            set => this.RaiseAndSetIfChanged(ref _day, value);
        }
        public int Time
        {
            get => _time;
            set => this.RaiseAndSetIfChanged(ref _time, value);
        }
        public decimal Open
        {
            get => _open;
            set => this.RaiseAndSetIfChanged(ref _open, value);
        }
        public decimal High
        {
            get => _high;
            set => this.RaiseAndSetIfChanged(ref _high, value);
        }
        public decimal Low
        {
            get => _low;
            set => this.RaiseAndSetIfChanged(ref _low, value);
        }
        public decimal Close
        {
            get => _close;
            set => this.RaiseAndSetIfChanged(ref _close, value);
        }
    }
}
