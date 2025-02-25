using DynamicData;
using ProjectZeroLib.Utils;
using ReactiveUI;
using Server.Service.Abstract;
using Server.Service.Enums;
using Server.Service.UserClient;
using Strategies.Strategies;
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
    /// <summary>
    /// Чат бот в телеграме, служащий магазином подписок и взаимодействующий с клиентской базой.
    /// </summary>
    public partial class TelegramBot : ReactiveObject
    {
        private readonly ClientsModel _clientDataBase;
        private readonly StrategiesRepository _strategies;

        private readonly TelegramBotClient _bot;
        private readonly string _botToken;
        private readonly long _adminId;

        private readonly int offset = 600;
        private const int minimalPayment = 1000;

        private static readonly List<PaymentSystem> paymentSystems =
        [
            new("Sberbank", KeyReader.ReadKeyFromFile("sber", "projectzero.txt")),
            new("Ukassa", KeyReader.ReadKeyFromFile("ukassa", "projectzero.txt"))
        ];

        private readonly SourceList<string> _messages = new();
        private readonly SourceList<string> _errors = new();

        public SourceList<string> Messages => _messages;
        public SourceList<string> Errors => _errors;

        public ClientsModel Clients => _clientDataBase;
        public StrategiesRepository Strategies => _strategies;

        public TelegramBot(ClientsModel clientDataBase, StrategiesRepository strategies)
        {
            _clientDataBase = clientDataBase;
            _strategies = strategies;

            _adminId = long.Parse(KeyReader.ReadKeyFromFile("admin", "projectzero.txt"));
            _botToken = KeyReader.ReadKeyFromFile("telegram", "projectzero.txt");

            _bot = new(_botToken);
        }

        /// <summary>
        /// Запускает чат бота.
        /// </summary>
        public void Start()
        {
            _bot.StartReceiving(Update, Error);
        }

        /// <summary>
        /// Обрабатывает обновления, приходящие в бот.
        /// Обрабатываемые типы: текстовое сообщение, нажатие кнопки и оплата.
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="update"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task Update(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update == null) return;

            if (!GetTimeSpanFromUpdates(update, offset))
            {
                var client = GetClient(update);

                if (client != null)
                {
                    var handler = update switch
                    {
                        { Message: { } message } => BotOnMessageReceived(message, client),
                        { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery, client),
                        { PreCheckoutQuery: { } preCheckoutQuery } => BotOnPreCheckoutQueryReceived(preCheckoutQuery, client),
                        _ => Task.CompletedTask,
                    };
                    await handler;
                }
            }
        }

        /// <summary>
        /// Обрабатывает ошибки в боте.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="exception"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            var error = Converter.CreateErrorMessage(exception);
            Logger.AddLog(Errors, error);
            await SendErrorMessage(exception);
        }

        /// <summary>
        /// Отправляет сообщения об ошибке в чат с <see cref="_adminId"/>.
        /// </summary>
        /// <param name="message"></param>
        public async Task SendErrorMessage(Exception ex)
        {
            var message = Converter.CreateErrorMessage(ex);

            await _bot.SendMessage(
                    chatId: _adminId,
                    text: $"@VitalUd\nОшибка:\n\n{message}");
        }

        /// <summary>
        /// Отправляет сообщение при аварийном выходе.
        /// </summary>
        public async Task SendShutdownErrorMessage()
        {
            await _bot.SendMessage(
                    chatId: _adminId,
                    text: $"@VitalUd\n\nАварийный выход.");
        }

        /// <summary>
        /// Обрабатывает обновления типа Message (текстовое сообщение).
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task BotOnMessageReceived(Message message, Client client)
        {
            if (message == null) return;

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
                    await MessageHandler(message, client);
            }
        }

        /// <summary>
        /// <para>Обрабатывает текстовые команды через '/'.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task CommandMessageHandler(Message message, Client client)
        {
            if (message.Text == null) return;

            if (message.Text.Equals("/strategies"))
                await StrategiesCommandMessageHandler(message, client);
            else if (message.Text.Equals("/search"))
                await SearchCommandMessageHandler(message, client);
            else if (message.Text.Equals("/info"))
                await InfoCommandMessageHandler(message, client);
            else if (message.Text.Equals("/deposit"))
                await DepositCommandMessageHandler(message, client);
        }

        /// <summary>
        /// <para>Обрабатывает команду /strategies.</para>
        /// Присылает сообщение с выбором биржи и переводит состояние клиента в Strategy.<br/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task StrategiesCommandMessageHandler(Message message, Client client)
        {
            client.Telegram.Stage = Stage.Strategy;

            var names = MenuService.Subscriptions.Select(x => x.Name).ToArray();
            InlineKeyboardMarkup inlineKeyboard = MenuService.GenerateButtons(names, buttonsPerRow: 1, callback: "burse_name_");
            await _bot.SendMessage(
                chatId: message.Chat.Id,
                text: $"Выберите биржу:",
                replyMarkup: inlineKeyboard);

            client.Telegram.Temp.Strategies.Clear();
        }

        /// <summary>
        /// <para>Обрабатывает команду /search.</para>
        /// Очищает массив временных стратегий и переводит состояние клиента в Search.<br/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task SearchCommandMessageHandler(Message message, Client client)
        {
            client.Telegram.Stage = Stage.Search;

            await _bot.SendMessage(
                chatId: message.Chat.Id,
                text: $"Введите номер стратегии:");

            client.Telegram.Temp.Strategies.Clear();
        }

        /// <summary>
        /// <para>Обрабатывает команду /info.</para>
        /// Отсылает сообщение с информацией об аккаунте и переводит состояние клиента в Zero.<br/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task InfoCommandMessageHandler(Message message, Client client)
        {
            client.Telegram.Stage = Stage.Zero;

            await _bot.SendMessage(
                chatId: message.Chat.Id,
                text: $"ID: {client.Telegram.Id}\n" +
                      $"Логин: {client.Data.Login}\n" +
                      $"Пароль: {client.Data.Password}\n" +
                      $"Депозит: {client.Data.Deposit} руб.\n" +
                      $"Платеж: {client.Data.Payment} руб./день\n" +
                      $"Стратегии:");
            await SendStrategyFromInfo(client, message);
        }

        /// <summary>
        /// <para>Обрабатывает команду /deposit.</para>
        /// Отсылает сообщение с запросом суммы пополнения и переводит состояние клиента в Deposit.<br/>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task DepositCommandMessageHandler(Message message, Client client)
        {
            client.Telegram.Stage = Stage.Deposit;

            await _bot.SendMessage(
                chatId: message.Chat.Id,
                text: $"Введите сумму для пополнения:");
        }

        /// <summary>
        /// <para>Обрабатывает сообщения в зависимости от состояния клиента.</para>
        /// Zero - информация об аккаунте.<br/>
        /// Strategy - обработка отсутствует, управление в этом состоянии только кнопками.<br/>
        /// Search - поиск стратегии по номеру и перевод в состояние Strategy.<br/>
        /// Deposit - проверка введенной суммы.<br/>
        /// Payment - проверка оплаты.<br/>
        /// Limit - проверка лимита.<br/>
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
        /// Обрабатывает обновления типа CallbackQuery (нажатие кнопки).
        /// </summary>
        /// <param name="callbackQuery"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery, Client client)
        {
            if (callbackQuery.Data == null) return;

            if (callbackQuery.Message != null)
                Logger.AddLog(Messages, $"{callbackQuery.Message.Chat.Username} Query: {callbackQuery.Data}");

            var data = callbackQuery.Data.Split('_');
            await CallbackHandler(callbackQuery, client, data);
        }

        /// <summary>
        /// <para>Обрабатывает нажатие кнопок в зависимости от состояния клиента.</para>
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
                if (data[1].Equals(MenuService.GetCallbackString(Callback.Next)) ||
                    data[1].Equals(MenuService.GetCallbackString(Callback.Previous)))
                    await MoveButton(_bot, client, callbackQuery, data[1]);
            }

            if (client.Telegram.Stage.Equals(Stage.Zero))
            {
                if (data[0].Equals(MenuService.GetCallbackString(Callback.Unsub)))
                    await UnsubscribeFromStrategy(client, callbackQuery);
            }
            else if (client.Telegram.Stage.Equals(Stage.Strategy))
            {
                if (data[0].Equals("burse"))
                {
                    if (data[1].Equals(MenuService.GetCallbackString(Callback.Name)))
                        await SendTypes(client, callbackQuery, data[2]);
                    else if (data[1].Equals(MenuService.GetCallbackString(Callback.Type)))
                        await SendSubtypes(callbackQuery, data[2]);
                    else if (data[1].Equals(MenuService.GetCallbackString(Callback.Subtype)))
                        await SendStrategy(client, callbackQuery, data[2]);
                }
                else if (data[0].Equals(MenuService.GetCallbackString(Callback.Sub)))
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
        /// Обрабатывает обновления типа PreCheckoutQuery (препроверка оплаты).
        /// </summary>
        /// <param name="preCheckoutQuery"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task BotOnPreCheckoutQueryReceived(PreCheckoutQuery preCheckoutQuery, Client client)
        {
            Logger.AddLog(_messages, $"{preCheckoutQuery.From.Username} PreCheckoutQuery: {preCheckoutQuery.Currency}");
            if (client.Telegram.Stage.Equals(Stage.Payment)) 
                await _bot.AnswerPreCheckoutQuery(preCheckoutQuery.Id);
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
            if (query.Message == null) return;

            var name = MenuService.Subscriptions.Find(x => x.Name.Equals(burse));
            if (name != null)
            {
                var types = name.Types.Select(x => x.Code).ToArray();
                if (types.Length > 0)
                {
                    client.Telegram.Temp.Burse = burse;

                    InlineKeyboardMarkup inlineKeyboard = MenuService.GenerateButtons(types, buttonsPerRow: 1, callback: "burse_type_");
                    await _bot.EditMessageText(
                        chatId: query.Message.Chat.Id,
                        messageId: query.Message.MessageId,
                        text: $"Выберите тип:",
                        replyMarkup: inlineKeyboard);
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
        private async Task SendSubtypes(CallbackQuery query, string type)
        {
            if (query.Message == null) return;

            var subtypes = MenuService.Subscriptions.SelectMany(x => x.Types).First(x => x.Name.Equals(type)).Subtypes;
            if (subtypes.Length > 0)
            {
                InlineKeyboardMarkup inlineKeyboard = MenuService.GenerateButtons(subtypes, buttonsPerRow: 1, callback: "burse_subtype_");
                await _bot.EditMessageText(
                    chatId: query.Message.Chat.Id,
                    messageId: query.Message.MessageId,
                    text: $"Выберите подтип:",
                    replyMarkup: inlineKeyboard);
            }
        }

        /// <summary>
        /// Формирует временный список стратегий TempStrategies по выбранным клиентом бирже, типу и подтипу с сортировкой по PL.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <param name="subtype"></param>
        /// <returns></returns>
        private async Task SendStrategy(Client client, CallbackQuery query, string subtype)
        {
            if (query.Message == null) return;

            var strategies = _strategies.StrategiesList.Items.Where
                (x => x.Name.ToString().Equals(client.Telegram.Temp.Burse) &&
                x.Telegram.Subtype.Equals(subtype)).ToList();

            if (strategies.Count > 0)
            {
                client.Telegram.Temp.Strategies = strategies;
                client.Telegram.Temp.Strategies.Sort((a, b) => b.Telegram.Pl.CompareTo(a.Telegram.Pl));

                client.Telegram.Index = 0;
                client.Telegram.Lenght = client.Telegram.Temp.Strategies.Count;

                InlineKeyboardMarkup inlineKeyboard = MenuService.GenerateStrategiesSubButtons(MenuService.GetCallbackString(Callback.Sub), "Подписаться", client);

                var strategy = client.Telegram.Temp.Strategies[0];
                using var stream = new FileStream(strategy.Telegram.ImagePath, FileMode.Open, FileAccess.Read);
                var inputFile = InputFile.FromStream(stream, Path.GetFileName(strategy.Telegram.ImagePath));

                await _bot.SendPhoto(
                    chatId: query.Message.Chat.Id,
                    photo: inputFile,
                    caption: $"Стратегия: {strategy.Code} \n" +
                    $"Описание: {strategy.Telegram.Description} \n" +
                    $"PL: {strategy.Telegram.Pl} \n" +
                    $"Минимальный торговый лимит: {strategy.Telegram.Limit} руб.",
                    replyMarkup: inlineKeyboard);
            }
            else 
            {
                await _bot.SendMessage(
                    chatId: query.Message.Chat.Id,
                    text: $"Нет стратегий.");

                client.Telegram.Stage = Stage.Zero;
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
            if (query.Message == null) return;

            var temp = client.Telegram.Temp;
            temp.Code = temp.Strategies[client.Telegram.Index].Code;
            temp.Price = temp.Strategies[client.Telegram.Index].Telegram.Limit;
            if (query.Message.Photo != null) 
                temp.PhotoId = query.Message.Photo[1].FileId;

            var strat = client.Data.Strategies.Items.FirstOrDefault(x => x.Code.Equals(client.Telegram.Temp.Code));
            if (strat != null)
            {
                await _bot.SendMessage(
                    chatId: query.Message.Chat.Id,
                    text: $"Подписка уже приобретена");
            }
            else
            {
                if (client.Data.Deposit < client.Telegram.Temp.Price * client.Data.Percentage / 100)
                {
                    await _bot.SendMessage(
                        chatId: query.Message.Chat.Id,
                        text: $"Недостаточно средств.\nЧтобы продолжить, введите сумму для пополнения:");

                    client.Telegram.Stage = Stage.Deposit;
                }
                else
                {
                    await _bot.SendMessage(
                        chatId: query.Message.Chat.Id,
                        text: $"Введите торговый лимит:");

                    client.Telegram.Stage = Stage.Limit;
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
            if (query.Message == null) return;

            if (client.Data.Strategies.Count > 0)
            {
                var strat = client.Data.Strategies.Items[client.Telegram.Index];
                if (strat != null)
                {
                    await _bot.SendMessage(
                        chatId: query.Message.Chat.Id,
                        text: $"Вы отписались от стратегии {strat.Code}");
                    await UiInvoker.UiInvoke(() => client.Data.Strategies.RemoveAt(client.Telegram.Index));
                }
            }
        }

        /// <summary>
        /// Получает одну стратегию по запросу через /search.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        private async void GetStrategy(Client client, Message message)
        {
            if (message.Text == null) return;

            CheckChars(message.Text, out bool flag);
            if (flag)
                await _bot.SendMessage(
                    chatId: message.Chat.Id, 
                    text: "Некорректный номер, повторите ввод:");
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
        /// Обрабатывает сообщения с суммой депозита при пополнении.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task GetDeposit(Client client, Message message)
        {
            if (message.Text == null) return;

            CheckChars(message.Text, out bool flag);
            if (flag)
            {
                await _bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Некорректная сумма, повторите ввод:");
            }
            else
            {
                if (int.TryParse(message.Text, out int deposit))
                {
                    if (deposit < minimalPayment)
                    {
                        await _bot.SendMessage(
                            chatId: message.Chat.Id,
                            text: $"Минимальная сумма к пополнению - {minimalPayment} руб., повторите ввод:");
                    }
                    else
                    {
                        client.Telegram.Stage = Stage.Payment;
                        client.Telegram.Temp.Deposit = deposit;

                        var names = new string[paymentSystems.Count];
                        for (int i = 0; i < paymentSystems.Count; i++) 
                            names[i] = paymentSystems[i].Name.ToString();
                        InlineKeyboardMarkup inlineKeyboard = MenuService.GenerateButtons(names, buttonsPerRow: 1);
                        await _bot.SendMessage(
                            chatId: message.Chat.Id,
                            text: $"Выберите способ оплаты:",
                            replyMarkup: inlineKeyboard);
                    }
                }
                else
                {
                    await _bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Некорректная сумма, повторите ввод:");
                }
            }
        }

        /// <summary>
        /// Обрабатывает выбор платежной системы при пополнении депозита, выставляет чек на оплату.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task GetPayment(Client client, CallbackQuery query)
        {
            if (query.Message == null) return;

            if (client.Telegram.Temp.Deposit > minimalPayment)
            {
                List<LabeledPrice> price =
                [
                    new LabeledPrice("Сумма к оплате", client.Telegram.Temp.Deposit * 100)
                ];

                var system = paymentSystems.Find(x => x.Name.Equals(query.Data));
                if (system != null)
                {
                    await _bot.SendInvoice(
                                chatId: query.Message.Chat.Id,
                                title: "Чек",
                                description: "Пополнение личного счета",
                                payload: "test",
                                providerToken: system.Token,
                                currency: "RUB",
                                prices: price,
                                replyMarkup: MenuService.GeneratePaymentButtons());
                }
            }
        }

        /// <summary>
        /// Обрабатывает сообщение об успешной оплате. 
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
            client.Telegram.Temp.Deposit = 0;

            Logger.AddLog(_clientDataBase.Logs, $"client {client.Data.Login} is Active now");

            if (client.Telegram.Temp.Code != null)
            {
                var strat = client.Telegram.Temp.Strategies[client.Telegram.Index];

                using var stream = new FileStream(strat.Telegram.ImagePath, FileMode.Open, FileAccess.Read);
                var inputFile = InputFile.FromStream(stream, Path.GetFileName(strat.Telegram.ImagePath));

                await _bot.SendPhoto(
                    chatId: message.Chat.Id,
                    photo: inputFile,
                    caption: $"Стратегия: {strat.Code} \n" +
                    $"Описание: {strat.Telegram.Description}\n" +
                    $"Минимальный торговый лимит: {strat.Telegram.Limit} руб.");

                await _bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"Введите торговый лимит:");
                client.Telegram.Stage = Stage.Limit;

            }
            else
            {
                client.Telegram.Stage = Stage.Zero;
            }
        }

        /// <summary>
        /// Обрабатывает сообщение с суммой лимита на стратегию.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task GetLimit(Client client, Message message)
        {
            if (message.Text == null) return;

            CheckChars(message.Text, out bool flag);
            if (flag)
            {
                await _bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Некорректная сумма, повторите ввод:");
            }
            else
            {
                int limit = int.Parse(message.Text);
                int commission = (int)(limit * client.Data.Percentage / 100);

                if (limit < client.Telegram.Temp.Price)
                {
                    await _bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Введенный лимит меньше минимального, повторите ввод:");
                }
                else if (commission > client.Data.Deposit)
                {
                    await _bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"Недостаточно средств (баланс - {client.Data.Deposit}, необходимо - {commission}), пополните счет:");

                    client.Telegram.Stage = Stage.Deposit;
                }
                else
                {
                    client.Telegram.Temp.Limit = limit;
                    await _bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: $"Подтвердите выбор:\n" +
                        $"Стратегия: {client.Telegram.Temp.Code}.\n" +
                        $"Торговый лимит: {client.Telegram.Temp.Limit}.\n" +
                        $"Комиссия: {commission + 1} руб/день.",
                        replyMarkup: MenuService.GenerateConfirmationButtons());
                }
            }
        }

        /// <summary>
        /// Обрабатывает сообщение при окончательном выборе подписки.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private async Task GetSubscription(Client client, CallbackQuery query)
        {
            if (query.Data == null || query.Message == null) return;

            //Подтверждение покупки 
            if (query.Data.Equals(MenuService.GetCallbackString(Callback.Correct)))
            {
                int payment = (int)(client.Telegram.Temp.Limit * client.Data.Percentage / 100) + 1;
                await UiInvoker.UiInvoke(() => client.Data.Strategies.Add(new(client.Telegram.Temp.Burse, client.Telegram.Temp.Code, client.Telegram.Temp.Limit, payment)));

                client.Telegram.Index = 0;

                await _bot.SendMessage(
                    chatId: query.Message.Chat.Id,
                    text: $"Подписка приобретена",
                    replyMarkup: MenuService.GenerateContinueButtons());
            }

            //Изменение данных по лимиту
            else if (query.Data.Equals(MenuService.GetCallbackString(Callback.Edit)))
            {
                await _bot.SendMessage(
                    chatId: query.Message.Chat.Id,
                    text: $"Введите торговый лимит:");
            }
            //Завершение работы с выбранным списком подписок
            else if (query.Data.Equals(MenuService.GetCallbackString(Callback.Done)))
            {
                client.Telegram.Temp.ClearData();
                client.Telegram.Stage = Stage.Zero;
            }
            //Возврат к просмотру отобранных подписок
            else if (query.Data.Equals(MenuService.GetCallbackString(Callback.Back)))
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

            client.Telegram.Index = 0;
            var clientStrategy = client.Data.Strategies.Items[client.Telegram.Index];
            var strategy = _strategies.StrategiesList.Items.FirstOrDefault(x => x.Code.Equals(clientStrategy.Code));
            if (strategy != null)
            {
                using var stream = new FileStream(strategy.Telegram.ImagePath, FileMode.Open, FileAccess.Read);
                var inputFile = InputFile.FromStream(stream, Path.GetFileName(strategy.Telegram.ImagePath));

                await _bot.SendPhoto(
                        chatId: message.Chat.Id,
                        photo: inputFile,
                        caption: $"Стратегия: {clientStrategy.Code} \n" +
                        $"Торговый лимит: {clientStrategy.TradeLimit} руб. \n" +
                        $"Тариф: {clientStrategy.Payment} руб./день",
                        replyMarkup: MenuService.GenerateStrategiesSubButtons(MenuService.GetCallbackString(Callback.Unsub), "Отписаться", client));
            }
        }

        /// <summary>
        /// Ищет клиента в базе по ID в зависимости от типа update. 
        /// Если клиента в базе нет, возвращает нового клиента без добавления в базу.
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        private Client? GetClient(Update update)
        {
            var id = update.Type switch
            {
                UpdateType.Message when update.Message?.From != null => update.Message.From.Id.ToString(),
                UpdateType.CallbackQuery when update.CallbackQuery?.From != null => update.CallbackQuery.From.Id.ToString(),
                UpdateType.PreCheckoutQuery when update.PreCheckoutQuery?.From != null => update.PreCheckoutQuery.From.Id.ToString(),
                UpdateType.MyChatMember when update.MyChatMember?.From != null => update.MyChatMember.From.Id.ToString(),
                _ => string.Empty
            };

            var client = _clientDataBase.Clients.Items.FirstOrDefault(x => x.Telegram.Id.Equals(id));

            if (client == null)
            {
                var telegram = new TelegramData(id);
                if (update.Type.Equals(UpdateType.Message))
                {
                    if (update.Message?.Chat.Username != null)
                    {
                        var data = new Data(update.Message.Chat.Username);
                        client = new Client(data, telegram);
                        UiInvoker.UiInvoke(() => Clients.Clients.Add(client));
                    }
                }
                else if (update.Type.Equals(UpdateType.CallbackQuery))
                {
                    if (update.CallbackQuery?.From.Username != null)
                    {
                        var data = new Data(update.CallbackQuery.From.Username);
                        client = new Client(data, telegram);
                        UiInvoker.UiInvoke(() => Clients.Clients.Add(client));
                    }
                }
            }

            return client;
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

            using var stream = new FileStream(strategy.Telegram.ImagePath, FileMode.Open, FileAccess.Read);
            var inputFile = InputFile.FromStream(stream, Path.GetFileName(strategy.Telegram.ImagePath));

            await _bot.SendPhoto(
                chatId: chatId,
                photo: inputFile,
                caption: caption,
                replyMarkup: MenuService.GenerateStrategiesSubButtons(MenuService.GetCallbackString(Callback.Sub), "Подписаться", client));
        }

        /// <summary>
        /// Генерирует сообщение при нажатии кнопки смещения.
        /// </summary>
        private async Task MoveButton(TelegramBotClient bot, Client client, CallbackQuery query, string move)
        {
            if (move.Equals("next")) client.Telegram.Index++;
            else if (move.Equals("previous")) client.Telegram.Index--;

            if (client.Telegram.Stage.Equals(Stage.Zero)) 
                await ChangeInfoElement(bot, client, query);
            else if (client.Telegram.Stage.Equals(Stage.Strategy)) 
                await ChangeStrategiesElement(bot, client, query);
        }

        /// <summary>
        /// Генерирует сообщение при смещении в блоке инфо.
        /// </summary>
        private async Task ChangeInfoElement(TelegramBotClient bot, Client client, CallbackQuery query)
        {
            if (query.Message == null) return;

            var clientStrategy = client.Data.Strategies.Items[client.Telegram.Index];
            var strategy = _strategies.StrategiesList.Items.First(x => x.Code.Equals(client.Data.Strategies.Items[client.Telegram.Index].Code));
            if (strategy != null)
            {
                await bot.DeleteMessage(
                    chatId: query.Message.Chat.Id,
                    messageId: query.Message.MessageId);

                using var stream = new FileStream(strategy.Telegram.ImagePath, FileMode.Open, FileAccess.Read);
                var inputFile = InputFile.FromStream(stream, Path.GetFileName(strategy.Telegram.ImagePath));

                await bot.SendPhoto(
                    chatId: query.Message.Chat.Id,
                    photo: inputFile,
                    caption: $"Стратегия: {clientStrategy.Code} \n" +
                    $"Торговый лимит: {clientStrategy.TradeLimit} руб. \n" +
                    $"Тариф: {clientStrategy.Payment} руб./день",
                    replyMarkup: MenuService.GenerateStrategiesSubButtons("unsub", "Отписаться", client));
            }
        }

        /// <summary>
        /// Генерирует сообщение при смещении в магазине подписок.
        /// </summary>
        private async Task ChangeStrategiesElement(TelegramBotClient bot, Client client, CallbackQuery query)
        {
            if (query.Message == null) return;

            var strategy = client.Telegram.Temp.Strategies[client.Telegram.Index];
            var info = strategy.Telegram;

            await bot.DeleteMessage(
                chatId: query.Message.Chat.Id,
                messageId: query.Message.MessageId);

            using var stream = new FileStream(strategy.Telegram.ImagePath, FileMode.Open, FileAccess.Read);
            var inputFile = InputFile.FromStream(stream, Path.GetFileName(strategy.Telegram.ImagePath));

            await bot.SendPhoto(
                chatId: query.Message.Chat.Id,
                photo: inputFile,
                caption: $"Стратегия: {strategy.Code} \n" +
                $"Описание: {info.Description} \n" +
                $"PL: {info.Pl} \n" +
                $"Минимальный торговый лимит: {info.Limit} руб.",
                replyMarkup: MenuService.GenerateStrategiesSubButtons("sub", "Подписаться", client));
        }

        /// <summary>
        /// Проверяет на дату входящего обновления. 
        /// При запуске бота отсеивает обновления, пришедшие ранее offset.
        /// </summary>
        /// <param name="update">Обновление от бота.</param>
        /// <param name="offset">Время в секундах, относительно которого отсеиваются обновления.</param>
        /// <returns></returns>
        private static bool GetTimeSpanFromUpdates(Update update, int offset)
        {
            var skip = false;
            var time = new DateTime();

            if (update.Type.Equals(UpdateType.Message))
            {
                if (update.Message != null)
                {
                    if (update.Message.EditDate != null) 
                        time = (DateTime)update.Message.EditDate;
                    else 
                        time = update.Message.Date;
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
                        else 
                            time = update.CallbackQuery.Message.Date;
                    }
                }
            }
            else if (update.Type.Equals(UpdateType.PreCheckoutQuery))
            {
                return skip;
            }

            var times = DateTime.UtcNow - time;
            if (times.TotalSeconds > offset) 
                skip = true;
            return skip;
        }

        /// <summary>
        /// Создает пароль из случайных символов (цифры/буквы).
        /// </summary>
        /// <param name="length">Количество случайных символов.</param>
        /// <returns></returns>
        private static string GetPass(int length)
        {
            var pass = string.Empty;
            var rand = new Random();
            while (pass.Length < length)
            {
                char ch = (char)rand.Next(33, 125);
                if (char.IsLetterOrDigit(ch))
                    pass += ch;
            }
            return pass;
        }

        /// <summary>
        /// Проверяет сообщение на наличие не цифр.
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
    }
}
