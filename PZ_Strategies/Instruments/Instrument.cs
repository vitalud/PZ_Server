using DynamicData;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Utils;
using ReactiveUI;
using System.IO;
using System.Reactive.Linq;

namespace Strategies.Instruments
{
    /// <summary>
    /// Данные инструмента.
    /// </summary>
    public partial class Instrument : ReactiveObject
    {
        private bool _isActive = false;
        private readonly bool _logging;
        private DateTime _lastUpdate = DateTime.Now;

        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }
        public bool Logging => _logging;
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set => this.RaiseAndSetIfChanged(ref _lastUpdate, value);
        }

        public BurseName Burse { get; set; }
        public Name Name { get; set; }
        public List<Kline> Klines { get; set; } = [
                new(KlineInterval.OneMinute),
                new(KlineInterval.FiveMinutes),
                new(KlineInterval.FifteenMinutes),
                new(KlineInterval.OneHour),
                new(KlineInterval.FourHours),
                new(KlineInterval.OneDay),
            ];
        public TradeBook Trades { get; set; } = new();
        public OrderBook Orders { get; set; } = new();
        public Indicators Other { get; set; } = new();
        public SignalData SignalData { get; set; } = new();

        public Instrument(BurseName burse, Name name, bool logging)
        {
            Burse = burse;
            Name = name;
            _logging = logging;

            CreateSubscriptions();
        }

        /// <summary>
        /// Создает подписки для формирования <see cref="SignalData"/> для каждого интервала свечей,
        /// а также подписки для добавления последних значений индикаторов в блок прочих вычисляемых
        /// индикаторов.
        /// </summary>
        private void CreateSubscriptions()
        {
            foreach (var kline in Klines)
            {
                kline.WhenAnyValue(x => x.Time)
                    .Skip(1)
                    .Subscribe(_ => GetSignalData(kline.Interval));
            }

            var oneMinuteKline = SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
            oneMinuteKline?.WhenAnyValue(x => x.Close)
                                .Subscribe(Other.Last60Close.Add);
        }

        /// <summary>
        /// Сохраняет последние данные инструмента в <see cref="SignalData"/>.
        /// </summary>
        /// <param name="interval">Интервал свечи.</param>
        private void GetSignalData(KlineInterval interval)
        {
            var signal = SignalData.Klines.Find(x => x.Interval.Equals(interval));
            var current = Klines.Find(x => x.Interval.Equals(interval));
            if (signal != null && current != null)
            {
                signal.Open = current.Open;
                signal.Close = current.Close;
                signal.High = current.High;
                signal.Low = current.Low;
                signal.Day = current.Day;
                signal.Time = current.Time;

                if (interval.Equals(KlineInterval.OneMinute))
                {
                    var signalOrderBook = SignalData.Orders;
                    var currentOrderBook = Orders;
                    signalOrderBook.AllAsks = currentOrderBook.AllAsks;
                    signalOrderBook.AllBids = currentOrderBook.AllBids;
                    signalOrderBook.NumAsks = currentOrderBook.NumAsks;
                    signalOrderBook.NumBids = currentOrderBook.NumBids;
                    signalOrderBook.BestAsks = currentOrderBook.BestAsks;
                    signalOrderBook.BestBids = currentOrderBook.BestBids;
                    var signalTrades = SignalData.Trades;
                    var currentTrades = Trades;
                    signalTrades.Buy = currentTrades.Buy;
                    signalTrades.Sell = currentTrades.Sell;
                    signalTrades.Volume = currentTrades.Volume;

                    if (Logging)
                        LogData();

                    SignalData.Complete = true;
                    ClearData();
                }
            }
        }

        /// <summary>
        /// Формирует последние данные инструмента для логирования.
        /// Сохраняет данные для минутных свечей.
        /// </summary>
        private void LogData()
        {
            var data = SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
            if (data != null)
            {
                var year = data.Day.ToString()[..4];
                var month = data.Day.ToString().Substring(4, 2);
                var day = data.Day.ToString().Substring(6, 2);
                var time = data.Time.ToString().PadLeft(6, '0');
                var path = Path.Combine(
                    "Logging",
                    year,
                    month,
                    day,
                    Burse.ToString(),
                    Name.Type
                );
                var date = $"{data.Day} {time}";
                var kline = $"{data.Open} {data.High} {data.Low} {data.Close}";
                var trades = $"{SignalData.Trades.Sell} {SignalData.Trades.Buy}";
                var orderbook = $"{SignalData.Orders.AllAsks} {SignalData.Orders.BestAsks} {SignalData.Orders.NumAsks} {SignalData.Orders.AllBids} {SignalData.Orders.BestBids} {SignalData.Orders.NumBids}";
                Logger.LogInstrumentData(path, $"{Name.Id}.txt", date, kline, trades, orderbook);
            }
        }

        /// <summary>
        /// Обнуляет данные по количеству торговых сделок.
        /// </summary>
        private void ClearData()
        {
            Trades.Buy = 0;
            Trades.Sell = 0;
        }
    }
}
