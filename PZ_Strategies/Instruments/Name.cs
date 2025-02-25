using ReactiveUI;
using System.Reactive.Linq;

namespace Strategies.Instruments
{
    /// <summary>
    /// Данные по имени инструмента для простых и составных (срочных) названий инструмента.
    /// </summary>
    public partial class Name : ReactiveObject
    {
        private string _firstName;
        private string _expiration = string.Empty;
        private string _id;
        private string _type = string.Empty;

        public string FirstName
        {
            get => _firstName;
            set => this.RaiseAndSetIfChanged(ref _firstName, value);
        }
        public string Expiration
        {
            get => _expiration;
            set => this.RaiseAndSetIfChanged(ref _expiration, value);
        }
        public string Id
        {
            get => _id;
            set => this.RaiseAndSetIfChanged(ref _id, value);
        }
        public string Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }

        public Name(string name)
        {
            _firstName = name;
            _id = name;

            CreateSubscriptions();
        }

        /// <summary>
        /// Создает подписку на автоматическое формирование имени инструмента при изменении даты экспирации.
        /// </summary>
        private void CreateSubscriptions()
        {
            this.WhenAnyValue(x => x.Expiration)
                .Skip(1)
                .Subscribe(v => Id = FirstName + Expiration);
        }
    }
}
