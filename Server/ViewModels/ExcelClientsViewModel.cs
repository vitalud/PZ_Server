using Server.Service.Abstract;

namespace Server.ViewModels
{
    public partial class ExcelClientsViewModel(ClientsModel excelModel) : ClientsViewModel(excelModel) { }
}
