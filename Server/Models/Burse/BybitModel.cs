using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Objects.Models.V5;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using Server.Service.Abstract;
using System.Reactive.Linq;
using CustomInterval = ProjectZeroLib.Enums.KlineInterval;
using KlineInterval = Bybit.Net.Enums.KlineInterval;

namespace Server.Models.Burse
{
    public class BybitModel : BurseModel
    {
        private static readonly Dictionary<KlineInterval, CustomInterval> intervalMapping = new()
        {
            { KlineInterval.OneMinute, CustomInterval.OneMinute },
            { KlineInterval.FiveMinutes, CustomInterval.FiveMinutes },
            { KlineInterval.FifteenMinutes, CustomInterval.FifteenMinutes },
            { KlineInterval.OneHour, CustomInterval.OneHour },
            { KlineInterval.FourHours, CustomInterval.FourHours },
            { KlineInterval.OneDay, CustomInterval.OneDay }
        };

        public BybitModel(InstrumentService instrumentService, BurseName name) : base(instrumentService, name)
        {
            _keys = [KeyEncryptor.ReadKeyFromFile("bybitapi", "projectzero.txt"), 
                     KeyEncryptor.ReadKeyFromFile("bybitsecret", "projectzero.txt")];
        }

        protected override void SetupClientsAsync()
        {
            _socket = new BybitSocketClient(options =>
            {
                options.ApiCredentials = new(_keys[0], _keys[1]);
                options.OutputOriginalData = true;
                options.ReconnectInterval = TimeSpan.FromSeconds(5);
            });
            _rest = new BybitRestClient(options =>
            {
                options.ApiCredentials = new(_keys[0], _keys[1]);
                options.OutputOriginalData = true;
            });
        }
        protected override async Task GetSubscriptions()
        {
            foreach (var inst in Instruments.Items)
                await Subscribe(inst);
        }
        protected override async Task Subscribe(Instrument instrument)
        {
            try
            {
                if (_socket is BybitSocketClient socket)
                {
                    foreach (var period in intervalMapping.Keys)
                    {
                        CallResult<UpdateSubscription> klineSub;
                        if (instrument.Name.Type.Equals("Spot"))
                        {
                            klineSub = await socket.V5SpotApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, (data) => KlineDataHandler(data, period, "Spot"));
                        }
                        else if (instrument.Name.Type.Equals("Futures"))
                        {
                            klineSub = await socket.V5LinearApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, (data) => KlineDataHandler(data, period, "Futures"));
                        }
                        else if (instrument.Name.Type.Equals("InverseFutures"))
                        {
                            klineSub = await socket.V5InverseApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, (data) => KlineDataHandler(data, period, "InverseFutures"));
                        }
                    }
                    CallResult<UpdateSubscription> tradeSub;
                    if (instrument.Name.Type.Equals("Spot"))
                    {
                        tradeSub = await socket.V5SpotApi.SubscribeToTradeUpdatesAsync(instrument.Name.Id, (data) => TradeDataHandler(data, "Spot"));
                        if (tradeSub.Success)
                            instrument.IsActive = true;
                    }
                    else if (instrument.Name.Type.Equals("Futures"))
                    {
                        tradeSub = await socket.V5LinearApi.SubscribeToTradeUpdatesAsync(instrument.Name.Id, (data) => TradeDataHandler(data, "Futures"));
                        if (tradeSub.Success)
                            instrument.IsActive = true;
                    }
                    else if (instrument.Name.Type.Equals("InverseFutures"))
                    {
                        tradeSub = await socket.V5InverseApi.SubscribeToTradeUpdatesAsync(instrument.Name.Id, (data) => TradeDataHandler(data, "InverseFutures"));
                        if (tradeSub.Success)
                            instrument.IsActive = true;
                    }
                }
            }
            catch
            {
                Disconnect();
            }
        }
        private async void KlineDataHandler(DataEvent<IEnumerable<BybitKlineUpdate>> data, KlineInterval period, string type)
        {
            if (data != null)
            {
                foreach (var dat in data.Data)
                {
                    var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(data.Symbol) && x.Name.Type.Equals(type));
                    if (stock != null)
                    {
                        var kline = stock.Klines.Find(x => x.Interval.Equals(ParseBybitInterval(period)));
                        if (kline != null)
                        {
                            stock.LastUpdate = data.Timestamp;
                            var time = dat.StartTime.Hour * 10000 + dat.StartTime.Minute * 100;
                            if (!kline.Time.Equals(time))
                            {
                                if (period.Equals(KlineInterval.OneMinute))
                                    await GetOrderBook(stock);
                                kline.Day = dat.StartTime.Year * 10000 + dat.StartTime.Month * 100 + dat.StartTime.Day;
                                kline.Time = dat.StartTime.Hour * 10000 + dat.StartTime.Minute * 100;
                            }
                            kline.Open = dat.OpenPrice;
                            kline.High = dat.HighPrice;
                            kline.Low = dat.LowPrice;
                            kline.Close = dat.ClosePrice;
                        }
                    }
                }
            }
        }
        private async Task GetOrderBook(Instrument stock)
        {
            BybitOrderbook orderBook = null;
            if (_rest is BybitRestClient rest)
            {
                if (stock.Name.Type.Equals("Spot"))
                {
                    var result = await rest.V5Api.ExchangeData.GetOrderbookAsync(Category.Spot, stock.Name.Id, 500);
                    orderBook = result.Data;
                }
                else if (stock.Name.Type.Equals("Futures"))
                {
                    var result = await rest.V5Api.ExchangeData.GetOrderbookAsync(Category.Linear, stock.Name.Id, 500);
                    orderBook = result.Data;
                }
                else if (stock.Name.Type.Equals("InverseFutures"))
                {
                    var result = await rest.V5Api.ExchangeData.GetOrderbookAsync(Category.Inverse, stock.Name.Id, 500);
                    orderBook = result.Data;
                }

                if (orderBook != null)
                {
                    var asks = orderBook.Asks;
                    var bids = orderBook.Bids;

                    var asks20 = asks.Take(20);
                    foreach (var ask in asks20) stock.Orders.BestAsks += ask.Quantity;
                    var bids20 = bids.Take(20);
                    foreach (var bid in bids20) stock.Orders.BestBids += bid.Quantity;

                    foreach (var ask in asks) stock.Orders.AllAsks += ask.Quantity;
                    foreach (var bid in bids) stock.Orders.AllBids += bid.Quantity;

                    stock.Orders.NumAsks = 0;
                    stock.Orders.NumBids = 0;
                }
                else
                {
                    stock.Orders.AllAsks = 0;
                    stock.Orders.AllBids = 0;
                    stock.Orders.NumAsks = 0;
                    stock.Orders.NumBids = 0;
                    stock.Orders.BestAsks = 0;
                    stock.Orders.BestBids = 0;
                }
            }
        }
        private void TradeDataHandler(DataEvent<IEnumerable<BybitTrade>> data, string type)
        {
            if (data != null)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id == data.Symbol && x.Name.Type.Equals(type));
                if (stock != null)
                {
                    foreach (var trade in data.Data)
                    {
                        if (trade.Side == OrderSide.Sell) { stock.Trades.Sell += trade.Quantity; }
                        else { stock.Trades.Buy += trade.Quantity; }
                    }
                }
            }
        }
        private static CustomInterval ParseBybitInterval(KlineInterval interval)
        {
            return intervalMapping.TryGetValue(interval, out var result) ? result : CustomInterval.None;
        }
    }
}
