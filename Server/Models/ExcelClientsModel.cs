using ClosedXML.Excel;
using DynamicData;
using ProjectZeroLib;
using Server.Service;
using Server.Service.Abstract;
using Server.Service.Enums;

namespace Server.Models
{
    public class ExcelClientsModel : ClientsModel
    {
        private readonly XLWorkbook _workbook;
        public ExcelClientsModel()
        {
            _workbook = new XLWorkbook(_path);
            GetClients();
        }

        protected override void GetClients()
        {
            foreach (var ws in _workbook.Worksheets)
            {
                if (ws.Name.Equals("Main")) GetClientBase(ws);
                else UpdateClientBase(ws);
            }
        }
        private void GetClientBase(IXLWorksheet ws)
        {
            for (int i = 2; i <= ws.RowsUsed().Count(); i++)
            {
                var row = ws.Row(i);
                Clients.Add(new Client()
                {
                    Data = new(row.Cell(1).Value.ToString())
                    {
                        Password = row.Cell(2).Value.ToString(),
                        Deposit = int.Parse(row.Cell(5).Value.ToString()),
                        Payment = int.Parse(row.Cell(6).Value.ToString()),
                    },
                    Telegram = new(row.Cell(4).Value.ToString())
                    {
                        Id = row.Cell(4).Value.ToString(),
                        State = (State)Enum.Parse(typeof(State), row.Cell(7).Value.ToString()),
                        Stage = (Stage)Enum.Parse(typeof(Stage), row.Cell(8).Value.ToString())
                    }
                });
            }
        }
        private void UpdateClientBase(IXLWorksheet ws)
        {
            for (int i = 2; i <= ws.RowsUsed().Count() + 1; i++)
            {
                var row = ws.Row(i);
                var cells = row.CellsUsed();
                var login = row.FirstCell().Value.ToString();
                if (login == "0" || login == "") return;
                Client client = Clients.Items.First(x => x.Data.Login == login);
                if (client != null)
                {
                    foreach (var cell in cells)
                    {
                        var type = cell.Value.Type;
                        if (type != XLDataType.Text)
                        {
                            var address = cell.Address;
                            var code = ws.Row(1).Cell(address.ColumnNumber).Value.ToString();
                            var limit = (int)cell.Value.GetNumber();
                            client.Data.Strategies.Add(new(code, limit));
                        }
                    }
                }
            }
        }
        protected override void OnDepositChanged(Client client, int deposit)
        {
            var main = _workbook.Worksheet(1);
            var lastRow = main.LastRowUsed().RangeAddress.FirstAddress.RowNumber;
            if (FindClient(client, main, lastRow, out int index))
            {
                lock (main)
                {
                    main.Row(index).Cell(5).Value = client.Data.Deposit;
                    _workbook.Save();
                }
            }
            else
            {
                lock (main)
                {
                    main.Row(lastRow + 1).Cell(1).Value = client.Data.Login;
                    main.Row(lastRow + 1).Cell(2).Value = client.Data.Password;
                    main.Row(lastRow + 1).Cell(4).Value = client.Telegram.Id;
                    main.Row(lastRow + 1).Cell(5).Value = client.Data.Deposit;
                    main.Row(lastRow + 1).Cell(6).Value = client.Data.Payment;
                    main.Row(lastRow + 1).Cell(7).Value = client.Telegram.State.ToString();
                    main.Row(lastRow + 1).Cell(8).Value = client.Telegram.Stage.ToString();
                    _workbook.Save();
                }
            }
        }
        protected override void OnPaymentChanged(Client client, int deposit)
        {
            var main = _workbook.Worksheet(1);
            var lastRow = main.LastRowUsed().RangeAddress.FirstAddress.RowNumber;
            if (FindClient(client, main, lastRow, out int index))
            {
                lock (main)
                {
                    main.Row(index).Cell(6).Value = client.Data.Payment;
                    _workbook.Save();
                }
            }
        }
        protected override void OnStrategiesChanged(Client client, int count, int oldCount)
        {
            var main = _workbook.Worksheet(1);
            var lastRow = main.LastRowUsed().RangeAddress.FirstAddress.RowNumber;
            if (FindClient(client, main, lastRow, out int index))
            {
                GetBurseSheet(client, out IXLWorksheet? burseSheet);
                if (burseSheet != null)
                {
                    lock (main)
                    {
                        if (count > oldCount)
                        {
                            ChangeSubscriptionData(main, burseSheet, client, index, client.Telegram.Temp.Limit.ToString());
                            client.Telegram.Temp.Limit = 0;
                            client.Telegram.Temp.Deposit = 0;
                            client.Telegram.Temp.Price = 0;
                            client.Telegram.Temp.Code = string.Empty;
                            client.Telegram.Temp.PhotoId = string.Empty;
                        }
                        else
                        {
                            ChangeSubscriptionData(main, burseSheet, client, index, string.Empty);
                        }
                    }
                }
            }
        }

        private bool FindClient(Client client, IXLWorksheet main, int lastRow, out int index)
        {
            index = 0;
            bool result = false;
            GetBurseSheet(client, out IXLWorksheet? burseSheet);
            if (burseSheet != null)
            {
                for (int i = 2; i <= lastRow; i++)
                {
                    var row = main.Row(i);
                    var id = row.Cell(4).Value.ToString();
                    if (id == client.Telegram.Id)
                    {
                        index = i;
                        result = true;
                    }
                }
            }
            return result;
        }
        private void ChangeSubscriptionData(IXLWorksheet main, IXLWorksheet burseSheet, Client client, int index, string value)
        {
            main.Row(index).Cell(6).Value = client.Data.Payment;
            var range = burseSheet.Range("A1:Z1").Cells();
            foreach (var cell in range)
            {
                if (client.Telegram.Temp.Code == cell.Value.ToString())
                {
                    var address = (cell.Address.ColumnLetter + index).ToString();
                    burseSheet.Cell(address).Value = value;
                    _workbook.Save();
                }
            }
        }
        private void GetBurseSheet(Client client, out IXLWorksheet? burseSheet)
        {
            burseSheet = null;
            var burse = client.Telegram.Temp.Burse;
            if (burse != null)
            {
                if (client.Telegram.Temp.Burse.Equals("Okx")) burseSheet = _workbook.Worksheet(2);
                else if (client.Telegram.Temp.Burse.Equals("Binance")) burseSheet = _workbook.Worksheet(3);
                else if (client.Telegram.Temp.Burse.Equals("Bybit")) burseSheet = _workbook.Worksheet(4);
                else if (client.Telegram.Temp.Burse.Equals("Quik")) burseSheet = _workbook.Worksheet(5);
            }
        }

        protected override void OnTradeLimitChanged(ShortStrategyInfo item, int limit, string login)
        {
            Logger.AddLog(_clientLogs, $"{login}: {item.Code} - {item.TradeLimit}");
        }
    }
}
