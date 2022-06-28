using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class MySqlDataMapper : MySqlConnectorMapper
    {
        private const string SSPI_KEYWORD = "Integrated Security";
        
        public MySqlDataMapper(DbConnectionStringBuilder csb)
            : base(csb,
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new ServerMapper(),
                    new AuthentificationMapper(),
                    new DatabaseMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal new class AuthentificationMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (!string.IsNullOrEmpty(urlInfo.Username))
                    Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                if (!string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

                if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(SSPI_KEYWORD, "sspi");
            }
        }
    }
}
