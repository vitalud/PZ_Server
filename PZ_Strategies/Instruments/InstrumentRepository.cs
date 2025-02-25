using DynamicData;
using ProjectZeroLib.Enums;
using ProjectZeroLib.Utils;
using Strategies.Instruments.Burse;
using System.Reactive.Linq;

namespace Strategies.Instruments
{
    /// <summary>
    /// Хранилище инструментов.
    /// </summary>
    public class InstrumentRepository
    {
        private readonly bool _logging;

        private readonly SourceList<Instrument> _instruments = new();

        public bool Logging => _logging;
        public SourceList<Instrument> Instruments => _instruments;

        public InstrumentRepository(bool logging)
        {
            _logging = logging;

            InitializeInstruments();
        }

        /// <summary>
        /// Инициализирует список инструментов и запускает задачу, отслеживающую
        /// дату экспирации ежедневно.
        /// </summary>
        private void InitializeInstruments()
        {
            SetInstruments(BurseName.Okx, OkxInstruments.Instruments, Logging);
            SetInstruments(BurseName.Bybit, BybitInstruments.Instruments, Logging);
            SetInstruments(BurseName.Binance, BinanceInstruments.Instruments, Logging);
            SetInstruments(BurseName.Quik, QuikInstruments.Instruments, Logging);

            Task.Run(StartDailyCheck);
        }

        /// <summary>
        /// Заполняет контейнер инструментами.
        /// </summary>
        /// <param name="burse">Биржа, которой принадлежат инструменты.</param>
        /// <param name="instruments">Контейнер с названиями инструментов.</param>
        /// <param name="logging">Параметр, отвечающий за логирование котировок инструмента.</param>
        private void SetInstruments(BurseName burse, Dictionary<string, List<Name>> instruments, bool logging)
        {
            foreach (var type in instruments.Keys)
            {
                foreach (var inst in instruments[type])
                {
                    var name = new Name(inst.FirstName)
                    {
                        Type = type,
                    };
                    Instruments.Add(new Instrument(burse, name, logging));
                }
            }

            if (burse != BurseName.Quik)
                SetExpirationDate(burse);
        }

        /// <summary>
        /// Устанавливает дату экспирации для срочных инструментов.
        /// </summary>
        /// <param name="name">Имя биржи, требуемое для выбора типа срочных инструментов.</param>
        private void SetExpirationDate(BurseName name)
        {
            var expiration = Expiration.GetQuarterExpirationDate(DateTime.UtcNow).ToString("yyMMdd");
            var date = DateTime.UtcNow.AddDays(1).ToString("yyMMdd");

            var instruments = Instruments.Items.Where(x => x.Burse == name);

            if (name == BurseName.Okx)
            {
                var futures = instruments.Where(x => x.Name.Type == "Futures");
                if (futures.Any())
                {
                    foreach (var fut in futures)
                        fut.Name.Expiration = Expiration.GetQuarterExpirationDate(DateTime.UtcNow).ToString("yyMMdd");
                }
            }
            else if (name == BurseName.Bybit)
            {
                var futures = instruments.Where(x => x.Name.Type == "InverseFutures");
                if (futures.Any())
                {
                    foreach (var fut in futures)
                        fut.Name.Expiration = Expiration.ConvertExpirationDateToBybitCode(Expiration.GetQuarterExpirationDate(DateTime.UtcNow));
                }
            }
            else if (name == BurseName.Binance)
            {
                var futures = instruments.Where(x => x.Name.Type == "UsdFutures" || x.Name.Type == "CoinFutures");
                if (futures.Any())
                {
                    foreach (var fut in futures)
                    {
                        if (fut.Name.FirstName[^1].Equals('_'))
                            fut.Name.Expiration = Expiration.GetQuarterExpirationDate(DateTime.UtcNow).ToString("yyMMdd");
                    }
                }
            }
        }

        /// <summary>
        /// Запускает со следующего дня после запуска в 4:00 по МСК
        /// ежедневную задачу по отслеживанию даты экспирации.
        /// </summary>
        /// <returns></returns>
        private async Task StartDailyCheck()
        {
            var now = DateTime.UtcNow.AddHours(-1);
            var nextRun = now.Date.AddDays(1);
            var initialDelay = nextRun - now;

            await Task.Delay(initialDelay);

            Observable.Interval(TimeSpan.FromDays(1))
                .Subscribe(_ => CheckCurrentDay());
        }

        /// <summary>
        /// Проверяет текущую дату относительно даты экспирации и вызывает
        /// в случае их разницы обновление даты экспирации у срочных 
        /// инструментов.
        /// </summary>
        private void CheckCurrentDay()
        {
            var expiration = Expiration.GetQuarterExpirationDate(DateTime.UtcNow).ToString("yyMMdd");
            var date = DateTime.UtcNow.ToString("yyMMdd");
            if (date != expiration)
            {
                foreach (var item in Instruments.Items)
                    SetExpirationDate(item.Burse);
            }
        }
    }
}
