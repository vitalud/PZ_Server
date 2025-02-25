using ProjectZeroLib.Enums;
using BinanceKlineInterval = Binance.Net.Enums.KlineInterval;
using BybitKlineInterval = Bybit.Net.Enums.KlineInterval;
using OkxKlineInterval = OKX.Net.Enums.KlineInterval;

namespace Server.Service
{
    /// <summary>
    /// Класс представляеющий собой преобразование 
    /// интервалов свечей к одному общему типу.
    /// </summary>
    public static class IntervalParser
    {
        private static readonly Dictionary<string, KlineInterval> quikIntervalMapping = new()
        {
            { "M1", KlineInterval.OneMinute },
            { "M5", KlineInterval.FiveMinutes },
            { "M15", KlineInterval.FifteenMinutes },
            { "H1", KlineInterval.OneHour },
            { "H4", KlineInterval.FourHours },
            { "D1", KlineInterval.OneDay }
        };

        private static readonly Dictionary<OkxKlineInterval, KlineInterval> okxIntervalMapping = new()
        {
            { OkxKlineInterval.OneMinute, KlineInterval.OneMinute },
            { OkxKlineInterval.FiveMinutes, KlineInterval.FiveMinutes },
            { OkxKlineInterval.FifteenMinutes, KlineInterval.FifteenMinutes },
            { OkxKlineInterval.OneHour, KlineInterval.OneHour },
            { OkxKlineInterval.FourHours, KlineInterval.FourHours },
            { OkxKlineInterval.OneDay, KlineInterval.OneDay }
        };

        private static readonly Dictionary<BybitKlineInterval, KlineInterval> bybitIntervalMapping = new()
        {
            { BybitKlineInterval.OneMinute, KlineInterval.OneMinute },
            { BybitKlineInterval.FiveMinutes, KlineInterval.FiveMinutes },
            { BybitKlineInterval.FifteenMinutes, KlineInterval.FifteenMinutes },
            { BybitKlineInterval.OneHour, KlineInterval.OneHour },
            { BybitKlineInterval.FourHours, KlineInterval.FourHours },
            { BybitKlineInterval.OneDay, KlineInterval.OneDay }
        };

        private static readonly Dictionary<BinanceKlineInterval, KlineInterval> binanceIntervalMapping = new()
        {
            { BinanceKlineInterval.OneMinute, KlineInterval.OneMinute },
            { BinanceKlineInterval.FiveMinutes, KlineInterval.FiveMinutes },
            { BinanceKlineInterval.FifteenMinutes, KlineInterval.FifteenMinutes },
            { BinanceKlineInterval.OneHour, KlineInterval.OneHour },
            { BinanceKlineInterval.FourHour, KlineInterval.FourHours },
            { BinanceKlineInterval.OneDay, KlineInterval.OneDay }
        };

        public static KlineInterval ParseQuikInterval(string interval)
        {
            return quikIntervalMapping.TryGetValue(interval, out var result) ? result : KlineInterval.None;
        }

        public static KlineInterval ParseOkxInterval(OkxKlineInterval interval)
        {
            return okxIntervalMapping.TryGetValue(interval, out var result) ? result : KlineInterval.None;
        }

        public static KlineInterval ParseBybitInterval(BybitKlineInterval interval)
        {
            return bybitIntervalMapping.TryGetValue(interval, out var result) ? result : KlineInterval.None;
        }

        public static KlineInterval ParseBinanceInterval(BinanceKlineInterval interval)
        {
            return binanceIntervalMapping.TryGetValue(interval, out var result) ? result : KlineInterval.None;
        }

        public static List<OkxKlineInterval> GetOkxIntervals()
        {
            return [.. okxIntervalMapping.Keys];
        }

        public static List<BybitKlineInterval> GetBybitIntervals()
        {
            return [.. bybitIntervalMapping.Keys];
        }
        public static List<BinanceKlineInterval> GetBinanceIntervals()
        {
            return [.. binanceIntervalMapping.Keys];
        }
    }
}
