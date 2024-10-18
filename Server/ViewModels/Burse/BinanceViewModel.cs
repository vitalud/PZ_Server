using Server.Models.Burse;
using Server.Service.Abstract;

namespace Server.ViewModels
{
    public class BinanceViewModel(BinanceModel binanceModel) : BurseViewModel(binanceModel) { }
}
