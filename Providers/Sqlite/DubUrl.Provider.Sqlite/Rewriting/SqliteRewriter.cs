using DubUrl.Provider.Sqlite.Conversion;
using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Sqlite.Rewriting;

internal class SqliteRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
        => DubUrl.Provider.Sqlite.Conversion.SqliteConnectionStringReverser.Convert(scheme, source);

    protected internal const string DATABASE_KEYWORD = "Data Source";

    public SqliteRewriter(DbConnectionStringBuilder csb, string rootPath)
        : base(new Specificator(csb),
              [
                new DataSourceMapper(rootPath),
                new OptionsMapper(),
              ]
        )
    { }

    internal class DataSourceMapper(string rootPath)
        : FilePathTokenMapper(DATABASE_KEYWORD, rootPath, "Sqlite", ":memory:");
}
