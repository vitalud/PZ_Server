using ProjectZeroLib.Enums;
using Server.Service.Enums;
using Server.Service.UserClient;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.Service.Bot
{
    /// <summary>
    /// Класс, описывающий методы создания интерфейсов для сообщений в чат боте.
    /// </summary>
    public static class MenuService
    {
        public readonly static List<Subscription> Subscriptions = [];

        static MenuService()
        {
            Subscriptions.Add(new(
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

            Subscriptions.Add(new(
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

            Subscriptions.Add(new(
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

            Subscriptions.Add(new(
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
        /// Создание кнопок под сообщением.
        /// </summary>
        /// <param name="array">Массив имен кнопок.</param>
        /// <param name="buttonsPerRow">Количество кнопок в ряду.</param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static InlineKeyboardButton[][] GenerateButtons(string[] array, int buttonsPerRow = 1, string? callback = null)
        {
            var data = array.Select(x => InlineKeyboardButton.WithCallbackData(x, callback + x));
            if (buttonsPerRow == 0)
                return [data.ToArray()];
            else
                return data.Chunk(buttonsPerRow).Select(c => c.ToArray()).ToArray();
        }

        /// <summary>
        /// Генератор кнопок для работы с подписками. Обрабатываются две коллекции подписок:
        /// Первая при просмотре в магазине подписок, вторая при просмотре подписок в инфо.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="name"></param>
        /// <param name="client"></param>
        /// <param name="addition"></param>
        /// <returns></returns>
        public static InlineKeyboardMarkup GenerateStrategiesSubButtons(string callback, string name, Client client, string? addition = null)
        {
            bool oneStrategy = false;
            int count = 0;

            InlineKeyboardButton next = InlineKeyboardButton.WithCallbackData(">>", "move_next");
            InlineKeyboardButton back = InlineKeyboardButton.WithCallbackData("<<", "move_previous");
            InlineKeyboardButton act = InlineKeyboardButton.WithCallbackData(name, callback + addition);

            if (callback.Equals(GetCallbackString(Callback.Sub)))
            {
                if (client.Telegram.Temp.Strategies.Count == 1)
                    oneStrategy = true;

                count = client.Telegram.Temp.Strategies.Count;
            }
            else if (callback.Equals(GetCallbackString(Callback.Unsub)))
            {
                if (client.Data.Strategies.Count == 1)
                    oneStrategy = true;

                count = client.Data.Strategies.Count;
            }

            InlineKeyboardMarkup inlineKeyboard;
            if (oneStrategy)
                inlineKeyboard = new([[act]]);
            else
            {
                if (client.Telegram.Index == 0)
                    inlineKeyboard = new([[act], [next]]);
                else if (client.Telegram.Index.Equals(count - 1) && count > 1)
                    inlineKeyboard = new([[act], [back]]);
                else
                    inlineKeyboard = new([[act], [back, next]]);
            }

            return inlineKeyboard;
        }

        /// <summary>
        /// Генерация кнопок оплаты.
        /// </summary>
        /// <returns></returns>
        public static InlineKeyboardMarkup GeneratePaymentButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new([
                    [ InlineKeyboardButton.WithPay("Pay") ],
                    [ InlineKeyboardButton.WithCallbackData("Назад", GetCallbackString(Callback.Back)) ]
                ]);
            return inlineKeyboard;
        }

        /// <summary>
        /// Генерация кнопок подтверждения.
        /// </summary>
        /// <returns></returns>
        public static InlineKeyboardMarkup GenerateConfirmationButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new([
                [ InlineKeyboardButton.WithCallbackData("Верно", GetCallbackString(Callback.Correct)) ],
                [ InlineKeyboardButton.WithCallbackData("Изменить", GetCallbackString(Callback.Edit)) ]
            ]);
            return inlineKeyboard;
        }

        /// <summary>
        /// Генерация кнопок продолжения после приобритения подписки.
        /// </summary>
        /// <returns></returns>
        public static InlineKeyboardMarkup GenerateContinueButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new([
                [ InlineKeyboardButton.WithCallbackData("Вернуться к просмотру", GetCallbackString(Callback.Back)) ],
                [ InlineKeyboardButton.WithCallbackData("Закончить просмотр", GetCallbackString(Callback.Done)) ]
            ]);
            return inlineKeyboard;
        }

        /// <summary>
        /// Преобразование callback в string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
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
