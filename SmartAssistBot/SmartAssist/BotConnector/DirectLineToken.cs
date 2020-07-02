using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartBot.SmartAssist.BotConnector
{
    /// <summary>
    /// class for serialization/deserialization DirectLineToken
    /// </summary>
    public class DirectLineToken
    {
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="token">Directline token string</param>
        public DirectLineToken(string token)
        {
            Token = token;
        }

        public string Token { get; set; }
    }
}
