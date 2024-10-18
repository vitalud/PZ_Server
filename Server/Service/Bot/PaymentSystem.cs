namespace Server.Service.Bot
{
    public class PaymentSystem(string name, string token)
    {
        public string Name { get; set; } = name;
        public string Token { get; set; } = token;
        public readonly string photoUrl = "https://sun9-12.userapi.com/impg/S2FcQICGUcOyC9-cdcibNffcZLIjAO-PKWGk6w/HF__iJx6QhY.jpg?size=771x511&quality=96&sign=6c1389adc7c4ceeb3ba988640a765af9&type=album";
    }
}
