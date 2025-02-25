using DynamicData;
using ProjectZeroLib.Enums;
using ReactiveUI;
using Strategies.Instruments;
using System.Reactive.Linq;

namespace Server.Service.Abstract
{
    /// <summary>
    /// Абстрактный класс, описывающий методы и свойства,
    /// необходимые для взаимодействия с криптобиржей.
    /// </summary>
    public abstract class BurseModel : ReactiveObject
    {
        protected readonly InstrumentRepository _instrumentRepository;

        private bool _isActive;
        private BurseName _name;

        private readonly SourceList<Instrument> _instruments = new();

        /// <summary>
        /// Подключение активно (true) в случае, если подписки на обновления
        /// данных всех инструменты завершены успешно.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }

        /// <summary>
        /// Имя криптобиржи, с которой происходит взаимодействие.
        /// </summary>
        public BurseName Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        /// <summary>
        /// Контейнер с инструментами, принадлежащим бирже.
        /// </summary>
        public SourceList<Instrument> Instruments => _instruments;

        public BurseModel(InstrumentRepository instrumentRepository, BurseName name)
        {
            _instrumentRepository = instrumentRepository;
            _name = name;

            GetSortedInstruments();
        }

        /// <summary>
        /// Сортирует инструменты по принадлежности к бирже и заполняет им контейнер с инструментами,
        /// а также создает подписку на переподску инструмента в случае обновления даты экспирации.
        /// </summary>
        private void GetSortedInstruments()
        {
            var filtered = _instrumentRepository.Instruments.Items.Where(x => x.Burse.Equals(Name));
            Instruments.AddRange(filtered);

            foreach (var instrument in Instruments.Items)
            {
                instrument.Name.WhenAnyValue(x => x.Expiration)
                    .Skip(1)
                    .Subscribe(async _ => await UpdateSubOnExpire(instrument));
            }
        }

        /// <summary>
        /// Обновляет подписку на срочный инструмент при экспирации.
        /// TODO: написать тесты для проверки.
        /// </summary>
        /// <param name="inst">Срочный инструмент.</param>
        /// <returns></returns>
        private async Task UpdateSubOnExpire(Instrument inst)
        {
            inst.IsActive = false;
            await Subscribe(inst);
        }

        /// <summary>
        /// Подключается к бирже и создает подписки на потоки данных инструментов.
        /// </summary>
        public async void Connect()
        {
            if (IsActive)
                await Disconnect();

            await GetSubscriptions();
        }

        /// <summary>
        /// Создает подписки на потоки данных инструментов, 
        /// а также подготавливает вычисляемые индикаторы.
        /// </summary>
        /// <returns></returns>
        private async Task GetSubscriptions()
        {
            var tasks = new List<Task>();
            foreach (var inst in Instruments.Items)
                tasks.Add(Subscribe(inst));

            await Task.WhenAll(tasks);

            foreach (var inst in Instruments.Items)
            {
                if (!inst.IsActive)
                {
                    await Disconnect();
                    return;
                }
            }

            IsActive = true;

            await PrepareIndicators();
        }

        /// <summary>
        /// Собирает данные для расчета вычисляемых индикаторов.
        /// </summary>
        /// <returns></returns>
        protected abstract Task PrepareIndicators();

        /// <summary>
        /// Подписка на обновление свечей по всем интервалам инструмента,
        /// а также обновление стакана и торговых сделок.
        /// </summary>
        /// <param name="instrument">Инструмент.</param>
        /// <returns></returns>
        protected abstract Task Subscribe(Instrument instrument);

        /// <summary>
        /// Сбрасывает все подписки.
        /// </summary>
        protected abstract Task Disconnect();
    }
}
