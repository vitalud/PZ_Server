namespace Server.Service.Bot
{
    /// <summary>
    /// Класс представляет собой набор типов стратегий для биржи.
    /// </summary>
    /// <param name="name">Имя биржи.</param>
    /// <param name="types">Типы стратегий для биржи.</param>
    public class Subscription(string name, List<Types> types)
    {
        public string Name { get; set; } = name;
        public List<Types> Types { get; set; } = types;
    }
}
