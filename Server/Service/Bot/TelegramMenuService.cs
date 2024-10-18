using Server.Service.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ProjectZeroLib;
using ProjectZeroLib.Enums;

namespace Server.Service.Bot
{
    public enum Callback
    {
        Name,
        Type,
        Subtype,
        Sub,
        Unsub,
        Next,
        Previous,
        Done,
        Back,
        Correct,
        Edit
    }
    public class StrategyMenu(string name, List<TypesMenu> types)
    {
        public string Name { get; set; } = name;
        public List<TypesMenu> Types { get; set; } = types;
    }

    public class TypesMenu(string name, string code, string[] subtypes)
    {
        public string Name { get; set; } = name;
        public string Code { get; set; } = code;
        public string[] Subtypes { get; set; } = subtypes;
    }

    public static class TelegramMenuService
    {
        public readonly static List<StrategyMenu> StrategiesMenu;
        static TelegramMenuService()
        {
            StrategiesMenu = [];
            StrategiesMenu.Add(new(
                BurseName.Okx.ToString(),
                [
                    new("1", "1",
                    [
                        "a", "b", "c", "d"
                    ]),
                    new("2", "2",
                    [
                        "e", "f", "g"
                    ]),
                ]
            ));
            StrategiesMenu.Add(new(
                BurseName.Binance.ToString(),
                [
                    new("1", "1",
                    [
                        "a", "b", "c", "d"
                    ]),
                    new("2", "2",
                    [
                        "e", "f", "g"
                    ]),
                ]
            ));
            StrategiesMenu.Add(new(
                BurseName.Bybit.ToString(),
                [
                    new("1", "1",
                    [
                        "a", "b", "c", "d"
                    ]),
                    new("2", "2",
                    [
                        "e", "f", "g"
                    ]),
                ]
            ));
            StrategiesMenu.Add(new(
                BurseName.Quik.ToString(),
                [
                    new("1", "1",
                    [
                        "a", "b", "c", "d"
                    ]),
                    new("2", "2",
                    [
                        "e", "f", "g"
                    ]),
                ]
            ));
        }

        /// <summary>
        /// Генератор сообщения при нажатии кнопки смещения.
        /// </summary>
        public static async Task MoveButton(TelegramBotClient bot, Client client, CallbackQuery query, string move)
        {
            if (move.Equals("next")) client.Telegram.Index++;
            else if (move.Equals("previous")) client.Telegram.Index--;

            if (client.Telegram.Stage == Stage.Zero) await ChangeInfoElement(bot, client, query);
            else if (client.Telegram.Stage == Stage.Strategy) await ChangeStrategiesElement(bot, client, query);
        }

        private static async Task ChangeInfoElement(TelegramBotClient bot, Client client, CallbackQuery query)
        {
            //var strat = client.Data.Strategies.Items[client.Telegram.Index];
            //var limit = strat.TradeLimit;
            //var info = Strategies.StrategiesList.Items.First(x => x.Code == client.Data.Strategies.Items[client.Telegram.Index].Code);
            //if (info != null)
            //{
            //    double charge = Math.Round(limit * client.Data.Percentage / 100 + 1);
            //    await bot.DeleteMessageAsync(
            //            chatId: query.Message.Chat.Id,
            //            messageId: query.Message.MessageId);
            //    await bot.SendPhotoAsync(
            //        chatId: query.Message.Chat.Id,
            //        photo: InputFile.FromFileId(info.TelegramInfo.PhotoUrl),
            //        caption: $"Стратегия: {info.Code} \n" +
            //        $"Торговый лимит: {limit} руб. \n" +
            //        $"Тариф: {charge} руб./день",
            //        replyMarkup: GenerateStrategiesSubButtons("unsub", "Отписаться", client));
            //}
        }

        private static async Task ChangeStrategiesElement(TelegramBotClient bot, Client client, CallbackQuery query)
        {
            var strat = client.Telegram.Temp.Strategies[client.Telegram.Index];
            var info = strat.TelegramInfo;
            await bot.DeleteMessageAsync(
                    chatId: query.Message.Chat.Id,
                    messageId: query.Message.MessageId);
            await bot.SendPhotoAsync(
                chatId: query.Message.Chat.Id,
                photo: InputFile.FromUri(info.PhotoUrl),
                caption: $"Стратегия: {strat.Code} \n" +
                $"Описание: {info.Description} \n" +
                $"PL: {info.Pl} \n" +
                $"Минимальный торговый лимит: {info.Limit} руб.",
                replyMarkup: GenerateStrategiesSubButtons("sub", "Подписаться", client));
        }

