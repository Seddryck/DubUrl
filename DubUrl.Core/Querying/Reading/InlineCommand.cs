using DubUrl.Querying.Dialects;
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

        public virtual string Read(IDialect dialect, string? connectivity) => Text;
        public bool Exists(IDialect dialect, string? connectivity, bool includeDefault = false) => true;

        public virtual IDbCommand CreateCommand(IDialect dialect, IDbConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = Read(dialect, DeductConnectivity(conn));

            return cmd;
        }

        protected string? DeductConnectivity(object conn)
            => (conn.GetType().Namespace) switch
            {
                "System.Data.Odbc" => "odbc",
                "System.Data.OleDb" => "oledb",
                _ => null,
            };
    }
}
