using DynamicData;
using ReactiveUI;
using Server.Models;
using System.Collections.ObjectModel;

namespace Server.ViewModels
{
    public partial class ServerViewModel : ReactiveObject
    {
        private readonly ServerModel _server;

        private readonly ReadOnlyObservableCollection<string> _errorLogs;
        private readonly ReadOnlyObservableCollection<string> _connectorLogs;
        private readonly ReadOnlyObservableCollection<string> _telegramLogs;

        public ServerModel Server => _server;
        public ReadOnlyObservableCollection<string> ErrorLogs => _errorLogs;
        public ReadOnlyObservableCollection<string> ConnectorLogs => _connectorLogs;
        public ReadOnlyObservableCollection<string> TelegramLogs => _telegramLogs;

        public bool IsActive => Server.Connector.IsActive;
        public int ActiveConnections => Server.Connector.ActiveConnections;

        public ServerViewModel(ServerModel server)
        {
            _server = server;
            var connector = Server.Connector;
            var telegram = Server.Telegram;

            connector.Logs.Connect()
               .Bind(out _connectorLogs)
               .Subscribe();
            telegram.Messages.Connect()
               .Bind(out _telegramLogs)
               .Subscribe();
            telegram.Errors.Connect()
                     .Bind(out _errorLogs)
                     .Subscribe();
        }

        /// <summary>
        /// Отправляет сообщение об ошибке в чат бот.
        /// </summary>
        /// <param name="ex"></param>
        public async void SendErrorMessage(Exception ex) => await Server.SendErrorMessage(ex);
    }
}
