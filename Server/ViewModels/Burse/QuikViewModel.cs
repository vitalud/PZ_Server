using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Server.Models;
using Server.Service.Abstract;

namespace Server.ViewModels
{
    public class QuikViewModel(QuikModel quikModel) : BurseViewModel(quikModel)
    {
    }
}
