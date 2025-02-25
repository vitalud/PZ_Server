using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Objects.Models.V5;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Utils;
using Server.Service;
using Server.Service.Abstract;
using Strategies.Instruments;
using System.Reactive.Linq;
using BybitKlineInterval = Bybit.Net.Enums.KlineInterval;

namespace Server.Models.Burse
{
    public partial class BybitModel : BurseModel
    {
        private readonly string[] _keys;
        private readonly BybitRestClient _rest;
        private readonly BybitSocketClient _socket;

        public string[] Keys => _keys;
        public BybitRestClient Rest => _rest;
        private BybitSocketClient Socket => _socket;

        public BybitModel(InstrumentRepository instrumentService, BurseName name) : base(instrumentService, name)
        {
            _keys = [KeyReader.ReadKeyFromFile("bybitapi", "projectzero.txt"), 
                     KeyReader.ReadKeyFromFile("bybitsecret", "projectzero.txt")];

            _rest = new BybitRestClient(options =>
            {
                options.ApiCredentials = new(Keys[0], Keys[1]);
                options.OutputOriginalData = false;
            });

            _socket = new BybitSocketClient(options =>
            {
                options.ApiCredentials = new(Keys[0], Keys[1]);
                options.OutputOriginalData = false;
                options.ReconnectInterval = TimeSpan.FromSeconds(5);
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
            var last = await Rest.V5Api.ExchangeData.GetKlinesAsync(Category.Spot, "BTCUSDT", BybitKlineInterval.OneMinute, limit: 60);
            if (last.Success)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals("BTCUSDT")
                && x.Name.Type.Equals("Spot")
                && x.Burse.Equals(BurseName.Bybit));
                if (stock != null)
                {
                    foreach (var kline in last.Data.List) 
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
            foreach (var period in IntervalParser.GetBybitIntervals())
            {
                CallResult<UpdateSubscription> klineSub;
                if (instrument.Name.Type.Equals("Spot"))
                {
                    klineSub = await Socket.V5SpotApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period,
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period, "Spot");
                        });
                }
                else if (instrument.Name.Type.Equals("Futures"))
                {
                    klineSub = await Socket.V5LinearApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period,
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period, "Futures");
                        });
                }
                else if (instrument.Name.Type.Equals("InverseFutures"))
                {
                    klineSub = await Socket.V5InverseApi.SubscribeToKlineUpdatesAsync(instrument.Name.Id, period,
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period, "InverseFutures");
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
                tradeSub = await Socket.V5SpotApi.SubscribeToTradeUpdatesAsync(instrument.Name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data, "Spot");
                    });
                if (tradeSub.Success)
                    instrument.IsActive = true;
            }
            else if (instrument.Name.Type.Equals("Futures"))
            {
                tradeSub = await Socket.V5LinearApi.SubscribeToTradeUpdatesAsync(instrument.Name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data, "Futures");
                    });
                if (tradeSub.Success)
                    instrument.IsActive = true;
            }
            else if (instrument.Name.Type.Equals("InverseFutures"))
            {
                tradeSub = await Socket.V5InverseApi.SubscribeToTradeUpdatesAsync(instrument.Name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data, "InverseFutures");
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
        private async void KlineDataHandler(DataEvent<IEnumerable<BybitKlineUpdate>> data, BybitKlineInterval period, string type)
        {
            foreach (var dat in data.Data)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(data.Symbol) && x.Name.Type.Equals(type));
                if (stock != null)
                {
                    var kline = stock.Klines.Find(x => x.Interval.Equals(IntervalParser.ParseBybitInterval(period)));
                    if (kline != null)
                    {
                        stock.LastUpdate = dat.Timestamp;
                        var time = dat.StartTime.Hour * 10000 + dat.StartTime.Minute * 100;
                        if (!kline.Time.Equals(time))
                        {
                            if (period.Equals(BybitKlineInterval.OneMinute))
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

        /// <summary>
        /// Получает данные стакана при получении последних данных свечи.
        /// </summary>
        /// <param name="stock"></param>
        /// <returns></returns>
        private async Task GetOrderBook(Instrument stock)
        {
            BybitOrderbook? orderBook = null;
            if (stock.Name.Type.Equals("Spot"))
            {
                var result = await Rest.V5Api.ExchangeData.GetOrderbookAsync(Category.Spot, stock.Name.Id, 500);
                orderBook = result.Data;
            }
            else if (stock.Name.Type.Equals("Futures"))
            {
                var result = await Rest.V5Api.ExchangeData.GetOrderbookAsync(Category.Linear, stock.Name.Id, 500);
                orderBook = result.Data;
            }
            else if (stock.Name.Type.Equals("InverseFutures"))
            {
                var result = await Rest.V5Api.ExchangeData.GetOrderbookAsync(Category.Inverse, stock.Name.Id, 500);
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
        private void TradeDataHandler(DataEvent<IEnumerable<BybitTrade>> data, string type)
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
}
