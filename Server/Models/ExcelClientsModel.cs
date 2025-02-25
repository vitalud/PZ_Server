using ClosedXML.Excel;
using DynamicData;
using ProjectZeroLib.Utils;
using ReactiveUI;
using Server.Service.Abstract;
using Server.Service.Enums;
using Server.Service.UserClient;
using System.Reactive.Linq;

namespace Server.Models
{
    /// <summary>
    /// Реализация базы данных пользователей в Excel.
    /// </summary>
    public partial class ExcelClientsModel : ClientsModel
    {
        private readonly XLWorkbook _workbook;

        /// <summary>
        /// Считывает файл базы данных и создает подписки для считанных клиентов.
        /// </summary>
        public ExcelClientsModel()
        {
            _workbook = new XLWorkbook(_path);

            GetClients();
            CreateSubscriptions();
        }

        /// <summary>
        /// Получение клиентов из файла/файлов "базы данных".
        /// </summary>
        protected override void GetClients()
        {
            foreach (var ws in _workbook.Worksheets)
            {
                if (ws.Name.Equals("Main")) 
                    GetClientBase(ws);
                else 
                    GetStrategyData(ws);
            }
        }

        /// <summary>
        /// Считывает данные клиента из книги Main.
        /// </summary>
        /// <param name="ws">Книга Main.</param>
        private void GetClientBase(IXLWorksheet ws)
        {
            var firstRow = ws.FirstRowUsed();
            if (firstRow == null) return;

            for (int i = 2; i <= ws.RowsUsed().Count(); i++)
            {
                var row = ws.Row(i);

                var data = new Data(row.Cell(GetCellByName(firstRow, "Login")).Value.ToString())
                {
                    Password = row.Cell(GetCellByName(firstRow, "Password")).Value.ToString(),
                    Deposit = int.Parse(row.Cell(GetCellByName(firstRow, "Deposit")).Value.ToString()),
                    Payment = int.Parse(row.Cell(GetCellByName(firstRow, "Payment")).Value.ToString()),
                    Percentage = double.Parse(row.Cell(GetCellByName(firstRow, "Percentage")).Value.ToString()),
                };

                var telegram = new TelegramData(row.Cell(GetCellByName(firstRow, "ID")).Value.ToString())
                {
                    State = (State)Enum.Parse(typeof(State), row.Cell(GetCellByName(firstRow, "State")).Value.ToString()),
                    Stage = (Stage)Enum.Parse(typeof(Stage), row.Cell(GetCellByName(firstRow, "Stage")).Value.ToString())
                };

                Clients.Add(new Client(data, telegram));
            }
        }

        /// <summary>
        /// Получение стратегий из книги.
        /// </summary>
        /// <param name="ws">Книга биржи.</param>
        private void GetStrategyData(IXLWorksheet ws)
        {
            var a = ws.RowsUsed().Count();
            for (int i = 2; i <= ws.RowsUsed().Count() + 1; i++)
            {
                var row = ws.Row(i);
                var cells = row.CellsUsed();
                var login = row.FirstCell().Value.ToString();

                if (login.Equals("0") || login.Equals(string.Empty)) return;

                var client = Clients.Items.First(x => x.Data.Login.Equals(login));
                if (client != null)
                {
                    GetStrategiesFromWorksheet(ws, cells, client);
                }
            }
        }

        /// <summary>
        /// Получение информации по стратегиям.
        /// </summary>
        /// <param name="ws">Книга биржи.</param>
        /// <param name="cells">Данные клиента.</param>
        /// <param name="client">Клиент.</param>
        private static void GetStrategiesFromWorksheet(IXLWorksheet ws, IXLCells cells, Client client)
        {
            foreach (var cell in cells)
            {
                if (cell.Address.ColumnLetter.Equals("A")) continue;

                var type = cell.Value.Type;

                if (type.Equals(XLDataType.Text))
                {
                    var address = cell.Address;
                    var code = ws.Row(1).Cell(address.ColumnNumber).Value.ToString();
                    var data = cell.Value.GetText().Split('_');
                    var limit = int.Parse(data[0]);
                    var payment = int.Parse(data[1]);
                    client.Data.Strategies.Add(new(ws.Name, code, limit, payment));
                }
            }
        }

        /// <summary>
        /// Создание подписки, срабатывающей при изменении количества клиентов.
        /// </summary>
        private void CreateSubscriptions()
        {
            Clients.Connect().Subscribe(OnClientsCountChanged);
        }

