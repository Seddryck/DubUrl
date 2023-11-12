using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Mapping.Database;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Prql.Compiler;

namespace DubUrl.Prql
{
    internal class InlinePrqlProvider : InlineSqlProvider
    {
        private PrqlCompilerOptions Options { get; }

        public InlinePrqlProvider(string text, IQueryLogger queryLogger)
            : this(text, queryLogger, true, false)
        { }

        public InlinePrqlProvider(string text, IQueryLogger queryLogger, bool format, bool signatureComment)
            : base(text, queryLogger)
        {
            Options = new PrqlCompilerOptions() { Format = format, SignatureComment = signatureComment };
        }

        protected override string Render(IDialect dialect, IConnectivity connectivity)
        {
            var compilerOptions = Options with { Target = MatchPrqlDialect(dialect) };
            var result = PrqlCompiler.Compile(Text, compilerOptions);
            return result.Output;
        }

        protected virtual string MatchPrqlDialect(IDialect dialect)
            => dialect switch
            {
                AnsiDialect => "sql.ansi",
                //BigQueryDialect => "sql.bigquery",
                //ClickHouseDialect => "sql.clickhouse",
                DuckdbDialect => "sql.duckdb",
                //GlareDbDialect => "sql.glaredb",
                //HiveDialect => "sql.hive",
                MySqlDialect => "sql.mysql",
                PgsqlDialect => "sql.postgres",
                SqliteDialect => "sql.sqlite",
                SnowflakeDialect => "sql.snowflake",
                TSqlDialect => "sql.mssql",
                _ => "sql.generic",
            };
    }
}
