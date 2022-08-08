using DubUrl.Mapping;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class ConnectionUrl
    {
        private SchemeMapperBuilder SchemeMapperBuilder { get; }
        private IMapper? Mapper { get; set; }
        private IParser Parser { get; }
        private string Url { get; }

        public ConnectionUrl(string url)
            : this(url, new Parser(), new SchemeMapperBuilder()) { }

        public ConnectionUrl(string url, SchemeMapperBuilder builder)
            : this(url, new Parser(), builder) { }

        internal ConnectionUrl(string url, IParser parser, SchemeMapperBuilder builder)
            => (Url, Parser, SchemeMapperBuilder) = (url, parser, builder);

        private (string ConnectionString, UrlInfo UrlInfo, string[] Dialects) ParseDetail()
        {
            var urlInfo = Parser.Parse(Url);
            SchemeMapperBuilder.Build(urlInfo.Schemes);
            Mapper = SchemeMapperBuilder.GetMapper();
            Mapper.Map(urlInfo);
            return (Mapper.GetConnectionString(), urlInfo, Mapper.GetDialects());
        }

        public string Parse() => ParseDetail().ConnectionString;

        public virtual IDbConnection Connect()
        {
            var parsing = ParseDetail();
            var provider = SchemeMapperBuilder.GetProviderFactory();
            var connection = provider.CreateConnection() ?? throw new ArgumentNullException();
            connection.ConnectionString = parsing.ConnectionString;
            return connection;
        }

        public virtual IDbConnection Open()
        {
            var connection = Connect();
            connection.Open();
            return connection;
        }

        public virtual string[] Dialects { get => ParseDetail().Dialects; }
    }
}