        /// <summary>
        /// <para> При обновлении коллекции создаются подписки на изменения информации клиента. </para>
        /// <para> Обрабатываемые изменения: Add - добавление одного клиента, 
        /// AddRange - добавление клиентов при инициации,
        /// Remove - удаление одного клиента.</para>
        /// </summary>
        /// <param name="changes">Изменения в коллекции</param>
        protected override void OnClientsCountChanged(IChangeSet<Client> changes)
        {
            lock (_locker)
            {
                foreach (var change in changes)
                {
                    if (change.Reason.Equals(ListChangeReason.Add))
                    {
                        var item = change.Item.Current;
                        Logger.AddLog(Logs, $"added client {item.Data.Login}");
                        CreateClientSubscriptions(item);
                    }
                    else if (change.Reason.Equals(ListChangeReason.AddRange))
                    {
                        foreach (var item in change.Range)
                        {
                            Logger.AddLog(Logs, $"added client {item.Data.Login}");
                            CreateClientSubscriptions(item);
                        }
                    }
                    else if (change.Reason.Equals(ListChangeReason.Remove)) {
                        var item = change.Item.Current;
                        Logger.AddLog(Logs, $"removed client {item.Data.Login}");
                    }
                }
            }
        }


        /// <summary>
        /// Создает подписки на изменения Deposit, Payment и Strategies клиента.
        /// </summary>
        /// <param name="client">Клиент.</param>
        private void CreateClientSubscriptions(Client client)
        {
            client.WhenAnyValue(x => x.Data.Deposit)
                .Skip(1)
                .Subscribe(deposit => OnDepositChanged(client, deposit));
            client.WhenAnyValue(x => x.Data.Payment)
                .Skip(1)
                .Subscribe(payment => OnPaymentChanged(client, payment));
            client.Data.Strategies.Connect()
                .Subscribe(changes => OnStrategiesCountChanged(changes, client));
        }

        /// <summary>
        /// Обработчик изменения Deposit клиента. Если клиента нет в базе,
        /// в конец книги Main добавляется строка с новым клиентом. Если клиент
        /// есть в базе, то изменяется только поле Deposit.
        /// </summary>
        /// <param name="client">Клиент.</param>
        /// <param name="deposit">Новое значение Deposit.</param>
        protected override void OnDepositChanged(Client client, int deposit)
        {
            lock (_locker)
            {
                var main = _workbook.Worksheet("Main");

                var firstRow = main.FirstRowUsed();
                var lastRow = main.LastRowUsed();
                if (firstRow == null || lastRow == null) return;
                
                var address = lastRow.RangeAddress.FirstAddress.RowNumber;

                var index = FindClientIndex(client.Telegram.Id);
                if (index > 0)
                {
                    main.Row(index).Cell(GetCellByName(firstRow, "Deposit")).Value = client.Data.Deposit;
                }
                else
                {
                    address++;
                    main.Row(address).Cell(GetCellByName(firstRow, "Login")).Value = client.Data.Login;
                    main.Row(address).Cell(GetCellByName(firstRow, "Password")).Value = client.Data.Password;
                    main.Row(address).Cell(GetCellByName(firstRow, "ID")).Value = client.Telegram.Id;
                    main.Row(address).Cell(GetCellByName(firstRow, "Deposit")).Value = client.Data.Deposit;
                    main.Row(address).Cell(GetCellByName(firstRow, "Payment")).Value = client.Data.Payment;
                    main.Row(address).Cell(GetCellByName(firstRow, "State")).Value = client.Telegram.State.ToString();
                    main.Row(address).Cell(GetCellByName(firstRow, "Stage")).Value = client.Telegram.Stage.ToString();
                }

                _workbook.Save();
            }
        }

        /// <summary>
        /// Обработчик изменения Payment клиента.
        /// </summary>
        /// <param name="client">Клиент.</param>
        /// <param name="payment">Новое значение Payment.</param>
        protected override void OnPaymentChanged(Client client, int payment)
        {
            lock (_locker)
            {
                var main = _workbook.Worksheet("Main");
                var firstRow = main.FirstRowUsed();

                if (firstRow == null) return;

                var index = FindClientIndex(client.Telegram.Id);
                if (index > 0)
                {
                    main.Row(index).Cell(GetCellByName(firstRow, "Payment")).Value = client.Data.Payment;
                    _workbook.Save();
                }
            }
        }

