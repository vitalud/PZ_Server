using ReactiveUI;
using System.Net.Sockets;

namespace Server.Service.UserClient
{
    /// <summary>
    /// Класс, описывающий данные клиента.
    /// </summary>
    public partial class Client : ReactiveObject
    {
        private Data _data;
        private TelegramData _telegram;
        private bool _isActive;

        public Data Data
        {
            get => _data;
            set => this.RaiseAndSetIfChanged(ref _data, value);
        }
        public TelegramData Telegram
        {
            get => _telegram;
            set => this.RaiseAndSetIfChanged(ref _telegram, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }

        public string SessionId { get; set; } = "default";

        public Client(Data data, TelegramData telegram)
        {
            _data = data;
            _telegram = telegram;
        }
    }
}
