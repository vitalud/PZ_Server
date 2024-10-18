using DynamicData;
using Newtonsoft.Json;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using Server.Service.Abstract;
using System.Net;
using System.Net.Sockets;
using CustomInterval = ProjectZeroLib.Enums.KlineInterval;

namespace Server.Models
{
    public class QuikModel : BurseModel
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

        private readonly TcpListener _listener = new(IPAddress.Parse("127.0.0.1"), 1020);
        public QuikModel(InstrumentService instrumentService) : base(instrumentService)
        {
            Name = BurseName.Quik;
            GeneratedSignals = [];
        }

        protected override async void SetupClientsAsync()
        {
            _listener.Start();
            var quik = await _listener.AcceptTcpClientAsync();
            _ = HandleQuikClientAsync(quik);
        }

        private async Task HandleQuikClientAsync(TcpClient quik)
        {
            try
            {
                var networkStream = quik.GetStream();
                Thread QuikDataExchange = new(async () =>
                {
                    var receivedMessage = await TcpService.ReadMessageAsync(networkStream);
                    if (receivedMessage != null)
                    {
                        await GetQuikData(receivedMessage);
                    }
                })
                {
                    IsBackground = true
                };
                QuikDataExchange.Start();
            }
            finally
            {

            }
        }

        private async Task GetQuikData(string data)
        {
            var temp = JsonConvert.DeserializeObject<QuikIndicators>(data);
            if (temp != null)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(temp.SecCode) & x.Name.Type.Equals(temp.ClassCode));
                if (stock == null) Instruments.Add(new Instrument(BurseName.Quik, new InstrumentName(temp.SecCode)
                {
                    Type = temp.ClassCode,
                }));

                stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(temp.SecCode) & x.Name.Type.Equals(temp.ClassCode));

                var period = ParseQuikInterval(temp.Interval);
                var kline = stock.Klines.Find(x => x.Interval.Equals(period));
                if (kline != null)
                {
                    lock (stock)
                    {
                        kline.Day = temp.Day;
                        kline.Time = temp.Time;
                        kline.Open = temp.Open;
                        kline.High = temp.High;
                        kline.Low = temp.Low;
                        kline.Close = temp.Close;

                        stock.Orders.AllAsks = temp.AllAsks;
                        stock.Orders.AllBids = temp.AllBids;
                        stock.Orders.NumAsks = temp.NumAsks;
                        stock.Orders.NumBids = temp.NumBids;
                        stock.Orders.BestAsks = temp.BestAsks;
                        stock.Orders.BestBids = temp.BestBids;
                        stock.Trades.Buy = temp.TradesBuy;
                        stock.Trades.Sell = temp.TradesSell;
                        stock.Trades.Volume = temp.Volume;

                        kline.Day = temp.Day;
                        kline.Time = temp.Time;
                    }
                }
            }
        }
        private static CustomInterval ParseQuikInterval(string interval)
        {
            return intervalMapping.TryGetValue(interval, out var result) ? result : CustomInterval.None;
        }
        protected override Task GetSubscriptions()
        {
            return Task.CompletedTask;
        }
        protected override Task Subscribe(Instrument instrument)
        {
            return Task.CompletedTask;
        }
    }

}
