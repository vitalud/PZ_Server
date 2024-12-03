using DynamicData;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using ReactiveUI;
using Server.Service.Bot;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text.Json;
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

        private readonly TelegramBot _telegram;
        private readonly InstrumentService _instrumentService;

        private readonly TcpListener _listener = new(IPAddress.Parse("127.0.0.1"), 1021);
        private TcpClient? _quik;

        private readonly IObservableList<Instrument> _instruments;
        public IObservableList<Instrument> Instruments => _instruments;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }


        public QuikModel(InstrumentService instrumentService, TelegramBot telegram)
        {
            _instrumentService = instrumentService;
            _telegram = telegram;

            _instruments = _instrumentService.QuikInstruments.Connect()
                .AsObservableList();

            Start();
        }

        private void Start()
        {
            _listener.Start();
            Task.Run(GetQuikAsync);
        }
        private async Task GetQuikAsync()
        {
            while (true)
            {
                if (!_isConnected)
                {
                    _quik = await _listener.AcceptTcpClientAsync();
                    IsConnected = true;
                    _ = HandleQuikClientAsync();
                }
            }
        }
        private async Task HandleQuikClientAsync()
        {
            if (_quik != null)
            {
                using var stream = _quik.GetStream();
                try
                {
                    while (true)
                    {
                        var message = await TcpService.ReadMessageAsync(stream);
                        if (message != null)
                        {
                            await GetQuikData(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _telegram.SendQuikErrorMessage();
                }
                finally
                {
                    _quik.Dispose();
                    await Logger.UiInvoke(() => _instrumentService.QuikInstruments.Clear());
                    IsConnected = false;
                }
            }
        }
        private async Task GetQuikData(string data)
        {
            var indicators = JsonSerializer.Deserialize<QuikIndicators>(data);
            if (indicators != null)
            {
                var stock = _instrumentService.QuikInstruments.Items.FirstOrDefault(x => x.Name.Id.Equals(indicators.SecCode) & x.Name.Type.Equals(indicators.ClassCode));
                if (stock == null) 
                {
                    var instrument = new Instrument(BurseName.Quik, new InstrumentName(indicators.SecCode))
                    {
                        IsActive = true,
                        Name = 
                        { 
                            Type = indicators.ClassCode 
                        }
                    };
                    await Logger.UiInvoke(() => _instrumentService.QuikInstruments.Add(instrument));
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
                            stock.LastUpdate = DateTime.Now;

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

                            stock.SignalData.Complete = true;
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
