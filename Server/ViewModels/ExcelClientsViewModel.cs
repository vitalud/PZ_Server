using Server.Models;
using Server.Service.Abstract;

namespace Server.ViewModels
{
    public class ExcelClientsViewModel(ExcelClientsModel excelModel) : ClientsViewModel(excelModel) { }
}
