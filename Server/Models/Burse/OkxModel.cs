using CryptoExchange.Net.Objects.Sockets;
using OKX.Net.Clients;
using OKX.Net.Enums;
using OKX.Net.Objects;
using OKX.Net.Objects.Market;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using Server.Service.Abstract;
using System.Reactive.Linq;
using CustomInterval = ProjectZeroLib.Enums.KlineInterval;
using KlineInterval = OKX.Net.Enums.KlineInterval;

namespace Server.Models.Burse
{
    public class OkxModel : BurseModel
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

        public OkxModel(InstrumentService instrumentService, BurseName name) : base(instrumentService, name)
        {
            _keys = [KeyEncryptor.ReadKeyFromFile("okxapi"), 
                     KeyEncryptor.ReadKeyFromFile("okxsecret"), 
                     KeyEncryptor.ReadKeyFromFile("okxword")];
        }

        protected override void SetupClientsAsync()
        {
            _socket = new OKXSocketClient(options =>
            {
                options.ApiCredentials = new OKXApiCredentials(_keys[0], _keys[1], _keys[2]);
                options.OutputOriginalData = true;
            });
            _rest = new OKXRestClient(options =>
            {
                options.ApiCredentials = new OKXApiCredentials(_keys[0], _keys[1], _keys[2]);
                options.OutputOriginalData = true;
            });
        }
        protected override async Task GetSubscriptions()
        {
            foreach (var inst in Instruments.Items)
                await Subscribe(inst);
            await PrepareIndicators();
        }
        protected override async Task Subscribe(Instrument instrument)
        {
            try
            {
                var name = instrument.Name;
                if (_socket is OKXSocketClient socket)
                {
                    foreach (var period in intervalMapping.Keys)
                    {
                        var klineSub = await socket.UnifiedApi.ExchangeData.SubscribeToKlineUpdatesAsync(name.Id, period, (data) => KlineDataHandler(data, period));
                        if (klineSub.Success)
                        {
                            if (name.Type.Equals("Futures"))
                                subToUpdateIds.Add(klineSub.Data.Id);
                        }
                    }
                    var tradeSub = await socket.UnifiedApi.ExchangeData.SubscribeToTradeUpdatesAsync(name.Id, TradeDataHandler);
                    if (tradeSub.Success)
                    {
                        if (name.Type.Equals("Futures"))
                            subToUpdateIds.Add(tradeSub.Data.Id);
                        instrument.IsActive = true;
                    }
                }
            }
            catch
            {
                Disconnect();
            }
        }
        private async Task PrepareIndicators()
        {
            if (_rest is OKXRestClient rest)
            {
                var last = await rest.UnifiedApi.ExchangeData.GetKlineHistoryAsync("BTC-USDT", KlineInterval.OneMinute, limit: 60);
                if (last.Success)
                {
                    var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals("BTC-USDT")
                    && x.Name.Type.Equals("Spot")
                    && x.Burse.Equals(BurseName.Okx));
                    if (stock != null)
                    {
                        foreach (var kline in last.Data) stock.Other.Last60Close.Add(kline.ClosePrice);
                    }
                }
            }
        }
        private async void KlineDataHandler(DataEvent<OKXKline> data, KlineInterval period)
        {
            if (data != null)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(data.Data.Symbol));
                if (stock != null)
                {
                    var kline = stock.Klines.Find(x => x.Interval.Equals(ParseOkxInterval(period)));
                    if (kline != null)
                    {
                        stock.LastUpdate = data.Timestamp;
                        var time = data.Data.Time.Hour * 10000 + data.Data.Time.Minute * 100;
                        if (!kline.Time.Equals(time))
                        {
                            if (period.Equals(KlineInterval.OneMinute))
                                await GetOrderBook(stock);
                            kline.Time = data.Data.Time.Hour * 10000 + data.Data.Time.Minute * 100;
                            kline.Day = data.Data.Time.Year * 10000 + data.Data.Time.Month * 100 + data.Data.Time.Day;
                        }
                        kline.Open = data.Data.OpenPrice;
                        kline.High = data.Data.HighPrice;
                        kline.Low = data.Data.LowPrice;
                        kline.Close = data.Data.ClosePrice;
                    }
                }
            }
        }
        private async Task GetOrderBook(Instrument stock)
        {
            if (_rest is OKXRestClient rest)
            {
                var orderBook = await rest.UnifiedApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 400);
                if (orderBook.Data != null)
                {
                    var asks = orderBook.Data.Asks;
                    var bids = orderBook.Data.Bids;

                    var asks20 = asks.Take(20);
                    foreach (var ask in asks20) stock.Orders.BestAsks += ask.Quantity;
                    var bids20 = bids.Take(20);
                    foreach (var bid in bids20) stock.Orders.BestBids += bid.Quantity;

                    foreach (var ask in asks)
                    {
                        stock.Orders.AllAsks += ask.Quantity;
                        stock.Orders.NumAsks += ask.OrdersCount;
                    }

                    foreach (var bid in bids)
                    {
                        stock.Orders.AllBids += bid.Quantity;
                        stock.Orders.NumBids += bid.OrdersCount;
                    }
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
        private void TradeDataHandler(DataEvent<OKXTrade> data)
        {
            if (data != null)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id == data.Data.Symbol);
                if (stock != null)
                {
                    if (data.Data.Side == OrderSide.Buy) { stock.Trades.Buy += data.Data.Quantity; }
                    else if (data.Data.Side == OrderSide.Sell) { stock.Trades.Sell += data.Data.Quantity; }
                }
            }
        }
        private static CustomInterval ParseOkxInterval(KlineInterval interval)
        {
            return intervalMapping.TryGetValue(interval, out var result) ? result : CustomInterval.None;
        }
    }
}
