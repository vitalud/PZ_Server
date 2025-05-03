using Binance.Net.Clients;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Objects.Models.Spot.Socket;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using ProjectZeroLib.Enums;
using Strategies.Instruments;
using ProjectZeroLib.Utils;
using Server.Service;
using Server.Service.Abstract;
using System.Reactive.Linq;
using BinanceKlineInterval = Binance.Net.Enums.KlineInterval;

namespace Server.Models.Burse
{
    /// <summary>
    /// Класс представляет собой взаимодействие с биржей Binance.
    /// Создаются подписки на обновления данных инструментов.
    /// </summary>
    public partial class BinanceModel : BurseModel
    {
        private readonly string[] _keys;
        private readonly BinanceRestClient _rest;
        private readonly BinanceSocketClient _socket;

        public string[] Keys => _keys;
        public BinanceRestClient Rest => _rest;
        private BinanceSocketClient Socket => _socket;

        public BinanceModel(InstrumentRepository instrumentService, BurseName name) : base(instrumentService, name)
        {
            _keys = [KeyReader.ReadKeyFromFile("binanceapi", "projectzero.txt"), 
                     KeyReader.ReadKeyFromFile("binancesecret", "projectzero.txt")];

            _rest = new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(Keys[0], Keys[1]);
                options.OutputOriginalData = false;
            });

            _socket = new BinanceSocketClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(Keys[0], Keys[1]);
                options.OutputOriginalData = false;
            });
        }

        protected override async Task Subscribe(Instrument instrument)
        {
            try
            {
                await SubscribeToKlineUpdates(instrument);
                await SubscribeToTradeUpdates(instrument);
            }
            catch
            {
                await Disconnect();
            }
        }

        protected override async Task PrepareIndicators()
        {
            var last = await Rest.SpotApi.ExchangeData.GetKlinesAsync("BTCUSDT", BinanceKlineInterval.OneMinute, limit: 60);
            if (last.Success)
            {
                var stock = Instruments.Items.FirstOrDefault(x => 
                x.Name.Id.Equals("BTCUSDT") && 
                x.Name.Type.Equals("Spot") && 
                x.Burse.Equals(BurseName.Binance));

                if (stock != null)
                {
                    foreach (var kline in last.Data) 
                        stock.Other.Last60Close.Add(kline.ClosePrice);
                }
            }
        }

        protected override async Task Disconnect()
        {
            await Socket.UnsubscribeAllAsync();

            IsActive = false;
            foreach (var item in Instruments.Items)
                item.IsActive = false;
        }

        /// <summary>
        /// Подписывается на поток данных свечей инструмента.
        /// </summary>
        /// <param name="instrument">Инструмент.</param>
        /// <returns></returns>
        private async Task SubscribeToKlineUpdates(Instrument instrument)
        {
            foreach (var period in IntervalParser.GetBinanceIntervals())
            {
                CallResult<UpdateSubscription> klineSub;
                if (instrument.Name.Type.Equals("Spot"))
                {
                    klineSub = await Socket.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period,
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period, "Spot");
                        });
                }
                else if (instrument.Name.Type.Equals("UsdFutures"))
                {
                    klineSub = await Socket.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period,
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period, "UsdFutures");
                        });
                }
                else if (instrument.Name.Type.Equals("CoinFutures"))
                {
                    klineSub = await Socket.CoinFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period, 
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period, "CoinFutures");
                        });
                }
            }
        }

        /// <summary>
        /// Подписывается на поток данных торговых сделок инструмента.
        /// </summary>
        /// <param name="instrument">Инструмент.</param>
        /// <returns></returns>
        private async Task SubscribeToTradeUpdates(Instrument instrument)
        {
            CallResult<UpdateSubscription> tradeSub;

            if (instrument.Name.Type.Equals("Spot"))
            {
                tradeSub = await Socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(instrument.Name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data, "Spot");
                    });
                if (tradeSub.Success)
                    instrument.IsActive = true;
            }
            else if (instrument.Name.Type.Equals("UsdFutures"))
            {
                tradeSub = await Socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(instrument.Name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data, "UsdFutures");
                    });
                if (tradeSub.Success)
                    instrument.IsActive = true;
            }
            else if (instrument.Name.Type.Equals("CoinFutures"))
            {
                tradeSub = await Socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(instrument.Name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data, "CoinFutures");
                    });
                if (tradeSub.Success)
                    instrument.IsActive = true;
            }
        }

        /// <summary>
        /// Обрабатывает обновления из потока данных по свечам инструмента.
        /// </summary>
        /// <param name="data">Данные.</param>
        /// <param name="period">Интервал свечи.</param>
        /// <param name="type">Тип инструмента.</param>
        private async void KlineDataHandler(DataEvent<IBinanceStreamKlineData> data, BinanceKlineInterval period, string type)
        {
            var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(data.Symbol) && x.Name.Type.Equals(type));
            if (stock != null)
            {
                var kline = stock.Klines.Find(x => x.Interval.Equals(IntervalParser.ParseBinanceInterval(period)));
                if (kline != null)
                {
                    if (data.DataTime != null)
                        stock.LastUpdate = (DateTime)data.DataTime;
                    if (data.Data.Data.Final)
                    {
                        if (period.Equals(BinanceKlineInterval.OneMinute))
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

        /// <summary>
        /// Получает данные стакана при получении последних данных свечи.
        /// </summary>
        /// <param name="stock"></param>
        /// <returns></returns>
        private async Task GetOrderBook(Instrument stock)
        {
            BinanceOrderBook? orderBook = null;
            if (stock.Name.Type.Equals("Spot"))
            {
                var result = await Rest.SpotApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 500);
                orderBook = result.Data;
            }
            else if (stock.Name.Type.Equals("UsdFutures"))
            {
                var result = await Rest.UsdFuturesApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 500);
                orderBook = result.Data;
            }
            else if (stock.Name.Type.Equals("CoinFutures"))
            {
                var result = await Rest.CoinFuturesApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 500);
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

        /// <summary>
        /// Обрабатывает обновления из потока данных по торговым сделакам инструмента.
        /// </summary>
        /// <param name="data">Данные.</param>
        /// <param name="type">Тип инструмента.</param>
        private void TradeDataHandler(DataEvent<BinanceStreamTrade> data, string type)
        {
            var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id == data.Symbol && x.Name.Type.Equals(type));
            if (stock != null)
            {
                if (data.Data.BuyerIsMaker) { stock.Trades.Sell += data.Data.Quantity; }
                else stock.Trades.Buy += data.Data.Quantity;
            }
        }
    }
}
