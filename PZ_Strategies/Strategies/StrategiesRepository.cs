using DynamicData;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Signal;
using ProjectZeroLib.Utils;
using ReactiveUI;
using Strategies.Instruments;
using System.IO;
using System.Reactive.Linq;
using SignalData = ProjectZeroLib.Signal.SignalData;

namespace Strategies.Strategies
{
    /// <summary>
    /// Хранилище стратегий.
    /// </summary>
    public class StrategiesRepository
    {
        private readonly InstrumentRepository _instruments;
        private readonly SourceList<Strategy> _strategies = new();
        private readonly string _images = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Images");

        public SourceList<Instrument> Instruments => _instruments.Instruments;
        public SourceList<Strategy> StrategiesList => _strategies;

        public StrategiesRepository(InstrumentRepository instruments)
        {
            _instruments = instruments;

            InitializeStrategies();
        }

        /// <summary>
        /// Инициализирует контейнер со стратегиями.
        /// </summary>
        private void InitializeStrategies()
        {
            StartStatusUpdater();

            AddStrategy0001();
            AddStrategy0002();
            AddStrategy0003();

            AddStrategy1001();
            AddStrategy1002();
            AddStrategy1003();

            AddStrategy2001();
            AddStrategy2002();
            AddStrategy2003();

            AddStrategy3003();
        }

        /// <summary>
        /// Запускает со следующей минуты после запуска ежеминутную задачу
        /// по обновлению статуса инструментов за пару секунд до получения
        /// новых данных по инструменту.
        /// </summary>
        private async void StartStatusUpdater()
        {
            var currentTime = DateTime.Now;
            var secondsRemaining = 57 - currentTime.Second;
            if (secondsRemaining < 0)
                secondsRemaining += 60;
            var initialDelay = TimeSpan.FromSeconds(secondsRemaining);

            await Task.Delay(initialDelay);

            Observable.Interval(TimeSpan.FromMinutes(1))
                .Subscribe(_ => UpdateStatus());
        }

        /// <summary>
        /// Переводит статут инструментов в false.
        /// </summary>
        private void UpdateStatus()
        {
            foreach (var inst in Instruments.Items)
            {
                if (inst.SignalData.Complete)
                    inst.SignalData.Complete = false;
            }
        }

        #region Okx
        private void AddStrategy0001()
        {
            var code = "0001";
            var burse = BurseName.Okx;

            var telegram = new Telegram(
                "1",
                "a",
                2,
                "Описание стратегии " + code,
                Path.Combine(_images, code + ".jpg"),
                10000);

            var date = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);

