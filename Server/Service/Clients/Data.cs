using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace Server.Service.UserClient
{
    /// <summary>
    /// Основные данные клиента.
    /// </summary>
    public partial class Data : ReactiveObject
    {
        private int _deposit = 0;
        private int _payment = 0;
        private double _percentage = 0.17;

        private readonly SourceList<StrategySummary> _strategies = new();
        private readonly ReadOnlyObservableCollection<StrategySummary> _items;

        public SourceList<StrategySummary> Strategies => _strategies;
        public ReadOnlyObservableCollection<StrategySummary> Items => _items;
        public string Login { get; } = "default";
        public string Password { get; set; } = "default";
        public int Deposit
        {
            get => _deposit;
            set => this.RaiseAndSetIfChanged(ref _deposit, value);
        }
        public int Payment
        {
            get => _payment;
            set => this.RaiseAndSetIfChanged(ref _payment, value);
        }
        public double Percentage
        {
            get => _percentage;
            set => this.RaiseAndSetIfChanged(ref _percentage, value);
        }

        public Data(string login)
        {
            Login = login;

            Strategies.Connect()
                      .Bind(out _items)
                      .Subscribe();
        }
    }
}
