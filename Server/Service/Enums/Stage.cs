namespace Server.Service.Enums
{
    /// <summary>Состояние, в котором находится клиент.</summary>
    public enum Stage
    {
        /// <summary>
        /// Начало работы с ботом либо просмотр информации об аккаунте.
        /// </summary>
        Zero,
        /// <summary>
        /// Пополнение счета.
        /// </summary>
        Payment,
        /// <summary>
        /// Поиск стратегии.
        /// </summary>
        Search,
        /// <summary>
        /// Работа со списком стратегий.
        /// </summary>
        Strategy,
        /// <summary>
        /// Указание/изменение торгового лимита на стратегию.
        /// </summary>
        Limit,
        /// <summary>
        /// Указание суммы к пополнению.
        /// </summary>
        Deposit,
    }
}
