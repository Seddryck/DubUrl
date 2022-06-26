using DubUrl.Mapping;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class UrlConnection
    {
        private SchemeMapperBuilder SchemeMapperBuilder { get; }
        private IMapper? Mapper { get; set; }
        private IParser Parser { get; }
        private string Url { get; }

        public UrlConnection(string url)
            : this(url, new Parser(), new SchemeMapperBuilder()) { }

        public UrlConnection(string url, SchemeMapperBuilder factory)
            : this(url, new Parser(), factory) { }

        internal UrlConnection(string url, IParser parser, SchemeMapperBuilder builder)
            => (Url, Parser, SchemeMapperBuilder) = (url, parser, builder);

        private (string ConnectionString, UrlInfo UrlInfo) ParseDetail()
        {
            var urlInfo = Parser.Parse(Url);
            SchemeMapperBuilder.Build(urlInfo.Schemes);
            Mapper = SchemeMapperBuilder.GetMapper();
            Mapper.Map(urlInfo);
            return (Mapper.GetConnectionString(), urlInfo);
        }

        public string Parse() => ParseDetail().ConnectionString;

        public DbConnection Open()
        {
            var parsing = ParseDetail();
            var provider = SchemeMapperBuilder.GetProviderFactory();
            var connection = provider.CreateConnection() ?? throw new ArgumentNullException();
            connection.ConnectionString = parsing.ConnectionString;
            connection.Open();
            return connection;
        }
    }
}
