using DubUrl.Rewriting.Tokening;
using System.Data.Common;

namespace DubUrl.Rewriting.Implementation;

internal class SingleStoreRewriter : MySqlConnectorRewriter
{
    public SingleStoreRewriter(DbConnectionStringBuilder csb) : base(csb, "SingleStore") { }

    protected SingleStoreRewriter(Specificator specificator, BaseTokenMapper[] tokenMappers)
        : base(specificator, tokenMappers) { }
}
