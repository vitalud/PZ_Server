namespace Server.Service.Enums
{
    /// <summary>Статус клиента в базе.</summary>
    public enum State
    {
        /// <summary>
        /// Неавторизованный пользователь.
        /// </summary>
        Neutral,
        /// <summary>
        /// Авторизованный пользователь.
        /// </summary>
        Active
    }
}
