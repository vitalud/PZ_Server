using CryptoExchange.Net.Objects.Sockets;
using OKX.Net.Clients;
using OKX.Net.Enums;
using OKX.Net.Objects;
using OKX.Net.Objects.Market;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Utils;
using Server.Service;
using Server.Service.Abstract;
using Strategies.Instruments;
using System.Reactive.Linq;
using OkxKlineInterval = OKX.Net.Enums.KlineInterval;

namespace Server.Models.Burse
{
    public partial class OkxModel : BurseModel
    {
        private readonly string[] _keys;
        private readonly OKXRestClient _rest;
        private readonly OKXSocketClient _socket;

        public string[] Keys => _keys;
        public OKXRestClient Rest => _rest;
        private OKXSocketClient Socket => _socket;

        public OkxModel(InstrumentRepository instrumentService, BurseName name) : base(instrumentService, name)
        {
            _keys = [KeyReader.ReadKeyFromFile("okxapi", "projectzero.txt"), 
                     KeyReader.ReadKeyFromFile("okxsecret", "projectzero.txt"), 
                     KeyReader.ReadKeyFromFile("okxword", "projectzero.txt")];

            _rest = new OKXRestClient(options =>
            {
                options.ApiCredentials = new OKXApiCredentials(Keys[0], Keys[1], Keys[2]);
                options.OutputOriginalData = false;
            });

            _socket = new OKXSocketClient(options =>
            {
                options.ApiCredentials = new OKXApiCredentials(Keys[0], Keys[1], Keys[2]);
                options.OutputOriginalData = false;
            });
        }

        protected override async Task Subscribe(Instrument instrument)
        {
            try
            {
                var name = instrument.Name;
                foreach (var period in IntervalParser.GetOkxIntervals())
                {
                    var klineSub = await Socket.UnifiedApi.ExchangeData.SubscribeToKlineUpdatesAsync(name.Id, period,
                        data =>
                        {
                            if (data != null)
                                KlineDataHandler(data, period);
                        });
                }
                var tradeSub = await Socket.UnifiedApi.ExchangeData.SubscribeToTradeUpdatesAsync(name.Id,
                    data =>
                    {
                        if (data != null)
                            TradeDataHandler(data);
                    });
                if (tradeSub.Success)
                {
                    instrument.IsActive = true;
                }
            }
            catch
            {
                await Disconnect();
            }
        }

        protected override async Task PrepareIndicators()
        {
            var last = await Rest.UnifiedApi.ExchangeData.GetKlineHistoryAsync("BTC-USDT", OkxKlineInterval.OneMinute, limit: 60);
            if (last.Success)
            {
                var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals("BTC-USDT")
                && x.Name.Type.Equals("Spot")
                && x.Burse.Equals(BurseName.Okx));
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
        /// Обрабатывает обновления из потока данных по свечам инструмента.
        /// </summary>
        /// <param name="data">Данные.</param>
        /// <param name="period">Интервал свечи.</param>
        private async void KlineDataHandler(DataEvent<OKXKline> data, OkxKlineInterval period)
        {
            var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id.Equals(data.Data.Symbol));
            if (stock != null)
            {
                var kline = stock.Klines.Find(x => x.Interval.Equals(IntervalParser.ParseOkxInterval(period)));
                if (kline != null)
                {
                    if (data.DataTime != null)
                        stock.LastUpdate = (DateTime)data.DataTime;
                    var time = data.Data.Time.Hour * 10000 + data.Data.Time.Minute * 100;
                    if (!kline.Time.Equals(time))
                    {
                        if (period.Equals(OkxKlineInterval.OneMinute))
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

        /// <summary>
        /// Получает данные стакана при получении последних данных свечи.
        /// </summary>
        /// <param name="stock"></param>
        /// <returns></returns>
        private async Task GetOrderBook(Instrument stock)
        {
            var orderBook = await Rest.UnifiedApi.ExchangeData.GetOrderBookAsync(stock.Name.Id, 400);
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

        /// <summary>
        /// Обрабатывает обновления из потока данных по торговым сделакам инструмента.
        /// </summary>
        /// <param name="data">Данные.</param>
        private void TradeDataHandler(DataEvent<OKXTrade> data)
        {
            var stock = Instruments.Items.FirstOrDefault(x => x.Name.Id == data.Data.Symbol);
            if (stock != null)
            {
                if (data.Data.Side == OrderSide.Buy) { stock.Trades.Buy += data.Data.Quantity; }
                else if (data.Data.Side == OrderSide.Sell) { stock.Trades.Sell += data.Data.Quantity; }
            }
        }
    }
}
