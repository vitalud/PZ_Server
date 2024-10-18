using ReactiveUI;
using Server.Service.Bot;

namespace Server.Models
{
    public class ServerModel : ReactiveObject
    {
        private readonly TelegramBot _telegram;
        private readonly TcpConnector _connector;

        public TelegramBot Telegram => _telegram;
        public TcpConnector Connector => _connector;

        public ServerModel(TelegramBot telegram, TcpConnector connector)
        {
            _telegram = telegram;
            _connector = connector;
        }

        public void Start()
        {
            _telegram.Start();
            _connector.ConnectorStart();
        }
    }
}
