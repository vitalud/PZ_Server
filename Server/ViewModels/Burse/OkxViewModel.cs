using Server.Models.Burse;
using Server.Service.Abstract;

namespace Server.ViewModels.Burse
{
    public class OkxViewModel(OkxModel okxModel) : BurseViewModel(okxModel) { }
}
