using DynamicData;
using ReactiveUI;
using Server.Models;
using System.Collections.ObjectModel;
using System.Reactive;

namespace Server.ViewModels
{
    public class ServerViewModel : ReactiveObject
    {
        private readonly ServerModel _server;
        public bool IsActive
        {
            get => _server.Connector.IsActive;
        }
        public int ActiveConnections
        {
            get => _server.Connector.ActiveConnections;
        }

        private readonly ReadOnlyObservableCollection<string> _errorLogs;
        public ReadOnlyObservableCollection<string> ErrorLogs => _errorLogs;

        private readonly ReadOnlyObservableCollection<string> _connectorLogs;
        public ReadOnlyObservableCollection<string> ConnectorLogs => _connectorLogs;

        private readonly ReadOnlyObservableCollection<string> _telegramLogs;
        public ReadOnlyObservableCollection<string> TelegramLogs => _telegramLogs;

        public ReactiveCommand<Unit, Unit> StartTelegram { get; }

        public ServerViewModel(ServerModel server)
        {
            _server = server;
            var connector = _server.Connector;
            var telegram = _server.Telegram;
            connector.Logs.Connect()
               .Bind(out _connectorLogs)
               .Subscribe();
            telegram.Messages.Connect()
               .Bind(out _telegramLogs)
               .Subscribe();
            telegram.Errors.Connect()
                     .Bind(out _errorLogs)
                     .Subscribe();

            StartTelegram = ReactiveCommand.Create(() => _server.Start());
        }
    }
}
