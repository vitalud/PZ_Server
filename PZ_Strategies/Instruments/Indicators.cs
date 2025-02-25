using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace Strategies.Instruments
{
    /// <summary>
    /// Прочие вычисляемые индикаторы инструмента.
    /// </summary>
    public partial class Indicators : ReactiveObject
    {
        private decimal _average60Close;
        private ObservableCollection<decimal> _last = [];

        public decimal Average60Close
        {
            get => _average60Close;
            private set => this.RaiseAndSetIfChanged(ref _average60Close, value);
        }

        public ObservableCollection<decimal> Last60Close
        {
            get => _last;
            set => this.RaiseAndSetIfChanged(ref _last, value);
        }

        public Indicators()
        {
            CreateSubscriptions();
        }

        /// <summary>
        /// Создает подписки для автоматического вычисления индикаторов.
        /// </summary>
        private void CreateSubscriptions()
        {
            this.WhenAnyValue(x => x.Last60Close.Count)
                .Subscribe(count => CalculateAverage(60, count));
        }

        /// <summary>
        /// Рассчитывает средний показатель последних ClosePrice.
        /// </summary>
        /// <param name="amount">Количество значений для расчета.</param>
        /// <param name="count">Текущее количество значений.</param>
        private void CalculateAverage(int amount, int count)
        {
            if (count >= amount)
            {
                if (count == amount + 1)
                    Last60Close.RemoveAt(0);
                Average60Close = Last60Close.Average();
            }
            else Average60Close = 0;
        }
    }
}
