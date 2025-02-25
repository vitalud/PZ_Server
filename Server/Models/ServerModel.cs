using ReactiveUI;
using Server.Service.Abstract;
using Server.Service.Bot;

namespace Server.Models
{
    /// <summary>
    /// Класс представляет собой сервер, отвечающий за работу 
    /// чат бота в телеграм и прослушивание клиентов.
    /// </summary>
    public partial class ServerModel : ReactiveObject
    {
        private readonly TelegramBot _telegram;
        private readonly Connector _connector;

        public TelegramBot Telegram => _telegram;
        public Connector Connector => _connector;

        public ServerModel(TelegramBot telegram, Connector connector)
        {
            _telegram = telegram;
            _connector = connector;

            Connector.ConnectorStart();
            Telegram.Start();
        }

        /// <summary>
        /// Отправляет сообщение с ошибкой в телеграм.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public async Task SendErrorMessage(Exception ex) => await Telegram.SendErrorMessage(ex);
    }
}
