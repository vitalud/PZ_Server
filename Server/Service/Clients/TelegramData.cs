using ReactiveUI;
using Server.Service.Enums;

namespace Server.Service.UserClient
{
    /// <summary>
    /// Данные клиента для взаимодействия с чат ботом.
    /// </summary>
    /// <param name="id"></param>
    public partial class TelegramData(string id) : ReactiveObject
    {
        private State _state;
        private Stage _stage;
        private Temp _temp = new();

        public State State
        {
            get => _state;
            set => this.RaiseAndSetIfChanged(ref _state, value);
        }
        public Stage Stage
        {
            get => _stage;
            set => this.RaiseAndSetIfChanged(ref _stage, value);
        }
        public Temp Temp
        {
            get => _temp;
            set => this.RaiseAndSetIfChanged(ref _temp, value);
        }
        public string Id { get; } = id;
        public int Index { get; set; }
        public int Lenght { get; set; }
    }
}
