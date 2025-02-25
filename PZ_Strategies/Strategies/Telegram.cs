namespace Strategies.Strategies
{
    /// <summary>
    /// Данные стратегии для отображения в телеграмм.
    /// </summary>
    /// <param name="type">Тип стратегии.</param>
    /// <param name="subtype">Подтип стратегии.</param>
    /// <param name="pl">Коэффициент.</param>
    /// <param name="description">Описание.</param>
    /// <param name="imagePath">Путь к изображению.</param>
    /// <param name="limit">Минимальный торговый лимит.</param>
    public class Telegram(string type, string subtype, int pl, string description, string imagePath, int limit)
    {
        public string Type { get; set; } = type;
        public string Subtype { get; set; } = subtype;
        public int Pl { get; set; } = pl;
        public string Description { get; set; } = description;
        public string ImagePath { get; set; } = imagePath;
        public int Limit { get; set; } = limit;
    }
}
