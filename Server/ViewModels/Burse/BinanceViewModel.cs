using Server.Models.Burse;
using Server.Service.Abstract;

namespace Server.ViewModels.Burse
{
    public partial class BinanceViewModel(BinanceModel binanceModel) : BurseViewModel(binanceModel) { }
}
