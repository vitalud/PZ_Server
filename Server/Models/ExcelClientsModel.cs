using ClosedXML.Excel;
using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using Server.Service;
using Server.Service.Abstract;
using Server.Service.Enums;
using System.Reactive.Linq;

namespace Server.Models
{
    public class ExcelClientsModel : ClientsModel
    {
        private readonly XLWorkbook _workbook;
        public ExcelClientsModel()
        {
            _workbook = new XLWorkbook(_path);

            GetClients();
            CreateSubscriptions();
        }

        protected override void GetClients()
        {
            foreach (var ws in _workbook.Worksheets)
            {
                if (ws.Name.Equals("Main")) GetClientBase(ws);
                else GetStrategyData(ws);
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
        private void GetStrategyData(IXLWorksheet ws)
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
                        var value = cell.Value;
                        if (cell.Address.ColumnLetter != "A")
                        {
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
                }
            }
        }

        private void CreateSubscriptions()
        {
            Clients.Connect()
                .Subscribe(OnClientsCountChanged);
        }
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

        protected override void OnDepositChanged(Client client, int deposit)
        {
            lock (_locker)
            {
                var main = _workbook.Worksheet(1);
                var lastRow = main.LastRowUsed().RangeAddress.FirstAddress.RowNumber;

                var index = FindClientIndex(client.Telegram.Id);
                if (index > 0)
                {
                    main.Row(index).Cell(5).Value = client.Data.Deposit;
                    _workbook.Save();
                }
                else
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

        protected override void OnPaymentChanged(Client client, int payment)
        {
            lock (_locker)
            {
                var main = _workbook.Worksheet(1);
                var lastRow = main.LastRowUsed().RangeAddress.FirstAddress.RowNumber;

                var index = FindClientIndex(client.Telegram.Id);
                if (index > 0)
                {
                    main.Row(index).Cell(6).Value = client.Data.Payment;
                    _workbook.Save();
                }
            }
        }

        private IXLWorksheet GetBurseSheet(string name)
        {
            IXLWorksheet sheet = null;
            if (name.Equals("Okx")) sheet = _workbook.Worksheet(2);
            else if (name.Equals("Binance")) sheet = _workbook.Worksheet(3);
            else if (name.Equals("Bybit")) sheet = _workbook.Worksheet(4);
            else if (name.Equals("Quik")) sheet = _workbook.Worksheet(5);
            return sheet;
        }


        protected override void OnClientsCountChanged(IChangeSet<Client> changes)
        {
            lock (_locker)
            {
                foreach (var change in changes)
                {
                    if (change.Reason.Equals(ListChangeReason.Add))
                    {
                        var item = change.Item.Current;
                        Logger.AddLog(_clientLogs, $"added client {item.Data.Login}");
                        CreateClientSubscriptions(item);
                    }
                    if (change.Reason.Equals(ListChangeReason.AddRange))
                    {
                        foreach (var item in change.Range)
                        {
                            Logger.AddLog(_clientLogs, $"added client {item.Data.Login}");
                            CreateClientSubscriptions(item);
                        }
                    }
                }
            }
        }

        protected override void OnStrategiesCountChanged(IChangeSet<ShortStrategyInfo> changes, Client client)
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

        protected override void AddStrategyToClient(ShortStrategyInfo data, Client client)
        {
            lock (_locker)
            {
                client.Data.Deposit -= data.Payment;
                client.Data.Payment += data.Payment;

                var sheet = GetBurseSheet(data.Burse);
                if (sheet != null)
                {
                    var index = FindClientIndex(client.Telegram.Id);
                    if (index > 0)
                    {
                        var address = FindCellAdress(sheet, data.Code, index);
                        if (address != null)
                        {
                            var value = $"{data.TradeLimit}_{data.Payment}";
                            sheet.Cell(address).Value = value;
                            _workbook.Save();
                        }
                    }
                }
            }
        }

        protected override void RemoveStrategyFromClient(ShortStrategyInfo data, Client client)
        {
            lock (_locker)
            {
                client.Data.Payment -= data.Payment;

                var sheet = GetBurseSheet(data.Burse);
                if (sheet != null)
                {
                    var index = FindClientIndex(client.Telegram.Id);
                    if (index > 0)
                    {
                        var address = FindCellAdress(sheet, data.Code, index);
                        if (address != null)
                        {
                            sheet.Cell(address).Clear();
                            _workbook.Save();
                        }
                    }
                }
            }
        }

        private int FindClientIndex(string id)
        {
            int index = 0;
            var main = _workbook.Worksheet(1);
            var lastRow = main.LastRowUsed().RangeAddress.FirstAddress.RowNumber;
            bool result = false;

            for (int i = 2; i <= lastRow; i++)
            {
                var row = main.Row(i);
                var _ = row.Cell(4).Value.ToString();
                if (_.Equals(id))
                {
                    index = i;
                    result = true;
                }
            }
            return index;
        }
        private string FindCellAdress(IXLWorksheet sheet, string code, int index)
        {
            var address = string.Empty;
            var range = sheet.Range("A1:Z1").Cells();
            foreach (var cell in range)
            {
                if (code.Equals(cell.Value.ToString()))
                {
                    address = cell.Address.ColumnLetter + index;
                }
            }
            return address;
        }
    }
}
