using DynamicData;
using Newtonsoft.Json;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using CustomInterval = ProjectZeroLib.Enums.KlineInterval;

namespace Server.Models
{
    public class QuikModel : ReactiveObject
    {
        private static readonly Dictionary<string, CustomInterval> intervalMapping = new()
        {
            { "M1", CustomInterval.OneMinute },
            { "M5", CustomInterval.FiveMinutes },
            { "M15", CustomInterval.FifteenMinutes },
            { "H1", CustomInterval.OneHour },
            { "H4", CustomInterval.FourHours },
            { "D1", CustomInterval.OneDay }
        };

        private readonly InstrumentService _instrumentService;

        private readonly TcpListener _listener = new(IPAddress.Parse("127.0.0.1"), 1020);
        private TcpClient _quik;

        private readonly IObservableList<Instrument> _instruments;
        public IObservableList<Instrument> Instruments => _instruments;

        private string _status;
        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public QuikModel(InstrumentService instrumentService)
        {
            _instrumentService = instrumentService;

            _instruments = _instrumentService.Instruments.Connect()
                .Filter(x => x.Name.Equals(BurseName.Quik))
                .AsObservableList();

            GetQuikClient();
        }

        private async void GetQuikClient()
        {
            _listener.Start();
            Status = "Ожидание подключения от Quik";
            _quik = await _listener.AcceptTcpClientAsync();
            _ = HandleQuikClientAsync();
        }
        private async Task HandleQuikClientAsync()
        {
            using var stream = _quik.GetStream();
            Status = "Подключен";
            while (true)
            {
                var message = await TcpService.ReadMessageAsync(stream);
                if (message != null)
                {
                    await GetQuikData(message);
                }
            }
        }
        private async Task GetQuikData(string data)
        {
            var indicators = JsonConvert.DeserializeObject<QuikIndicators>(data);
            if (indicators != null)
            {
                var stock = _instrumentService.Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(indicators.SecCode) & x.Name.Type.Equals(indicators.ClassCode));
                if (stock == null) 
                {
                    var instrument = new Instrument(BurseName.Quik, new InstrumentName(indicators.SecCode))
                    {
                        Name = 
                        { 
                            Type = indicators.ClassCode 
                        }
                    };
                    _instrumentService.Instruments.Add(instrument);
                    stock = instrument;
                }

                if (stock != null)
                {
                    var period = ParseQuikInterval(indicators.Interval);
                    var kline = stock.Klines.Find(x => x.Interval.Equals(period));
                    if (kline != null)
                    {
                        lock (stock)
                        {
                            kline.Day = indicators.Day;
                            kline.Time = indicators.Time;
                            kline.Open = indicators.Open;
                            kline.High = indicators.High;
                            kline.Low = indicators.Low;
                            kline.Close = indicators.Close;

                            stock.Orders.AllAsks = indicators.AllAsks;
                            stock.Orders.AllBids = indicators.AllBids;
                            stock.Orders.NumAsks = indicators.NumAsks;
                            stock.Orders.NumBids = indicators.NumBids;
                            stock.Orders.BestAsks = indicators.BestAsks;
                            stock.Orders.BestBids = indicators.BestBids;
                            stock.Trades.Buy = indicators.TradesBuy;
                            stock.Trades.Sell = indicators.TradesSell;
                            stock.Trades.Volume = indicators.Volume;

                            kline.Day = indicators.Day;
                            kline.Time = indicators.Time;
                        }
                    }
                }
            }
        }
        private static CustomInterval ParseQuikInterval(string interval)
        {
            return intervalMapping.TryGetValue(interval, out var result) ? result : CustomInterval.None;
        }
    }

}
