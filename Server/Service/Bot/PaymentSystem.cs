using System.IO;

namespace Server.Service.Bot
{
    public class PaymentSystem(string name, string token)
    {
        public string Name { get; set; } = name;
        public string Token { get; set; } = token;
        public readonly string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Images", "payment.jpg");
    }
}