        /// <summary>
        /// <para> Обработчик изменения количества Strategy клиента. </para>
        /// Add - добавление стратегии, Remove - удаление стратегии.
        /// </summary>
        /// <param name="changes">Изменения.</param>
        /// <param name="client">Клиент.</param>
        protected override void OnStrategiesCountChanged(IChangeSet<StrategySummary> changes, Client client)
        {
            foreach (var change in changes)
            {
                if (change.Reason.Equals(ListChangeReason.Add))
                {
                    var item = change.Item.Current;
                    AddStrategyToClient(item, client);
                }
                else if (change.Reason.Equals(ListChangeReason.Remove))
                {
                    var item = change.Item.Current;
                    RemoveStrategyFromClient(item, client);
                }
            }
        }

        /// <summary>
        /// Добавляет в книгу биржи информацию по стратегии клиента.
        /// </summary>
        /// <param name="data">Добавляемая стратегия.</param>
        /// <param name="client">Клиент.</param>
        protected override void AddStrategyToClient(StrategySummary data, Client client)
        {
            lock (_locker)
            {
                var sheet = GetBurseSheet(data.Burse);
                if (sheet != null)
                {
                    var index = FindClientIndex(client.Telegram.Id);
                    if (index > 0)
                    {
                        var address = FindCellAdress(sheet, data.Code, index);
                        if (address != null)
                        {
                            client.Data.Payment += data.Payment;
                            client.Data.Deposit -= data.Payment;
                            var value = $"{data.TradeLimit}_{data.Payment}";
                            sheet.Cell(address).Value = value;
                            _workbook.Save();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Удаляет из книги биржи информацию по стратегии клиента.
        /// </summary>
        /// <param name="data">Удаляемая стратегия.</param>
        /// <param name="client">Клиент.</param>
        protected override void RemoveStrategyFromClient(StrategySummary data, Client client)
        {
            lock (_locker)
            {
                var sheet = GetBurseSheet(data.Burse);
                if (sheet != null)
                {
                    var index = FindClientIndex(client.Telegram.Id);
                    if (index > 0)
                    {
                        var address = FindCellAdress(sheet, data.Code, index);
                        if (address != null)
                        {
                            client.Data.Payment -= data.Payment;
                            sheet.Cell(address).Clear();
                            _workbook.Save();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Поиск номера строки в книге Main по ID клиента.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private int FindClientIndex(string id)
        {
            int index = 0;
            var main = _workbook.Worksheet("Main");

            var lastRow = main.LastRowUsed();
            var firstRow = main.FirstRowUsed();

            if (lastRow != null && firstRow != null)
            {
                var address = lastRow.RangeAddress.FirstAddress.RowNumber;

                for (int i = 2; i <= address; i++)
                {
                    var row = main.Row(i);
                    var clientId = row.Cell(GetCellByName(firstRow, "ID")).Value.ToString();
                    if (clientId.Equals(id))
                        index = i;
                }
            }
            return index;
        }

        /// <summary>
        /// Получение книги по имени.
        /// </summary>
        /// <param name="name">Имя книги.</param>
        /// <returns>Книга с именем name.</returns>
        private IXLWorksheet GetBurseSheet(string name)
        {
            var sheets = _workbook.Worksheets.Where(x => x.Name == name);
            if (sheets.Count() != 1)
            {
                throw new InvalidOperationException();
            }

            var sheet = sheets.First();
            return sheet;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sheet">Книга.</param>
        /// <param name="code">Код стратегии.</param>
        /// <param name="index">Номер строки клиента в книге.</param>
        /// <returns></returns>
        private static string FindCellAdress(IXLWorksheet sheet, string code, int index)
        {
            var address = string.Empty;
            var firstRow = sheet.FirstRowUsed();

            if (firstRow != null)
            { 
                var cells = firstRow.CellsUsed();

                foreach (var cell in cells)
                {
                    if (code.Equals(cell.Value.ToString()))
                        address = cell.Address.ColumnLetter + index;
                }
            }

            return address;
        }

        /// <summary>
        /// Получение номера столбца по имени.
        /// </summary>
        /// <param name="row">Строка с именами.</param>
        /// <param name="name">Имя для поиска.</param>
        /// <returns>Номер столбца</returns>
        private static int GetCellByName(IXLRow row, string name)
        {
            var column = 0;

            foreach (var cell in row.CellsUsed())
            {
                if (cell.GetString().Equals(name))
                {
                    column = cell.Address.ColumnNumber;
                    break;
                }
            }

            return column;
        }
    }
}
