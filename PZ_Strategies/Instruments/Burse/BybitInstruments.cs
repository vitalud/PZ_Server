namespace Strategies.Instruments.Burse
{
    /// <summary>
    /// Используемые инструменты биржи Bybit.
    /// </summary>
    public class BybitInstruments
    {
        public static readonly Dictionary<string, List<Name>> Instruments = new()
        {
            { "Spot", new List<Name>
                {
                    new("BTCUSDT"),
                    new("ETHUSDT"),
                    new("BNBUSDT"),
                    new("XRPUSDT"),
                    new("LTCUSDT"),
                    new("ARBUSDT"),
                    new("SUIUSDT"),
                    new("SOLUSDT"),
                }
            },
            { "Futures", new List<Name>
                {
                    new("BTCUSDT"),
                    new("ETHUSDT"),
                }
            },
            { "InverseFutures", new List<Name>
                {
                    new("BTCUSD"),
                    new("ETHUSD"),
                }
            }
        };
    }
}
