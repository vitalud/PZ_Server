using ReactiveUI;
using Strategies.Strategies;

namespace Server.Service.UserClient
{
    /// <summary>
    /// Временные параметры, используемые для промежуточных операций в чат боте.
    /// </summary>
    public partial class Temp : ReactiveObject
    {
        public List<Strategy> Strategies { get; set; } = [];
        public string Burse { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string PhotoId { get; set; } = string.Empty;
        public int Limit { get; set; }
        public int Price { get; set; }
        public int Deposit { get; set; }

        public void ClearData()
        {
            Strategies.Clear();
            Burse = string.Empty;
            Code = string.Empty;
            PhotoId = string.Empty;
            Limit = 0;
            Price = 0;
            Deposit = 0;
        }
    }
}