        /// <summary>
        /// Создание кнопок под сообщением.
        /// </summary>
        /// <param name="array">Массив имен кнопок.</param>
        /// <param name="buttonsPerRow">Количество кнопок в ряду.</param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static InlineKeyboardButton[][] GenerateButtons(string[] array, int buttonsPerRow = 1, string callback = null)
        {
            var data = array.Select(x => InlineKeyboardButton.WithCallbackData(x, callback + x));
            if (buttonsPerRow == 0)
                return [data.ToArray()];
            else
                return data.Chunk(buttonsPerRow).Select(c => c.ToArray()).ToArray();
        }

        public static T GetElement<T>(List<T> list, int index)
        {
            if (index < 0 || index >= list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Индекс находится вне диапазона списка.");
            }

            return list[index];
        }

        //Генератор кнопок для работы с подписками: buy - в поиске/просмотре стратегий; unsub - в инфо
        public static InlineKeyboardMarkup GenerateStrategiesSubButtons(string callback, string name, Client client, string addition = null)
        {
            bool oneStrategy = false;

            InlineKeyboardMarkup inlineKeyboard = null;
            InlineKeyboardButton next = InlineKeyboardButton.WithCallbackData(">>", "move_next");
            InlineKeyboardButton back = InlineKeyboardButton.WithCallbackData("<<", "move_previous");
            InlineKeyboardButton act = InlineKeyboardButton.WithCallbackData(name, callback + addition);

            //buy только для TempStrategies
            if (callback.Equals(GetCallbackString(Callback.Sub)))
            {
                if (client.Telegram.Temp.Strategies.Count == 1) oneStrategy = true;

                if (oneStrategy) //одна стратегия
                {
                    inlineKeyboard = new(new[]
                    { new[] { act } });
                }
                else
                {
                    if (client.Telegram.Index == 0) //начало списка
                    {
                        inlineKeyboard = new(new[]
                        { new[] { act }, new[] { next } });
                    }
                    else if (client.Telegram.Index == client.Telegram.Temp.Strategies.Count - 1 & client.Telegram.Temp.Strategies.Count > 1) //конец списка
                    {
                        inlineKeyboard = new(new[]
                        { new[] { act }, new[] { back } });
                    }
                    else //промежуточные значения
                    {
                        inlineKeyboard = new(new[]
                        { new[] { act }, new[] { back, next } });
                    }
                }
            }
            //unsub только для Strategies
            else if (callback.Equals(GetCallbackString(Callback.Unsub)))
            {
                if (client.Data.Strategies.Count == 1) oneStrategy = true;

                if (oneStrategy) //одна стратегия
                {
                    inlineKeyboard = new(new[]
                    { new[] { act } });
                }
                else
                {
                    if (client.Telegram.Index == 0) //начало списка
                    {
                        inlineKeyboard = new(new[]
                        { new[] { act }, new[] { next } });
                    }
                    else if (client.Telegram.Index == client.Data.Strategies.Count - 1 & client.Data.Strategies.Count > 1) //конец списка
                    {
                        inlineKeyboard = new(new[]
                        { new[] { act }, new[] { back } });
                    }
                    else //промежуточные значения
                    {
                        inlineKeyboard = new(new[]
                        { new[] { act }, new[] { back, next } });
                    }
                }
            }
            return inlineKeyboard;
        }
        public static InlineKeyboardMarkup GeneratePaymentButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithPayment("Pay")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Назад", GetCallbackString(Callback.Back))
                    }
                });
            return inlineKeyboard;
        }
        public static InlineKeyboardMarkup GenerateConfirmationButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new(new[]
                {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Верно", GetCallbackString(Callback.Correct)),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Изменить", GetCallbackString(Callback.Edit)),
                }
            });
            return inlineKeyboard;
        }
        public static InlineKeyboardMarkup GenerateContinueButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new(new[]
                {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Вернуться к просмотру", GetCallbackString(Callback.Back)),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Закончить просмотр", GetCallbackString(Callback.Done)),
                }
            });
            return inlineKeyboard;
        }
        public static string GetCallbackString(Callback value)
        {
            return value switch
            {
                Callback.Name => "name",
                Callback.Type => "type",
                Callback.Subtype => "subtype",
                Callback.Sub => "sub",
                Callback.Unsub => "unsub",
                Callback.Next => "next",
                Callback.Previous => "previous",
                Callback.Done => "done",
                Callback.Back => "back",
                Callback.Correct => "correct",
                Callback.Edit => "edit",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
