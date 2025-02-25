namespace Server.Service.Bot
{
    /// <summary>
    /// Класс представляет собой набор подтипов стратегий для их типов.
    /// </summary>
    /// <param name="name">Имя биржи.</param>
    /// <param name="code">Код типа.</param>
    /// <param name="subtypes">Подтипы стратегий.</param>
    public class Types(string name, string code, string[] subtypes)
    {
        public string Name { get; set; } = name;
        public string Code { get; set; } = code;
        public string[] Subtypes { get; set; } = subtypes;
    }
}
