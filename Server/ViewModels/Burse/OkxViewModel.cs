using Server.Models.Burse;
using Server.Service.Abstract;

namespace Server.ViewModels.Burse
{
    public partial class OkxViewModel(OkxModel okxModel) : BurseViewModel(okxModel) { }
}
