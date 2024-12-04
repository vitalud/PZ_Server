using Binance.Net.Clients;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Objects.Models.Spot.Socket;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using ProjectZeroLib;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Instruments;
using Server.Service.Abstract;
using System.Reactive.Linq;
using CustomInterval = ProjectZeroLib.Enums.KlineInterval;
using KlineInterval = Binance.Net.Enums.KlineInterval;

namespace Server.Models.Burse
{
    public class BinanceModel : BurseModel
    {
        private static readonly Dictionary<KlineInterval, CustomInterval> intervalMapping = new()
        {
            { KlineInterval.OneMinute, CustomInterval.OneMinute },
            { KlineInterval.FiveMinutes, CustomInterval.FiveMinutes },
            { KlineInterval.FifteenMinutes, CustomInterval.FifteenMinutes },
            { KlineInterval.OneHour, CustomInterval.OneHour },
            { KlineInterval.FourHour, CustomInterval.FourHours },
            { KlineInterval.OneDay, CustomInterval.OneDay }
        };

        public BinanceModel(InstrumentService instrumentService, BurseName name) : base(instrumentService, name)
        {
            _keys = [KeyEncryptor.ReadKeyFromFile("binanceapi", "projectzero.txt"), 
                     KeyEncryptor.ReadKeyFromFile("binancesecret", "projectzero.txt")];
        }

        protected override void SetupClientsAsync()
        {
            _socket = new BinanceSocketClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(_keys[0], _keys[1]);
                options.OutputOriginalData = true;
            });
            _rest = new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(_keys[0], _keys[1]);
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
                if (_socket is BinanceSocketClient socket)
                {
                    foreach (var period in intervalMapping.Keys)
                    {
                        CallResult<UpdateSubscription> klineSub;
                        if (instrument.Name.Type.Equals("Spot"))
                        {
                            klineSub = await socket.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, (data) => KlineDataHandler(data, period, "Spot"));
                        }
                        else if (instrument.Name.Type.Equals("UsdFutures"))
                        {
                            klineSub = await socket.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, (data) => KlineDataHandler(data, period, "UsdFutures"));
                            if (klineSub.Success)
                            {
                                if (instrument.Name.Id.Contains('_')) subToUpdateIds.Add(klineSub.Data.Id);
                            }
                        }
                        else if (instrument.Name.Type.Equals("CoinFutures"))
                        {
                            klineSub = await socket.CoinFuturesApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, (data) => KlineDataHandler(data, period, "CoinFutures"));
                            if (klineSub.Success)
                            {
                                if (instrument.Name.Id.Contains('_')) subToUpdateIds.Add(klineSub.Data.Id);
                            }
                        }
                    }
                    CallResult<UpdateSubscription> tradeSub;
                    if (instrument.Name.Type.Equals("Spot"))
                    {
                        tradeSub = await socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(instrument.Name.Id, (data) => TradeDataHandler(data, "Spot"));
                        if (tradeSub.Success)
                            instrument.IsActive = true;
                    }
                    else if (instrument.Name.Type.Equals("UsdFutures"))
                    {
                        tradeSub = await socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(instrument.Name.Id, (data) => TradeDataHandler(data, "UsdFutures"));
                        if (tradeSub.Success)
                            instrument.IsActive = true;
                    }
                    else if (instrument.Name.Type.Equals("CoinFutures"))
                    {
                        tradeSub = await socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(instrument.Name.Id, (data) => TradeDataHandler(data, "CoinFutures"));
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
        private async Task PrepareIndicators()
        {
            if (_rest is BinanceRestClient rest)
            {
                var last = await rest.SpotApi.ExchangeData.GetKlinesAsync("BTCUSDT", KlineInterval.OneMinute, limit: 60);
                if (last.Success)
                {
                    var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals("BTCUSDT")
                    && x.Name.Type.Equals("Spot")
                    && x.Burse.Equals(BurseName.Binance));
                    if (stock != null)
                    {
                        foreach (var kline in last.Data) stock.Other.Last60Close.Add(kline.ClosePrice);
                    }
                }
            }
        }
        private async void KlineDataHandler(DataEvent<IBinanceStreamKlineData> data, KlineInterval period, string type)
        {
            if (data != null)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(data.Symbol) && x.Name.Type.Equals(type));
                if (stock != null)
                {
                    var kline = stock.Klines.Find(x => x.Interval.Equals(ParseBinanceInterval(period)));
                    if (kline != null)
                    {
                        stock.LastUpdate = data.Timestamp;
                        if (data.Data.Data.Final)
                        {
                            if (period.Equals(KlineInterval.OneMinute))
                                await GetOrderBook(stock);
                            kline.Day = data.Data.Data.OpenTime.Year * 10000 + data.Data.Data.OpenTime.Month * 100 + data.Data.Data.OpenTime.Day;
                            kline.Time = data.Data.Data.OpenTime.Hour * 10000 + data.Data.Data.OpenTime.Minute * 100;
                        }
                        kline.Open = data.Data.Data.OpenPrice;
                        kline.High = data.Data.Data.HighPrice;
                        kline.Low = data.Data.Data.LowPrice;
                        kline.Close = data.Data.Data.ClosePrice;
                    }
                }
            }
        }
        private async Task GetOrderBook(Instrument stock)
        {
            BinanceOrderBook? orderBook = null;
            if (_rest is BinanceRestClient rest)
            {
                if (stock.Name.Type.Equals("Spot"))
                {
                    var result = await rest.SpotApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 500);
                    orderBook = result.Data;
                }
                else if (stock.Name.Type.Equals("UsdFutures"))
                {
                    var result = await rest.UsdFuturesApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 500);
                    orderBook = result.Data;
                }
                else if (stock.Name.Type.Equals("CoinFutures"))
                {
                    var result = await rest.CoinFuturesApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 500);
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
        private void TradeDataHandler(DataEvent<BinanceStreamTrade> data, string type)
        {
            if (data != null)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id == data.Symbol && x.Name.Type.Equals(type));
                if (stock != null)
                {
                    if (data.Data.BuyerIsMaker) { stock.Trades.Sell += data.Data.Quantity; }
                    else stock.Trades.Buy += data.Data.Quantity;
                }
            }
        }
        private static CustomInterval ParseBinanceInterval(KlineInterval interval)
        {
            return intervalMapping.TryGetValue(interval, out var result) ? result : CustomInterval.None;
        }
    }
}
