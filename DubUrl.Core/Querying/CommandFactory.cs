using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    public class CommandFactory
    {
        public virtual IDbCommand Execute(IDbConnection conn, IQuery query, string[] dialects)
        {
            if (!query.Exists(dialects, true))
                throw new ArgumentException();

            var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query.Read(dialects);
            
            return cmd;
        }
    }
}
