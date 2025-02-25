using DynamicData;
using ProjectZeroLib.Utils;
using ReactiveUI;
using Server.Service.Abstract;
using System.Reactive.Linq;

namespace Server.Models
{
    /// <summary>
    /// Базовый класс, в котором собраны все сервисы.
    /// </summary>
    public partial class MainModel : ReactiveObject
    {
        private readonly ServerModel _server;
        private readonly ClientsModel _clients;
        private readonly object _locker = new();

        public MainModel(ServerModel server, ClientsModel clients)
        {
            _server = server;
            _clients = clients;

            Task.Run(StartDailyCheck);
        }

        /// <summary>
        /// Запускает со следующего дня после запуска ежедневную задачу
        /// по сохранению логов.
        /// </summary>
        /// <returns></returns>
        private async Task StartDailyCheck()
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
            var initialDelay = nextRun - now;

            await Task.Delay(initialDelay);

            Observable.Interval(TimeSpan.FromDays(1))
                .Subscribe(_ => SaveLogs());
        }

        /// <summary>
        /// Собирает коллекцию логов из сервисов, сохраняет и очищает.
        /// </summary>
        public void SaveLogs()
        {
            lock (_locker)
            {
                var logs = new Dictionary<string, SourceList<string>>
                {
                    { "connector", _server.Connector.Logs },
                    { "telegram", _server.Telegram.Messages },
                    { "clients", _clients.Logs }
                };

                Logger.BackupLog(logs);

                _server.Connector.Logs.Clear();
                _server.Telegram.Messages.Clear();
                _clients.Logs.Clear();
            }
        }
    }
}
