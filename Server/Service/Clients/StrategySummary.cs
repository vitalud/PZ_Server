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
        private bool _isActive = false;
        public string Burse { get; } = burse;
        public string Code { get; } = code;
        public int TradeLimit { get; set; } = tradeLimit;
        public bool ActivatedByClient
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }
        public int Payment { get; set; } = payment;
    }
}
