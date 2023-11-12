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
        private IPrqlCompiler PrqlCompiler { get; }

        public InlinePrqlProvider(string text, IQueryLogger queryLogger)
            : this(text, queryLogger, true, false)
        { }

        public InlinePrqlProvider(string text, IQueryLogger queryLogger, bool format, bool signatureComment)
            : base(text, queryLogger)
        {
            PrqlCompiler = new PrqlCompilerWrapper(queryLogger, format, signatureComment);
        }

        internal InlinePrqlProvider(string text, IQueryLogger queryLogger, IPrqlCompiler compiler)
            : base(text, queryLogger)
        {
            PrqlCompiler = compiler;
        }

        protected override string Render(IDialect dialect, IConnectivity connectivity)
            => PrqlCompiler.ToSql(Text, dialect);
    }
}
