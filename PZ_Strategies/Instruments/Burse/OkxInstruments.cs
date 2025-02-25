namespace Strategies.Instruments.Burse
{
    /// <summary>
    /// Используемые инструменты биржи Okx.
    /// </summary>
    public class OkxInstruments
    {
        public static readonly Dictionary<string, List<Name>> Instruments = new()
        {
            { "Spot", new List<Name>
                {
                    new("BTC-USDT"),
                    new("ETH-USDT"),
                    new("BNB-USDT"),
                    new("XRP-USDT"),
                    new("FLM-USDT"),
                    new("LTC-USDT"),
                    new("ARB-USDT"),
                    new("SUI-USDT"),
                    new("SOL-USDT")
                }
            },
            { "Futures", new List<Name>
                {
                    new("BTC-USDT-"),
                    new("ETH-USDT-"),
                }
            },
            { "Swap", new List<Name>
                {
                    new("BTC-USDT-SWAP"),
                    new("ETH-USDT-SWAP"),
                    new("BNB-USDT-SWAP"),
                    new("XRP-USDT-SWAP"),
                    new("FLM-USDT-SWAP"),
                    new("LTC-USDT-SWAP"),
                    new("ARB-USDT-SWAP"),
                    new("SUI-USDT-SWAP"),
                    new("SOL-USDT-SWAP")
                }
            }
        };
    }
}
