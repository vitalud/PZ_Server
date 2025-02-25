using ReactiveUI;

namespace Server.Service.UserClient
{
    /// <summary>
    /// Краткие сведения о стратегии.
    /// </summary>
    /// <param name="burse"></param>
    /// <param name="code"></param>
    /// <param name="tradeLimit"></param>
    /// <param name="payment"></param>
    public partial class StrategySummary(string burse, string code, int tradeLimit, int payment) : ReactiveObject
    {
        public string Burse { get; } = burse;
        public string Code { get; } = code;
        public int TradeLimit { get; set; } = tradeLimit;
        public bool ActivatedByClient { get; set; } = false;
        public int Payment { get; set; } = payment;
    }
}
