using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading;

internal class InlineCommand : ICommandProvider
{
    protected string Text { get; }
    private IQueryLogger QueryLogger { get; }

    public InlineCommand(string text, IQueryLogger queryLogger) 
        => (Text, QueryLogger) = (text, queryLogger);

    public string Read(IDialect dialect, IConnectivity connectivity)
    {
        var text = Render(dialect);
        QueryLogger.Log(text);
        return text;
    }

    protected virtual string Render(IDialect dialect)
        => Text;

    public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => true;

    public virtual IDbCommand CreateCommand(IDialect dialect, IConnectivity connectivity, IDbConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = Read(dialect, connectivity);
        return cmd;
    }
}
