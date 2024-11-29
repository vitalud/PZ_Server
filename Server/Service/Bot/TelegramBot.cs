using DynamicData;
using ProjectZeroLib;
using ReactiveUI;
using Server.Service.Abstract;
using Server.Service.Enums;
using System.Data;
using System.IO;
using System.Reactive.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.Service.Bot
{
    public class TelegramBot : ReactiveObject
    {
        private readonly ClientsModel _clientDataBase;
        private readonly Strategies _strategies;

        private readonly TelegramBotClient _bot;
        private readonly string _botToken;
        private readonly long _adminId;

        private readonly int offset = 600;
        private const int minimalPayment = 1000;

        private static readonly List<PaymentSystem> paymentSystems =
        [
            new("Sberbank", KeyEncryptor.ReadKeyFromFile("sber", "projectzero.txt")),
            new("Ukassa", KeyEncryptor.ReadKeyFromFile("ukassa", "projectzero.txt"))
        ];

        private readonly SourceList<string> _messages = new();
        public SourceList<string> Messages => _messages;

        private readonly SourceList<string> _errors = new();
        public SourceList<string> Errors => _errors;

        public TelegramBot(ClientsModel clientDataBase, Strategies strategies)
        {
            _clientDataBase = clientDataBase;
            _strategies = strategies;

            _adminId = long.Parse(KeyEncryptor.ReadKeyFromFile("admin", "projectzero.txt"));
            _botToken = KeyEncryptor.ReadKeyFromFile("telegram", "projectzero.txt");

            if (_botToken != null)
                _bot = new(_botToken);

            this.WhenAnyValue(x => x.Errors.Count).Subscribe(SendErrorMessage);
        }

        public void Start()
        {
            if (_bot != null)
                _bot.StartReceiving(Update, Error);
            else
                Logger.AddLog(_errors, "bot is null");
        }

        /// <summary>
        /// Обработка обновлений, приходящих в бот.
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="update"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task Update(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update != null)
            {
                if (!GetTimeSpanFromUpdates(update, offset))
                {
                    var client = GetClient(update);
                    var handler = update switch
                    {
                        { Message: { } message } => BotOnMessageReceived(message, client),
                        { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery, client),
                        { PreCheckoutQuery: { } preCheckoutQuery } => BotOnPreCheckoutQueryReceived(preCheckoutQuery, client),
                        _ => throw new NotImplementedException(),
                    };
                    await handler;
                }
            }
        }

        /// <summary>
        /// Обработка ошибок в боте.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="exception"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            var error = $"{DateTime.Now}: {exception.Message}\n{exception.Source}\n{exception.StackTrace}";
            Logger.AddLog(_errors, error);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Отправка сообщения об ошибке в чат по adminId.
        /// </summary>
        /// <param name="count"></param>
        private void SendErrorMessage(int count)
        {
            if (count > 0)
            {
                _bot.SendTextMessageAsync(
                    chatId: _adminId,
                    text: $"@VitalUd\nОшибка:\n\n{Errors.Items[Errors.Items.Count - 1]}");
            }
        }

        /// <summary>
        /// Отправка сообщения при аварийном выходе.
        /// </summary>
        /// <param name="count"></param>
        public async Task SendShutdownErrorMessage()
        {
            await _bot.SendTextMessageAsync(
                    chatId: _adminId,
                    text: $"@VitalUd, я умер.");
        }

        /// <summary>
        /// Обработчик обновлений типа Message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task BotOnMessageReceived(Message message, Client client)
        {
            if (message != null)
            {
                if (message.Text != null)
                {
                    Logger.AddLog(_messages, $"{message.Chat.Username} Message: {message.Text}");
                    if (message.Text.StartsWith('/')) 
                        await CommandMessageHandler(message, client);
                    else 
                        await MessageHandler(message, client);
                }
                else
                {
                    if (message.Type.Equals(MessageType.SuccessfulPayment))
                    {
                        await MessageHandler(message, client);
                    }
                }
            }
        }

        /// <summary>
        /// <para>Обработка команд.</para>
        /// /strategies - присылает сообщение с выбором биржи и переводит состояние клиента в Strategy.<br/>
        /// /search - предварительно очищает массив временных стратегий и переводит состояние клиента в Search.<br/>
        /// /info - отсылает сообщение с информацией об аккаунте и переводит состояние клиента в Zero.<br/>
        /// /deposit - отсылает сообщение с запросом суммы пополнения и переводит состояние клиента в Deposit.<br/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task CommandMessageHandler(Message message, Client client)
        {
            if (message.Text.Equals("/strategies"))
            {
                client.Telegram.Stage = Stage.Strategy;
                string[] names = TelegramMenuService.StrategiesMenu.Select(x => x.Name).ToArray();
                InlineKeyboardMarkup inlineKeyboard = TelegramMenuService.GenerateButtons(names, buttonsPerRow: 1, callback: "burse_name_");
                await _bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Выберите биржу:",
                    replyMarkup: inlineKeyboard);
                client.Telegram.Temp.Strategies.Clear();
            }
            else if (message.Text.Equals("/search"))
            {
                client.Telegram.Stage = Stage.Search;
                client.Telegram.Temp.Strategies.Clear();
                await _bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Введите номер стратегии:");
            }
            else if (message.Text.Equals("/info"))
            {
                client.Telegram.Stage = Stage.Zero;
                await _bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"ID: {client.Telegram.Id}\n" +
                          $"Логин: {client.Data.Login}\n" +
                          $"Пароль: {client.Data.Password}\n" +
                          $"Депозит: {client.Data.Deposit} руб.\n" +
                          $"Платеж: {client.Data.Payment} руб./день\n" +
                          $"Стратегии:");
                await SendStrategyFromInfo(client, message);
            }
            else if (message.Text.Equals("/deposit"))
            {
                client.Telegram.Stage = Stage.Deposit;
                await _bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"Введите сумму для пополнения:");
            }
        }

        /// <summary>
        /// <para>Обработка сообщений в зависимости от состояния клиента.</para>
        /// Zero - информация об аккаунте.<br/>
        /// Strategy - обработка отсутствует, управление в этом состоянии только кнопками.<br/>
        /// Search - поиск стратегии по номеру и перевод в состояние Strategy.<br/>
        /// Deposit - проверка введенной суммы.<br/>
        /// Payment - проверка оплаты.<br/>
        /// Payment - проверка лимита.<br/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task MessageHandler(Message message, Client client)
        {
            if (client.Telegram.Stage.Equals(Stage.Search))
            {
                GetStrategy(client, message);
                client.Telegram.Stage = Stage.Strategy;
            }
            else if (client.Telegram.Stage.Equals(Stage.Deposit)) 
                await GetDeposit(client, message);
            else if (client.Telegram.Stage.Equals(Stage.Payment))
            {
                if (message.SuccessfulPayment != null) 
                    await PostPayment(client, message);
            }
            else if (client.Telegram.Stage.Equals(Stage.Limit)) 
                await GetLimit(client, message);
        }

        /// <summary>
        /// Обработчик обновлений типа CallbackQuery.
        /// </summary>
        /// <param name="callbackQuery"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery, Client client)
        {
            if (callbackQuery != null)
            {
                if (callbackQuery.Data != null)
                {
                    if (callbackQuery.Message != null) Logger.AddLog(Messages, $"{callbackQuery.Message.Chat.Username} Query: {callbackQuery.Data}");
                    var data = callbackQuery.Data.Split('_');
                    await CallbackHandler(callbackQuery, client, data);
                }
            }
        }

        /// <summary>
        /// <para>Обработка нажатий кнопок в зависимости от состояния клиента.</para>
        /// Zero - просмотр стратегий в инфо аккаунта с возможностью отписки.<br/>
        /// Strategy - просмотр стратегий по выбору биржи, типа или подтипа.<br/>
        /// Search - при поиске отсутствуют какие-либо кнопки, при нахождении стратегии из списка переводит состояние клиента в Strategies<br/>
        /// Deposit - обработка отсутствует.<br/>
        /// Payment - проверка оплаты.<br/>
        /// Limit - проверка лимита.<br/>
        /// </summary>
        /// <param name="callbackQuery"></param>
        /// <param name="client"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task CallbackHandler(CallbackQuery callbackQuery, Client client, string[] data)
        {
            if (data[0].Equals("move"))
            {
                if (data[1].Equals(TelegramMenuService.GetCallbackString(Callback.Next)) ||
                    data[1].Equals(TelegramMenuService.GetCallbackString(Callback.Previous)))
                    await TelegramMenuService.MoveButton(_bot, client, callbackQuery, data[1]);
            }

            if (client.Telegram.Stage.Equals(Stage.Zero))
            {
                if (data[0].Equals(TelegramMenuService.GetCallbackString(Callback.Unsub)))
                    await UnsubscribeFromStrategy(client, callbackQuery);
            }
            else if (client.Telegram.Stage.Equals(Stage.Strategy))
            {
                if (data[0].Equals("burse"))
                {
                    if (data[1].Equals(TelegramMenuService.GetCallbackString(Callback.Name)))
                        await SendTypes(client, callbackQuery, data[2]);
                    else if (data[1].Equals(TelegramMenuService.GetCallbackString(Callback.Type)))
                        await SendSubtypes(client, callbackQuery, data[2]);
                    else if (data[1].Equals(TelegramMenuService.GetCallbackString(Callback.Subtype)))
                        await SendStrategy(client, callbackQuery, data[2]);
                }
                else if (data[0].Equals(TelegramMenuService.GetCallbackString(Callback.Sub)))
                    await SubscribeToStrategy(client, callbackQuery);
            }
            else if (client.Telegram.Stage.Equals(Stage.Payment))
            {
                if (callbackQuery.Data != "back") 
                    await GetPayment(client, callbackQuery);
            }
            else if (client.Telegram.Stage.Equals(Stage.Limit))
                await GetSubscription(client, callbackQuery);
        }

        /// <summary>
        /// Обработчик обновлений типа PreCheckoutQuery.
        /// </summary>
        /// <param name="preCheckoutQuery"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task BotOnPreCheckoutQueryReceived(PreCheckoutQuery preCheckoutQuery, Client client)
        {
            Logger.AddLog(_messages, $"{preCheckoutQuery.From.Username} PreCheckoutQuery: {preCheckoutQuery.Currency}");
            if (client.Telegram.Stage.Equals(Stage.Payment)) 
                await _bot.AnswerPreCheckoutQueryAsync(preCheckoutQuery.Id);
        }

        /// <summary>
        /// Редактирует сообщение с выбором биржи и меняет на выбор типа этой биржи.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <param name="burse">Имя выбранной биржи</param>
        /// <returns></returns>
        private async Task SendTypes(Client client, CallbackQuery query, string burse)
        {
            if (query.Data != null)
            {
                var _ = TelegramMenuService.StrategiesMenu.Find(x => x.Name.Equals(burse));
                if (_ != null)
                {
                    var types = _.Types.Select(x => x.Code).ToArray();
                    if (types.Length != 0)
                    {
                        client.Telegram.Temp.Burse = burse;
                        InlineKeyboardMarkup inlineKeyboard = TelegramMenuService.GenerateButtons(types, buttonsPerRow: 1, callback: "burse_type_");
                        await _bot.EditMessageTextAsync(
                            chatId: query.Message.Chat.Id,
                            messageId: query.Message.MessageId,
                            text: $"Выберите тип:",
                            replyMarkup: inlineKeyboard);
                    }
                }
            }
        }

        /// <summary>
        /// Редактирует сообщение с выбором типа биржи и меняет на выбор подтипа типа этой биржи.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <param name="type">Имя выбранного типа</param>
        /// <returns></returns>
        private async Task SendSubtypes(Client client, CallbackQuery query, string type)
        {
            if (query != null)
            {
                var subtypes = TelegramMenuService.StrategiesMenu.SelectMany(x => x.Types).First(x => x.Name.Equals(type)).Subtypes;
                if (subtypes.Length != 0)
                {
                    InlineKeyboardMarkup inlineKeyboard = TelegramMenuService.GenerateButtons(subtypes, buttonsPerRow: 1, callback: "burse_subtype_");
                    await _bot.EditMessageTextAsync(
                        chatId: query.Message.Chat.Id,
                        messageId: query.Message.MessageId,
                        text: $"Выберите подтип:",
                        replyMarkup: inlineKeyboard);
                }
            }
        }

        /// <summary>
        /// Формирование временного списка стратегий TempStrategies по выбранным клиентом бирже, типу и подтипу с сортировкой по PL.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <param name="subtype"></param>
        /// <returns></returns>
        private async Task SendStrategy(Client client, CallbackQuery query, string subtype)
        {
            var strategies = _strategies.StrategiesList.Items.Where(x => (x.Name.ToString(), x.Telegram.Subtype).Equals((client.Telegram.Temp.Burse, subtype))).ToList();
            if (strategies.Count != 0) 
                client.Telegram.Temp.Strategies = strategies;
            else return;

            client.Telegram.Temp.Strategies.Sort((a, b) => b.Telegram.Pl.CompareTo(a.Telegram.Pl));
            client.Telegram.Index = 0;
            client.Telegram.Lenght = client.Telegram.Temp.Strategies.Count;

            InlineKeyboardMarkup inlineKeyboard = TelegramMenuService.GenerateStrategiesSubButtons(TelegramMenuService.GetCallbackString(Callback.Sub), "Подписаться", client);

            var _ = client.Telegram.Temp.Strategies[0];
            using (var stream = new FileStream(_.Telegram.ImagePath, FileMode.Open, FileAccess.Read))
            {
                var inputFile = InputFile.FromStream(stream, Path.GetFileName(_.Telegram.ImagePath));

                await _bot.SendPhotoAsync(
                    chatId: query.Message.Chat.Id,
                    photo: inputFile,
                    caption: $"Стратегия: {_.Code} \n" +
                    $"Описание: {_.Telegram.Description} \n" +
                    $"PL: {_.Telegram.Pl} \n" +
                    $"Минимальный торговый лимит: {_.Telegram.Limit} руб.",
                    replyMarkup: inlineKeyboard);
            }
        }

        /// <summary>
        /// Подписывается на стратегию.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task SubscribeToStrategy(Client client, CallbackQuery query)
        {
            if (query != null)
            {
                if (query.Message != null)
                {
                    GetTempInfo(client);
                    if (query.Message.Photo != null) client.Telegram.Temp.PhotoId = query.Message.Photo[1].FileId;

                    var strat = client.Data.Strategies.Items.FirstOrDefault(x => x.Code.Equals(client.Telegram.Temp.Code));
                    if (strat != null)
                    {
                        await _bot.SendTextMessageAsync(
                            chatId: query.Message.Chat.Id,
                            text: $"Подписка уже приобретена");
                    }
                    else
                    {
                        if (client.Data.Deposit < client.Telegram.Temp.Price * client.Data.Percentage / 100)
                        {
                            await _bot.SendTextMessageAsync(
                                chatId: query.Message.Chat.Id,
                                text: $"Недостаточно средств.\nЧтобы продолжить, введите сумму для пополнения:");
                            client.Telegram.Stage = Stage.Deposit;
                        }
                        else
                        {
                            await _bot.SendTextMessageAsync(
                                chatId: query.Message.Chat.Id,
                                text: $"Введите торговый лимит:");
                            client.Telegram.Stage = Stage.Limit;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Отписывается от стратегии.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task UnsubscribeFromStrategy(Client client, CallbackQuery query)
        {
            if (query != null)
            {
                if (query.Message != null)
                {
                    if (client.Data.Strategies.Count != 0)
                    {
                        var index = client.Telegram.Index;
                        var strat = client.Data.Strategies.Items[index];
                        if (strat != null)
                        {
                            await _bot.SendTextMessageAsync(
                                chatId: query.Message.Chat.Id,
                                text: $"Вы отписались от стратегии {strat.Code}");
                            Logger.UiInvoke(() => client.Data.Strategies.RemoveAt(index));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Получение одной стратегии по запросу через /search.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        private async void GetStrategy(Client client, Message message)
        {
            CheckChars(message.Text, out bool flag);
            if (flag) await _bot.SendTextMessageAsync(chatId: message.Chat.Id, text: "Некорректный номер, повторите ввод:");
            else
            {
                var strategy = _strategies.StrategiesList.Items.FirstOrDefault(x => x.Code.Equals(message.Text));
                if (strategy != null)
                {
                    client.Telegram.Temp.Strategies.Clear();
                    client.Telegram.Temp.Strategies.Add(strategy);
                    await GenerateStrategyMessage(strategy, client, message.Chat.Id);
                }
            }
        }

        /// <summary>
        /// Обработчик сообщения с суммой депозита при пополнении.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task GetDeposit(Client client, Message message)
        {
            CheckChars(message.Text, out bool flag);
            if (flag)
            {
                await _bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Некорректная сумма, повторите ввод:");
            }
            else
            {
                if (int.TryParse(message.Text, out int deposit))
                {
                    if (deposit < minimalPayment)
                    {
                        await _bot.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: "Минимальная сумма к пополнению - 1000 руб., повторите ввод:");
                    }
                    else
                    {
                        client.Telegram.Stage = Stage.Payment;
                        client.Telegram.Temp.Deposit = deposit;
                        string[] names = new string[paymentSystems.Count];
                        for (int i = 0; i < paymentSystems.Count; i++) names[i] = paymentSystems[i].Name.ToString();
                        InlineKeyboardMarkup inlineKeyboard = TelegramMenuService.GenerateButtons(names, buttonsPerRow: 1);
                        await _bot.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: $"Выберите способ оплаты:",
                            replyMarkup: inlineKeyboard);
                    }
                }
                else
                {
                    await _bot.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Некорректная сумма, повторите ввод:");
                }
            }
        }

        /// <summary>
        /// Обработчик выбора платежной системы при пополнении депозита, выставляет чек на оплату.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task GetPayment(Client client, CallbackQuery query)
        {
            if (query != null) {
                if (client.Telegram.Temp.Deposit != 0)
                {
                    List<LabeledPrice> price =
                    [
                        new LabeledPrice("Сумма к оплате", client.Telegram.Temp.Deposit * 100)
                    ];
                    PaymentSystem system = paymentSystems.Find(x => x.Name.Equals(query.Data));
                    if (system != null)
                    {
                        await _bot.SendInvoiceAsync(
                                    chatId: query.Message!.Chat.Id,
                                    title: "Чек",
                                    description: "Пополнение личного счета",
                                    payload: "test",
                                    providerToken: system.Token,
                                    currency: "RUB",
                                    prices: price,
                                    photoUrl: system.photoUrl,
                                    replyMarkup: TelegramMenuService.GeneratePaymentButtons());
                    }
                }
            }
        }

        /// <summary>
        /// Обработчик сообщения об успешной оплате. 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task PostPayment(Client client, Message message)
        {
            if (client.Telegram.State.Equals(State.Neutral))
            {
                client.Telegram.State = State.Active;
                client.Data.Password = GetPass(10);
            }

            client.Data.Deposit += client.Telegram.Temp.Deposit;
            Logger.AddLog(_clientDataBase.ClientLogs, $"client {client.Data.Login} is Active now");
            client.Telegram.Temp.Deposit = 0;

            if (client.Telegram.Temp.Code != null)
            {
                var strat = client.Telegram.Temp.Strategies[client.Telegram.Index];

                using (var stream = new FileStream(strat.Telegram.ImagePath, FileMode.Open, FileAccess.Read))
                {
                    var inputFile = InputFile.FromStream(stream, Path.GetFileName(strat.Telegram.ImagePath));

                    await _bot.SendPhotoAsync(
                        chatId: message.Chat.Id,
                        photo: inputFile,
                        caption: $"Стратегия: {strat.Code} \n" +
                        $"Описание: {strat.Telegram.Description}\n" +
                        $"Минимальный торговый лимит: {strat.Telegram.Limit} руб.");

                    await _bot.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"Введите торговый лимит:");
                    client.Telegram.Stage = Stage.Limit;
                }

            }
            else
            {
                client.Telegram.Stage = Stage.Zero;
            }
        }

        /// <summary>
        /// Обработчик сообщения с суммой лимита на стратегию.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task GetLimit(Client client, Message message)
        {
            if (message != null)
            {
                CheckChars(message.Text, out bool flag);
                if (flag)
                {
                    await _bot.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Некорректная сумма, повторите ввод:");
                }
                else
                {
                    int limit = int.Parse(message.Text);
                    if (limit < client.Telegram.Temp.Price)
                    {
                        await _bot.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: "Введенный лимит меньше минимального, повторите ввод:");
                    }
                    else if ((int)(limit * 0.17 / 100) > client.Data.Deposit)
                    {
                        await _bot.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: $"Недостаточно средств (баланс - {client.Data.Deposit}), пополните счет:");
                    }
                    else
                    {
                        client.Telegram.Temp.Limit = limit;
                        int com = (int)(client.Telegram.Temp.Limit * client.Data.Percentage / 100);
                        await _bot.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: $"Подтвердите выбор:\n" +
                            $"Стратегия: {client.Telegram.Temp.Code}.\n" +
                            $"Торговый лимит: {client.Telegram.Temp.Limit}.\n" +
                            $"Комиссия: {com + 1} руб/день.",
                            replyMarkup: TelegramMenuService.GenerateConfirmationButtons());
                    }
                }
            }
        }

        /// <summary>
        /// Обработчик сообщения при окончательном выборе подписки.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task GetSubscription(Client client, CallbackQuery query)
        {
            //Подтверждение покупки 
            if (query.Data.Equals(TelegramMenuService.GetCallbackString(Callback.Correct)))
            {
                int payment = (int)(client.Telegram.Temp.Limit * client.Data.Percentage / 100) + 1;
                Logger.UiInvoke(() => client.Data.Strategies.Add(new(client.Telegram.Temp.Burse, client.Telegram.Temp.Code, client.Telegram.Temp.Limit, payment)));
                //client.Data.Payment += payment;
                //client.Data.Deposit -= payment;

                client.Telegram.Index = 0;
                await _bot.SendTextMessageAsync(
                    chatId: query.Message.Chat.Id,
                    text: $"Подписка приобретена",
                    replyMarkup: TelegramMenuService.GenerateContinueButtons());
            }
            //Изменение данных по лимиту
            else if (query.Data.Equals(TelegramMenuService.GetCallbackString(Callback.Edit)))
            {
                await _bot.SendTextMessageAsync(
                    chatId: query.Message.Chat.Id,
                    text: $"Введите торговый лимит:");
            }
            //Завершение работы с выбранным списком подписок
            else if (query.Data.Equals(TelegramMenuService.GetCallbackString(Callback.Done)))
            {
                client.Telegram.Temp.Limit = 0;
                client.Telegram.Temp.Deposit = 0;
                client.Telegram.Temp.Price = 0;
                client.Telegram.Temp.Code = null;
                client.Telegram.Temp.PhotoId = null;
                client.Telegram.Temp.Strategies.Clear();
                client.Telegram.Stage = Stage.Zero;
            }
            //Возврат к просмотру отобранных подписок
            else if (query.Data.Equals(TelegramMenuService.GetCallbackString(Callback.Back)))
            {
                client.Telegram.Stage = Stage.Strategy;
                var strat = client.Telegram.Temp.Strategies[0];
                await GenerateStrategyMessage(strat, client, query.Message.Chat.Id);
            }
        }

        /// <summary>
        /// Отсылает сообщение с информацией о приобретенной подписке.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task SendStrategyFromInfo(Client client, Message message)
        {
            if (client.Data.Strategies.Count.Equals(0)) return;
            else
            {
                client.Telegram.Index = 0;
                var _ = client.Data.Strategies.Items[client.Telegram.Index];
                var strat = _strategies.StrategiesList.Items.FirstOrDefault(x => x.Code.Equals(_.Code));
                if (strat != null)
                {
                    using (var stream = new FileStream(strat.Telegram.ImagePath, FileMode.Open, FileAccess.Read))
                    {
                        var inputFile = InputFile.FromStream(stream, Path.GetFileName(strat.Telegram.ImagePath));

                        await _bot.SendPhotoAsync(
                                chatId: message.Chat.Id,
                                photo: inputFile,
                                caption: $"Стратегия: {_.Code} \n" +
                                $"Торговый лимит: {_.TradeLimit} руб. \n" +
                                $"Тариф: {_.Payment} руб./день",
                                replyMarkup: TelegramMenuService.GenerateStrategiesSubButtons(TelegramMenuService.GetCallbackString(Callback.Unsub), "Отписаться", client));
                    }
                }
            }
        }

        /// <summary>
        /// Поиск клиента в базе по ID в зависимости от типа update. Если клиента в базе нет, возвращает нового клиента без добавления в базу.
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        private Client GetClient(Update update)
        {
            string id = string.Empty;
            if (update.Type.Equals(UpdateType.Message)) id = update.Message.From.Id.ToString();
            else if (update.Type.Equals(UpdateType.CallbackQuery)) id = update.CallbackQuery.From.Id.ToString();
            else if (update.Type.Equals(UpdateType.PreCheckoutQuery)) id = update.PreCheckoutQuery.From.Id.ToString();
            else if (update.Type.Equals(UpdateType.MyChatMember)) id = update.MyChatMember.From.Id.ToString();

            var client = _clientDataBase.Clients.Items.FirstOrDefault(x => x.Telegram.Id.Equals(id));

            if (client == null)
            {
                if (update.Type.Equals(UpdateType.Message))
                {
                    client = new Client()
                    {
                        Telegram = new(update.Message.From.Id.ToString()),
                        Data = new(update.Message.From.Username)
                    };
                    Logger.UiInvoke(() => _clientDataBase.Clients.Add(client));
                }
                else if (update.Type.Equals(UpdateType.CallbackQuery))
                {
                    client = new Client()
                    {
                        Telegram = new(update.CallbackQuery.From.Id.ToString()),
                        Data = new(update.CallbackQuery.From.Username)
                    };
                    Logger.UiInvoke(() => _clientDataBase.Clients.Add(client));
                }
            }
            return client;
        }

        /// <summary>
        /// Создание пароля из случайных символов (цифры/буквы).
        /// </summary>
        /// <param name="x">Количество случайных символов.</param>
        /// <returns></returns>
        private static string GetPass(int x)
        {
            string pass = "";
            var r = new Random();
            while (pass.Length < x)
            {
                char c = (char)r.Next(33, 125);
                if (char.IsLetterOrDigit(c))
                    pass += c;
            }
            return pass;
        }

        /// <summary>
        /// Проверка сообщения на наличие не цифр.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="flag"></param>
        private static void CheckChars(string text, out bool flag)
        {
            flag = false;
            if (!string.IsNullOrEmpty(text))
            {
                var chars = text.ToCharArray();
                foreach (char ch in chars)
                {
                    if (!char.IsDigit(ch))
                    {
                        flag = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Генерирует сообщение с информацией по стратегии при просмотре из каталога.
        /// </summary>
        /// <param name="strategy"></param>
        /// <param name="client"></param>
        /// <param name="chatId"></param>
        /// <returns></returns>
        private async Task GenerateStrategyMessage(Strategy strategy, Client client, ChatId chatId)
        {
            var caption =
                $"Стратегия: {strategy.Code} \n" +
                $"Описание: {strategy.Telegram.Description} \n" +
                $"PL: {strategy.Telegram.Pl} \n" +
                $"Минимальный торговый лимит: {strategy.Telegram.Limit} руб.";

            using (var stream = new FileStream(strategy.Telegram.ImagePath, FileMode.Open, FileAccess.Read))
            {
                var inputFile = InputFile.FromStream(stream, Path.GetFileName(strategy.Telegram.ImagePath));

                await _bot.SendPhotoAsync(
                    chatId: chatId,
                    photo: inputFile,
                    caption: caption,
                    replyMarkup: TelegramMenuService.GenerateStrategiesSubButtons(TelegramMenuService.GetCallbackString(Callback.Sub), "Подписаться", client));

            }
        }

        /// <summary>
        /// При нажатии кнопки Подписаться собирается информация о Коде и Цене из выбранной подписки.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="text"></param>
        private static void GetTempInfo(Client client)
        {
            var temp = client.Telegram.Temp;
            temp.Code = temp.Strategies[client.Telegram.Index].Code;
            temp.Price = temp.Strategies[client.Telegram.Index].Telegram.Limit;
        }

        /// <summary>
        /// Проверка на дату входящего обновления. При запуске бота отсеивает обновления, пришедшие ранее offset.
        /// </summary>
        /// <param name="update">Обновление от бота.</param>
        /// <param name="offset">Время в секундах, относительно которого отсеиваются обновления.</param>
        /// <returns></returns>
        private static bool GetTimeSpanFromUpdates(Update update, int offset)
        {
            bool skip = false;
            DateTime time = new();
            if (update.Type.Equals(UpdateType.Message))
            {
                if (update.Message != null)
                {
                    if (update.Message.EditDate != null) 
                        time = (DateTime)update.Message.EditDate;
                    else time = update.Message.Date;
                }
            }
            else if (update.Type.Equals(UpdateType.CallbackQuery))
            {
                if (update.CallbackQuery != null)
                {
                    if (update.CallbackQuery.Message != null)
                    {
                        if (update.CallbackQuery.Message.EditDate != null) 
                            time = (DateTime)update.CallbackQuery.Message.EditDate;
                        else time = update.CallbackQuery.Message.Date;
                    }
                }
            }
            else if (update.Type.Equals(UpdateType.PreCheckoutQuery))
            {
                return skip;
            }
            var times = DateTime.UtcNow - time;
            if (times.TotalSeconds > offset) skip = true;
            return skip;
        }
    }
}
