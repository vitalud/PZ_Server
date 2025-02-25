namespace Strategies.Instruments.Burse
{
    /// <summary>
    /// Используемые инструменты биржи Binance.
    /// </summary>
    public class BinanceInstruments
    {
        public static readonly Dictionary<string, List<Name>> Instruments = new()
        {
            { "Spot", new List<Name>
                {
                    new("BTCUSDT"),
                    new("ETHUSDT"),
                    new("BNBUSDT"),
                    new("XRPUSDT"),
                    new("FLMUSDT"),
                    new("LTCUSDT"),
                    new("ARBUSDT"),
                    new("SUIUSDT"),
                }
            },
            { "UsdFutures", new List<Name>
                {
                    new("BTCUSDT"),
                    new("BTCUSDT_"),
                    new("ETHUSDT"),
                    new("ETHUSDT_"),
                    new("BNBUSDT"),
                    new("XRPUSDT"),
                    new("FLMUSDT"),
                    new("LTCUSDT"),
                    new("ARBUSDT"),
                    new("SUIUSDT"),
                }
            },
            { "CoinFutures", new List<Name>
                {
                    new("BTCUSD_"),
                    new("BTCUSD_PERP"),
                    new("ETHUSD_"),
                    new("ETHUSD_PERP"),
                    new("BNBUSD_"),
                    new("BNBUSD_PERP"),
                    new("XRPUSD_"),
                    new("XRPUSD_PERP"),
                    new("LTCUSD_"),
                    new("LTCUSD_PERP"),
                }
            }
        };
    }
}
