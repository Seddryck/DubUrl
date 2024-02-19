using DubUrl.Extensions;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

public class ConnectionUrl : BaseConnectionUrl
{
    public ConnectionUrl(string url)
        : this(url, new Parser(), new SchemeMapperBuilder().Build()) { }

    public ConnectionUrl(string url, SchemeMapper schemeMapper)
        : this(url, new Parser(), schemeMapper) { }

    internal ConnectionUrl(string url, IParser parser, SchemeMapper schemeMapper)
        : base(url, parser, schemeMapper) { }

    public virtual IDbConnection Connect()
    {
        var connection = GetProviderFactory().CreateConnection() ?? throw new ArgumentNullException();
        connection.ConnectionString = Result.ConnectionString;
        return connection;
    }

    public virtual IDbConnection Open()
    {
        var connection = Connect();
        connection.Open();
        return connection;
    }
}
