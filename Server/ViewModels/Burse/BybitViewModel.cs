using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Server.Models.Burse;
using Server.Service.Abstract;

namespace Server.ViewModels.Burse
{
    public class BybitViewModel(BybitModel bybitModel) : BurseViewModel(bybitModel)
    {
    }
}
