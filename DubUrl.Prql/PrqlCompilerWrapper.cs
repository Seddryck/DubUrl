using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using Prql.Compiler;
using static System.Net.Mime.MediaTypeNames;

namespace DubUrl.Prql
{
    internal class PrqlCompilerWrapper : IPrqlCompiler
    {
        private PrqlCompilerOptions Options { get; }
        private IQueryLogger QueryLogger { get; }

        public PrqlCompilerWrapper(IQueryLogger queryLogger, bool format, bool signatureComment)
        {
            QueryLogger = queryLogger;  
            Options = new PrqlCompilerOptions() { Format = format, SignatureComment = signatureComment };
        }

        public virtual string ToSql(string text, IDialect dialect)
        {
            var compilerOptions = Options with { Target = MatchPrqlDialect(dialect) };
            QueryLogger.Log(text);
            var result = PrqlCompiler.Compile(text, compilerOptions);
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
