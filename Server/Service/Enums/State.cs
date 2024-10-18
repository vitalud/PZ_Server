using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
