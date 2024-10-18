using Server.Models.Burse;
using Server.Service.Abstract;

namespace Server.ViewModels
{
    public class OkxViewModel(OkxModel okxModel) : BurseViewModel(okxModel) { }
}