            var stocks = new List<Stock>()
            {
                new(burse, "BTC-USDT", "Spot", 0.1m, 1, 5),
                new(burse, $"BTC-USDT-{date:yyMMdd}", "Futures", 0.1m, 0.01m, 1)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);

            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("BTC-USDT")
                && x.Name.Type.Equals("Spot"));
            var B = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.FirstName.Equals("BTC-USDT-")
                && x.Name.Expiration != null
                && x.Name.Type.Equals("Futures"));

            if (A != null && B != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete), B.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete && B.SignalData.Complete)
                        {
                            str.Signal = Strat0001();
                        }
                    });
            }
        }
        private SignalData Strat0001()
        {
            var signal = new SignalData("0001");

            var instruments = Instruments.Items;
            var data1 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(("BTC-USDT", "Spot")));
            var data2 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(($"BTC-USDT-{Expiration.GetQuarterExpirationDate(DateTime.Now):yyMMdd}", "Futures")));
            if (data1 != null && data2 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                var B = data2.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null && B != null)
                {
                    int daysLeft = (Expiration.GetQuarterExpirationDate(DateTime.Now) - DateTime.Now).Days;
                    if (A.Close != 0 && daysLeft != 0)
                    {
                        decimal profit = (B.Close - A.Close) / A.Close;
                        decimal modifier = A.Close / (A.Close + B.Close / 10);
                        decimal sprad = Math.Round(100 * (365 / daysLeft) * modifier * profit, 2);
                        if (sprad > (decimal)3.5)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 100;
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data1.Name.Id}", "Spot", Side.Buy));
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data2.Name.Id}", "Futures", Side.Sell));
                        }
                        else if (sprad < (decimal)3.0)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 0;
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data1.Name.Id}", "Spot", Side.Sell));
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data2.Name.Id}", "Futures", Side.Buy));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        private void AddStrategy0002()
        {
            var code = "0002";
            var burse = BurseName.Okx;

            var telegram = new Telegram(
                "1",
                "a",
                3,
                "Описание стратегии " + code,
                Path.Combine(_images, code + ".jpg"),
                10000);

            var date = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);

            var stocks = new List<Stock>()
            {
                new(burse, "ETH-USDT", "Spot", 0.1m, 1, 4),
                new(burse, $"ETH-USDT-{date:yyMMdd}", "Futures", 0.1m, 0.01m, 1),
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);
            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("ETH-USDT")
                && x.Name.Type.Equals("Spot"));
            var B = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.FirstName.Equals("ETH-USDT-")
                && x.Name.Expiration != null
                && x.Name.Type.Equals("Futures"));
            if (A != null && B != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete), B.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete && B.SignalData.Complete)
                        {
                            str.Signal = Strat0002();
                        }
                    });
            }
        }
        private SignalData Strat0002()
        {
            var signal = new SignalData("0002");

            var instruments = Instruments.Items;
            var data1 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(("ETH-USDT", "Spot")));
            var data2 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(($"ETH-USDT-{Expiration.GetQuarterExpirationDate(DateTime.Now):yyMMdd}", "Futures")));
            if (data1 != null && data2 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                var B = data2.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null && B != null)
                {
                    int daysLeft = (Expiration.GetQuarterExpirationDate(DateTime.Now) - DateTime.Now).Days;
                    if (A.Close != 0 && daysLeft != 0)
                    {
                        decimal profit = (B.Close - A.Close) / A.Close;
                        decimal modifier = A.Close / (A.Close + B.Close / 10);
                        decimal sprad = Math.Round(100 * (365 / daysLeft) * modifier * profit, 2);
                        if (sprad > (decimal)3.5)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 100;
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data1.Name.Id}", "Spot", Side.Buy));
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data2.Name.Id}", "Futures", Side.Sell));
                        }
                        else if (sprad < (decimal)3.0)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 0;
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data1.Name.Id}", "Spot", Side.Sell));
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data2.Name.Id}", "Futures", Side.Buy));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        private void AddStrategy0003()
        {
            var code = "0003";
            var burse = BurseName.Okx;

            var telegram = new Telegram(
                "2",
                "e",
                2,
                "Описание стратегии " + code,
                Path.Combine(_images, code + ".jpg"),
                10000);

            var stocks = new List<Stock>()
            {
                new(burse, "BTC-USDT", "Spot", 0.1m, 1, 5)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);
            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("BTC-USDT")
                && x.Name.Type.Equals("Spot"));
            if (A != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete)
                        {
                            str.Signal = Strat0003();
                        }
                    });
            }
        }
        private SignalData Strat0003()
        {
            var signal = new SignalData("0003");

            var data1 = Instruments.Items.FirstOrDefault(x => (x.Name.Id, x.Name.Type, x.Burse).Equals(("BTC-USDT", "Spot", BurseName.Okx)));
            if (data1 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null)
                {
                    if (data1.Other.Average60Close != 0)
                    {
                        decimal sprad = Math.Round(100 * (A.Close - data1.Other.Average60Close) / data1.Other.Average60Close, 2);
                        if (Math.Abs(sprad) > (decimal)0.0000001)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            var side = sprad > 0 ? Side.Buy : Side.Sell;
                            signal.Stocks.Add(new StockSignal(BurseName.Okx, $"{data1.Name.Id}", "Spot", side));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        #endregion

        #region Binance
        private void AddStrategy1001()
        {
            var code = "1001";
            var burse = BurseName.Binance;

            var telegram = new Telegram(
                "1",
                "a",
                2,
                "Описание стратегии " + code,
                Path.Combine(_images, "common.jpg"),
                10000);

            var date = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);

            var stocks = new List<Stock>()
            {
                new(burse, "BTCUSDT", "Spot", 0.1m, 1, 3),
                new(burse, $"BTCUSDT_{date:yyMMdd}", "UsdFutures", 0.1m, 0.01m, 1)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);

            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("BTCUSDT")
                && x.Name.Type.Equals("Spot"));
            var B = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.FirstName.Equals("BTCUSDT_")
                && x.Name.Expiration != null
                && x.Name.Type.Equals("UsdFutures"));

            if (A != null && B != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete), B.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete && B.SignalData.Complete)
                        {
                            str.Signal = Strat1001();
                        }
                    });
            }
        }
        private SignalData Strat1001()
        {
            var signal = new SignalData("1001");

            var instruments = Instruments.Items;
            var data1 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(("BTCUSDT", "Spot")));
            var data2 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(($"BTCUSDT_{Expiration.GetQuarterExpirationDate(DateTime.Now):yyMMdd}", "UsdFutures")));
            if (data1 != null && data2 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                var B = data2.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null && B != null)
                {
                    int daysLeft = (Expiration.GetQuarterExpirationDate(DateTime.Now) - DateTime.Now).Days;
                    if (A.Close != 0 && daysLeft != 0)
                    {
                        decimal profit = (B.Close - A.Close) / A.Close;
                        decimal modifier = A.Close / (A.Close + B.Close / 10);
                        decimal sprad = Math.Round(100 * (365 / daysLeft) * modifier * profit, 2);
                        if (sprad > (decimal)3.5)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 100;
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data1.Name.Id}", "Spot", Side.Buy));
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data2.Name.Id}", "UsdFutures", Side.Sell));
                        }
                        else if (sprad < (decimal)3.0)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 0;
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data1.Name.Id}", "Spot", Side.Sell));
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data2.Name.Id}", "UsdFutures", Side.Buy));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        private void AddStrategy1002()
        {
            var code = "1002";
            var burse = BurseName.Binance;

            var telegram = new Telegram(
                "1",
                "a",
                3,
                "Описание стратегии " + code,
                Path.Combine(_images, "common.jpg"),
                10000);

            var date = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);

            var stocks = new List<Stock>()
            {
                new(burse, "ETHUSDT", "Spot", 0.1m, 1, 3),
                new(burse, $"ETHUSDT_{date:yyMMdd}", "UsdFutures", 0.1m, 0.01m, 1),
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);
            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("ETHUSDT")
                && x.Name.Type.Equals("Spot"));
            var B = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.FirstName.Equals("ETHUSDT_")
                && x.Name.Expiration != null
                && x.Name.Type.Equals("UsdFutures"));
            if (A != null && B != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete), B.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete && B.SignalData.Complete)
                        {
                            str.Signal = Strat1002();
                        }
                    });
            }
        }
        private SignalData Strat1002()
        {
            var signal = new SignalData("1002");

            var instruments = Instruments.Items;
            var data1 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(("ETHUSDT", "Spot")));
            var data2 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(($"ETHUSDT_{Expiration.GetQuarterExpirationDate(DateTime.Now):yyMMdd}", "UsdFutures")));

            if (data1 != null && data2 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                var B = data2.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null && B != null)
                {
                    int daysLeft = (Expiration.GetQuarterExpirationDate(DateTime.Now) - DateTime.Now).Days;
                    if (A.Close != 0 && daysLeft != 0)
                    {
                        decimal profit = (B.Close - A.Close) / A.Close;
                        decimal modifier = A.Close / (A.Close + B.Close / 10);
                        decimal sprad = Math.Round(100 * (365 / daysLeft) * modifier * profit, 2);
                        if (sprad > (decimal)3.5)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 100;
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data1.Name.Id}", "Spot", Side.Buy));
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data2.Name.Id}", "UsdFutures", Side.Sell));
                        }
                        else if (sprad < (decimal)3.0)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 0;
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data1.Name.Id}", "Spot", Side.Sell));
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data2.Name.Id}", "UsdFutures", Side.Buy));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        private void AddStrategy1003()
        {
            var code = "1003";
            var burse = BurseName.Binance;

            var telegram = new Telegram(
                "2",
                "e",
                2,
                "Описание стратегии " + code,
                Path.Combine(_images, code + ".jpg"),
                10000);

            var stocks = new List<Stock>()
            {
                new(burse, "BTCUSDT", "Spot", 0.1m, 1, 3)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);

            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("BTCUSDT")
                && x.Name.Type.Equals("Spot"));

            if (A != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete)
                        {
                            str.Signal = Strat1003();
                        }
                    });
            }
        }
        private SignalData Strat1003()
        {
            var signal = new SignalData("1003");

            var data1 = Instruments.Items.FirstOrDefault(x => (x.Name.Id, x.Name.Type, x.Burse).Equals(("BTCUSDT", "Spot", BurseName.Binance)));
            if (data1 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null)
                {
                    if (data1.Other.Average60Close != 0)
                    {
                        decimal sprad = Math.Round(100 * (A.Close - data1.Other.Average60Close) / data1.Other.Average60Close, 2);
                        if (Math.Abs(sprad) > (decimal)0.0000001)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            var side = sprad > 0 ? Side.Buy : Side.Sell;
                            signal.Stocks.Add(new StockSignal(BurseName.Binance, $"{data1.Name.Id}", "Spot", side));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        #endregion

        #region Bybit
        private void AddStrategy2001()
        {
            var code = "2001";
            var burse = BurseName.Bybit;

            var telegram = new Telegram(
                "1",
                "a",
                2,
                "Описание стратегии " + code,
                Path.Combine(_images, "common.jpg"),
                10000);

            var raw = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);
            var date = Expiration.ConvertExpirationDateToBybitCode(raw);

            var stocks = new List<Stock>()
            {
                new(burse, "BTCUSDT", "Spot", 0.1m, 1, 3),
                new(burse, $"BTCUSD{date}", "InverseFutures", 0.1m, 1, 4)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);

            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("BTCUSDT")
                && x.Name.Type.Equals("Spot"));
            var B = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.FirstName.Equals("BTCUSD")
                && x.Name.Expiration != null
                && x.Name.Type.Equals("InverseFutures"));

            if (A != null && B != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete), B.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete && B.SignalData.Complete)
                        {
                            str.Signal = Strat2001();
                        }
                    });
            }
        }
        private SignalData Strat2001()
        {
            var signal = new SignalData("2001");
            var raw = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);
            var date = Expiration.ConvertExpirationDateToBybitCode(raw);

            var instruments = Instruments.Items;
            var data1 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(("BTCUSDT", "Spot")));
            var data2 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(($"BTCUSD{date}", "InverseFutures")));
            
            if (data1 != null && data2 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                var B = data2.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null && B != null)
                {
                    int daysLeft = (Expiration.GetQuarterExpirationDate(DateTime.Now) - DateTime.Now).Days;
                    if (A.Close != 0 && daysLeft != 0)
                    {
                        decimal profit = (B.Close - A.Close) / A.Close;
                        decimal modifier = A.Close / (A.Close + B.Close / 10);
                        decimal sprad = Math.Round(100 * (365 / daysLeft) * modifier * profit, 2);
                        if (sprad > (decimal)3.5)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 100;
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data1.Name.Id}", "Spot", Side.Buy));
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data2.Name.Id}", "InverseFutures", Side.Sell));
                        }
                        else if (sprad < (decimal)3.0)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 0;
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data1.Name.Id}", "Spot", Side.Sell));
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data2.Name.Id}", "InverseFutures", Side.Buy));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        private void AddStrategy2002()
        {
            var code = "2002";
            var burse = BurseName.Bybit;

            var telegram = new Telegram(
                "1",
                "a",
                3,
                "Описание стратегии " + code,
                Path.Combine(_images, "common.jpg"),
                10000);

            var raw = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);
            var date = Expiration.ConvertExpirationDateToBybitCode(raw);

            var stocks = new List<Stock>()
            {
                new(burse, "ETHUSDT", "Spot", 0.1m, 1, 2),
                new(burse, $"ETHUSD{date}", "InverseFutures", 0.1m, 1, 1),
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);
            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("ETHUSDT")
                && x.Name.Type.Equals("Spot"));
            var B = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.FirstName.Equals("ETHUSD")
                && x.Name.Expiration != null
                && x.Name.Type.Equals("InverseFutures"));
            if (A != null && B != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete), B.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete && B.SignalData.Complete)
                        {
                            str.Signal = Strat2002();
                        }
                    });
            }
        }
        private SignalData Strat2002()
        {
            var signal = new SignalData("2002");
            var raw = Expiration.GetQuarterExpirationDate(DateTime.UtcNow);
            var date = Expiration.ConvertExpirationDateToBybitCode(raw);

            var instruments = Instruments.Items;
            var data1 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(("ETHUSDT", "Spot")));
            var data2 = instruments.FirstOrDefault(x => (x.Name.Id, x.Name.Type).Equals(($"ETHUSD{date}", "InverseFutures")));

            if (data1 != null && data2 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                var B = data2.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null && B != null)
                {
                    int daysLeft = (Expiration.GetQuarterExpirationDate(DateTime.Now) - DateTime.Now).Days;
                    if (A.Close != 0 && daysLeft != 0)
                    {
                        decimal profit = (B.Close - A.Close) / A.Close;
                        decimal modifier = A.Close / (A.Close + B.Close / 10);
                        decimal sprad = Math.Round(100 * (365 / daysLeft) * modifier * profit, 2);
                        if (sprad > (decimal)3.5)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 100;
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data1.Name.Id}", "Spot", Side.Buy));
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data2.Name.Id}", "InverseFutures", Side.Sell));
                        }
                        else if (sprad < (decimal)3.0)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            signal.Percent = 0;
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data1.Name.Id}", "Spot", Side.Sell));
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data2.Name.Id}", "InverseFutures", Side.Buy));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        private void AddStrategy2003()
        {
            var code = "2003";
            var burse = BurseName.Bybit;

            var telegram = new Telegram(
                "2",
                "e",
                2,
                "Описание стратегии " + code,
                Path.Combine(_images, code + ".jpg"),
                10000);

            var stocks = new List<Stock>()
            {
                new(burse, "BTCUSDT", "Spot", 0.1m, 1, 3)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);

            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("BTCUSDT")
                && x.Name.Type.Equals("Spot"));

            if (A != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete)
                        {
                            str.Signal = Strat2003();
                        }
                    });
            }
        }
        private SignalData Strat2003()
        {
            var signal = new SignalData("2003");

            var data1 = Instruments.Items.FirstOrDefault(x => (x.Name.Id, x.Name.Type, x.Burse).Equals(("BTCUSDT", "Spot", BurseName.Bybit)));
            if (data1 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null)
                {
                    if (data1.Other.Average60Close != 0)
                    {
                        decimal sprad = Math.Round(100 * (A.Close - data1.Other.Average60Close) / data1.Other.Average60Close, 2);
                        if (Math.Abs(sprad) > (decimal)0.0000001)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            var side = sprad > 0 ? Side.Buy : Side.Sell;
                            signal.Stocks.Add(new StockSignal(BurseName.Bybit, $"{data1.Name.Id}", "Spot", side));
                        }
                        else signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        #endregion

        #region Quik
        private void AddStrategy3003()
        {
            var code = "3003";
            var burse = BurseName.Quik;

            var telegram = new Telegram(
                "2",
                "e",
                4,
                "Описание стратегии " + code,
                Path.Combine(_images, code + ".jpg"),
                12000);

            var stocks = new List<Stock>() 
            {
                new(burse, "SBER", "TQBR", 1, 1, 1)
            };

            var str = new Strategy(telegram, stocks, burse, code, 1);
            _strategies.Add(str);

            var A = Instruments.Items.FirstOrDefault(x => x.Burse.Equals(burse)
                && x.Name.Id.Equals("SBER")
                && x.Name.Type.Equals("TQBR"));

            if (A != null)
            {
                Observable.CombineLatest(A.WhenAnyValue(x => x.SignalData.Complete))
                    .Subscribe(_ =>
                    {
                        if (A.SignalData.Complete)
                        {
                            str.Signal = Strat3003();
                        }
                    });
            }
        }
        private SignalData Strat3003()
        {
            var signal = new SignalData("3003");

            var data1 = Instruments.Items.FirstOrDefault(x =>
                x.Name.Id == "SBER" &&
                x.Name.Type == "TQBR" &&
                x.Burse == BurseName.Quik);

            if (data1 != null)
            {
                var A = data1.SignalData.Klines.Find(x => x.Interval.Equals(KlineInterval.OneMinute));
                if (A != null)
                {
                    if (data1.Other.Average60Close != 0)
                    {
                        decimal sprad = Math.Round(100 * (A.Close - data1.Other.Average60Close) / data1.Other.Average60Close, 2);
                        if (Math.Abs(sprad) > (decimal)0.0000001)
                        {
                            signal.Signal = $"Order -> {sprad}%";
                            var side = sprad > 0 ? Side.Buy : Side.Sell;
                            signal.Stocks.Add(new StockSignal(data1.Burse, $"{data1.Name.Id}", $"{data1.Name.Type}", side));
                        }
                        else
                            signal.Signal = $"No order -> {sprad}%";
                    }
                }
            }
            return signal;
        }
        #endregion
    }
}
