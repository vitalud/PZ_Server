using System.IO;

namespace Server.Service.Bot
{
    /// <summary>
    /// Платежные системы для чат бота.
    /// </summary>
    /// <param name="name">Имя платежной системы.</param>
    /// <param name="token">Токен платежной системы.</param>
    public class PaymentSystem(string name, string token)
    {
        public string Name { get; set; } = name;
        public string Token { get; set; } = token;
        public readonly string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Images", "payment.jpg");
    }
}
