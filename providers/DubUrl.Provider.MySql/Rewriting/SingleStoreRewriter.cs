using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;
using System.Data.Common;

namespace DubUrl.Provider.MySql.Rewriting;

internal class SingleStoreRewriter : MySqlConnectorRewriter
{
    public SingleStoreRewriter(DbConnectionStringBuilder csb) : base(csb, "SingleStore") { }

    protected SingleStoreRewriter(Specificator specificator, BaseTokenMapper[] tokenMappers)
        : base(specificator, tokenMappers) { }
}
