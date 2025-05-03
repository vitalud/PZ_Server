using DynamicData;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Utils;
using ReactiveUI;
using Server.Service;
using Strategies.Instruments;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace Server.Models
{
    /// <summary>
    /// Класс представляет собой взаимодействие с биржей Quik.
    /// </summary>
    /// <param name="instruments"></param>
    public partial class QuikModel : ReactiveObject
    {
        private readonly InstrumentRepository _instrumentRepository;

        private readonly TcpListener _listener = new(IPAddress.Parse("127.0.0.1"), 1021);
        private TcpClient? _quik;

        private readonly CancellationTokenSource _cts = new();

        private bool _isConnected;

        private readonly SourceList<Instrument> _instruments = new();

        private readonly Subject<Exception> _exceptions = new();
        public IObservable<Exception> Exceptions => _exceptions.AsObservable();

        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }
        public SourceList<Instrument> Instruments => _instruments;

        public QuikModel(InstrumentRepository instruments)
        {
            _instrumentRepository = instruments;

            var filtered = _instrumentRepository.Instruments.Items.Where(x => x.Burse.Equals(BurseName.Quik));
            Instruments.AddRange(filtered);
        }

        /// <summary>
        /// Запускает TcpListener на прослушивание подключения со стороны Quik.
        /// </summary>
        public void Start()
        {
            _listener.Start();
            Task.Run(GetQuikAsync);
        }

        /// <summary>
        /// Принимает подключение со стороны Quik.
        /// </summary>
        /// <returns></returns>
        private async Task GetQuikAsync()
        {
            while (true)
            {
                if (!_isConnected)
                {
                    _quik = await _listener.AcceptTcpClientAsync();
                    IsConnected = true;
                    try
                    {
                        await HandleQuikClientAsync();
                    }
                    catch (Exception ex)
                    {
                        _exceptions.OnNext(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Обрабатывает входящие сообщения от Quik.
        /// </summary>
        /// <returns></returns>
        private async Task HandleQuikClientAsync()
        {
            if (_quik != null)
            {
                using var stream = _quik.GetStream();
                try
                {
                    while (true)
                    {
                        var message = await TcpService.ReadMessageAsync(stream, _cts.Token);
                        if (message != null)
                        {
                            GetQuikData(message);
                        }
                        await Task.Delay(25);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Разрыв соединения с Quik.", ex);
                }
                finally
                {
                    _quik.Dispose();
                    IsConnected = false;
                }
            }
        }

        /// <summary>
        /// Преобразует данные от Quik и добавляет/изменяет данные по инструменту.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private void GetQuikData(string message)
        {
            var data = JsonSerializer.Deserialize<QuikIndicators>(message);
            if (data != null)
            {
                var stock = GetQuikStock(data);
                if (stock != null)
                {
                    var period = IntervalParser.ParseQuikInterval(data.Interval);
                    var kline = stock.Klines.Find(x => x.Interval.Equals(period));
                    if (kline != null)
                    {
                        stock.LastUpdate = DateTime.Now;

                        kline.Day = data.Day;
                        kline.Time = data.Time;

                        kline.Open = data.Open;
                        kline.High = data.High;
                        kline.Low = data.Low;
                        kline.Close = data.Close;

                        if (period == KlineInterval.OneMinute)
                        {
                            stock.Other.Last60Close.Add(data.Close);

                            stock.Orders.AllAsks = data.AllAsks;
                            stock.Orders.AllBids = data.AllBids;
                            stock.Orders.NumAsks = data.NumAsks;
                            stock.Orders.NumBids = data.NumBids;
                            stock.Orders.BestAsks = data.BestAsks;
                            stock.Orders.BestBids = data.BestBids;
                            stock.Trades.Buy = data.TradesBuy;
                            stock.Trades.Sell = data.TradesSell;
                            stock.Trades.Volume = data.Volume;

                            stock.SignalData.Complete = true;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Возвращает инструмент Quik из базы. В случае срочных контрактов
        /// сравнивает только первые два символа инструмента.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private Instrument? GetQuikStock(QuikIndicators data)
        {
            var name = data.ClassCode == "SPBFUT" ? data.SecCode[..2] : data.SecCode;
            var stock = Instruments.Items.FirstOrDefault(x => 
                x.Name.FirstName.Equals(name) && 
                x.Name.Type.Equals(data.ClassCode));
            return stock;
        }
    }
}
