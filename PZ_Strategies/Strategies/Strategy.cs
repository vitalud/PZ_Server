using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Signal;
using ReactiveUI;
using System.Text.Json.Serialization;

namespace Strategies.Strategies
{
    /// <summary>
    /// Информация о стратегии.
    /// </summary>
    /// <param name="telegram">Информация для телеграм.</param>
    /// <param name="stocks">Информация для биржи.</param>
    /// <param name="name">Имя биржи.</param>
    /// <param name="code">Код стратегии.</param>
    /// <param name="leverage">Плечо.</param>
    public partial class Strategy(Telegram telegram, List<Stock> stocks, BurseName name, string code, int leverage) : ReactiveObject
    {
        private readonly Telegram _telegram = telegram;
        private readonly List<Stock> _stocks = stocks;
        private readonly BurseName _name = name;
        private SignalData _signal = new("init");
        private decimal _clientLimit;

        [JsonIgnore]
        public Telegram Telegram => _telegram;
        public List<Stock> Stocks => _stocks;
        public BurseName Name => _name;

        [JsonIgnore]
        public SignalData Signal
        {
            get => _signal;
            set => this.RaiseAndSetIfChanged(ref _signal, value);
        }
        public string Code { get; set; } = code;
        public int Leverage { get; set; } = leverage;
        public decimal ClientLimit
        {
            get => _clientLimit;
            set => this.RaiseAndSetIfChanged(ref _clientLimit, value);
        }
    }
}
