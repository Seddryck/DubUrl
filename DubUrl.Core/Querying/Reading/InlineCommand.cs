using DubUrl.Mapping;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    internal class InlineCommand : ICommandProvider
    {
        private string Text { get; }

        public InlineCommand(string text) => Text = text;

        public string Read(IDialect dialect, IConnectivity connectivity) => Read();
        private string Read() => Text;
        
        //public virtual IDbCommand CreateCommand(IDialect dialect, IDbConnection conn)
        //{
        //    var cmd = conn.CreateCommand();
        //    cmd.CommandType = CommandType.Text;
        //    cmd.CommandText = Read();

        //    return cmd;
        //}
    }
}
