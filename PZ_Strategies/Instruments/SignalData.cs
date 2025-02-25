using ProjectZeroLib.Enums;
using ReactiveUI;

namespace Strategies.Instruments
{
    /// <summary>
    /// Последние данные инструмента, используемые в расчете сигналов.
    /// </summary>
    public partial class SignalData : ReactiveObject
    {
        private bool _complete = false;
        private readonly List<Kline> _klines = [
                new(KlineInterval.OneMinute),
                new(KlineInterval.FiveMinutes),
                new(KlineInterval.FifteenMinutes),
                new(KlineInterval.OneHour),
                new(KlineInterval.FourHours),
                new(KlineInterval.OneDay),
            ];
        private readonly TradeBook _tradeBook = new();
        private readonly OrderBook _orderBook = new();

        public bool Complete
        {
            get => _complete;
            set => this.RaiseAndSetIfChanged(ref _complete, value);
        }
        public List<Kline> Klines => _klines;
        public TradeBook Trades => _tradeBook;
        public OrderBook Orders => _orderBook;
    }
}
