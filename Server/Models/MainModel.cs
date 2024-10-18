using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using Server.Service.Abstract;

namespace Server.Models
{
    public class MainModel : ReactiveObject
    {
        private readonly ServerModel _server;
        private readonly ClientsModel _clients;
        private readonly BursesModel _burses;

        private static Timer _timer;

        public MainModel(ServerModel server, ClientsModel clients, BursesModel burses)
        {
            _server = server;
            _clients = clients;
            _burses = burses;

            SetupTimer();

            _server.Start();
        }

        private void SetupTimer()
        {
            var currentTime = DateTime.Now;
            var millisecondsRemaining = 24 * 60 * 60 * 1000 - currentTime.TimeOfDay.TotalMilliseconds;
            var timeToNextDay = TimeSpan.FromMilliseconds(millisecondsRemaining);
            _timer = new Timer(_ => SaveLogs(), null, timeToNextDay, TimeSpan.FromDays(1));
        }

        public void SaveLogs()
        {
            lock (this)
            {
                Dictionary<string, SourceList<string>> logs = [];
                logs.Add("connector", _server.Connector.Logs);
                logs.Add("telegram", _server.Telegram.Messages);
                logs.Add("clients", _clients.ClientLogs);
                Logger.BackupLog(logs);
                _server.Connector.Logs.Clear();
                _server.Telegram.Messages.Clear();
                _clients.ClientLogs.Clear();
            }
        }

        public void Test()
        {
            //SaveLogs();
            //_burses.Test();
        }
    }
}
