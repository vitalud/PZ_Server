namespace Strategies.Instruments.Burse
{
    /// <summary>
    /// Используемые инструменты биржи Quik.
    /// </summary>
    public class QuikInstruments
    {
        public static readonly Dictionary<string, List<Name>> Instruments = new()
        {
            { "SPBFUT", new List<Name>
                {
                    new("Si"),
                    new("CR"),
                    new("Eu"),
                    new("ED"),
                    new("NG"),
                    new("GD"),
                    new("BR"),
                    new("SV"),
                    new("SF"),
                    new("RI"),
                    new("MX"),
                    new("RM"),
                    new("MM"),
                    new("SR"),
                    new("GZ"),
                    new("LK"),
                    new("MN"),
                    new("RN")
                }
            },
            { "TQBR", new List<Name>
                {
                    new("SBER"),
                    new("MTLR"),
                    new("LKOH"),
                    new("SNGSP"),
                    new("GAZP"),
                    new("GMKN"),
                    new("VKCO"),
                    new("MOEX"),
                    new("MGNT"),
                    new("ROSN"),
                    new("VTBR"),
                    new("SNGS"),
                    new("ALRS"),
                    new("TATN"),
                    new("SGZH"),
                    new("TRNFP"),
                    new("MTSS")
                }
            },
            { "CETS", new List<Name>
                {
                    new("CNYRUB_TOM")
                }
            }
        };
    }
}
