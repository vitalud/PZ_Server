using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using Server.Service.Enums;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Reactive.Linq;

namespace Server.Service
{
    public class Client : ReactiveObject
    {
        private NetworkStream _stream;
        public NetworkStream Stream
        {
            get => _stream;
            set => this.RaiseAndSetIfChanged(ref _stream, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }
        public string SessionId { get; set; } = "default";

        private Data _data;
        public Data Data
        {
            get => _data;
            set => this.RaiseAndSetIfChanged(ref _data, value);
        }

        private Telegram _telegram;
        public Telegram Telegram
        {
            get => _telegram;
            set => this.RaiseAndSetIfChanged(ref _telegram, value);
        }

        public Client()
        {
            this.WhenAnyValue(x => x.Stream)
                .Skip(1)
                .Subscribe(OnStreamChanged);
        }

        private void OnStreamChanged(NetworkStream stream)
        {
            
        }
    }
    public class Data : ReactiveObject
    {
        private readonly SourceList<ShortStrategyInfo> _strategies = new();
        public SourceList<ShortStrategyInfo> Strategies => _strategies;
        private ReadOnlyObservableCollection<ShortStrategyInfo> _items;
        public ReadOnlyObservableCollection<ShortStrategyInfo> Items => _items;
        public string Login { get; set; } = "default";
        public string Password { get; set; } = "default";
        public double Percentage { get; set; } = 0.17;

        private int _deposit = 0;
        public int Deposit
        {
            get => _deposit;
            set => this.RaiseAndSetIfChanged(ref _deposit, value);
        }

        private int _payment = 0;
        public int Payment
        {
            get => _payment;
            set => this.RaiseAndSetIfChanged(ref _payment, value);
        }

        public Data(string login)
        {
            Login = login;
            Strategies.Connect()
                      .Bind(out _items)
                      .Subscribe();
        }
    }
    public class Telegram(string id) : ReactiveObject
    {
        public string Id { get; set; } = id;

        private State _state;
        public State State
        {
            get => _state;
            set => this.RaiseAndSetIfChanged(ref _state, value);
        }
        private Stage _stage;
        public Stage Stage
        {
            get => _stage;
            set => this.RaiseAndSetIfChanged(ref _stage, value);
        }

        private Temp _temp = new();
        public Temp Temp
        {
            get => _temp;
            set => this.RaiseAndSetIfChanged(ref _temp, value);
        }

        public int Index { get; set; }
        public int Lenght { get; set; }
    }
    public class Temp : ReactiveObject
    {
        public List<Strategy> Strategies { get; set; } = [];
        public string Burse { get; set; }
        public string Code { get; set; }
        public string PhotoId { get; set; }
        public int Limit { get; set; }
        public int Price { get; set; }
        public int Deposit { get; set; }
    }
    public class ShortStrategyInfo(string burse, string code, int tradeLimit, int payment) : ReactiveObject
    {
        public string Burse { get; set; } = burse;
        public string Code { get; set; } = code;
        public int TradeLimit { get; set; } = tradeLimit;
        public bool ActivatedByClient { get; set; } = false;
        public int Payment { get; set; } = payment;
    }
}
