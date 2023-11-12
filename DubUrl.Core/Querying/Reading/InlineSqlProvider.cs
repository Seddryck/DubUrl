using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class InlineSqlProvider : ICommandProvider
    {
        protected string Text { get; }
        private IQueryLogger QueryLogger { get; }

        public InlineSqlProvider(string text, IQueryLogger queryLogger) 
            => (Text, QueryLogger) = (text, queryLogger);

        public string Read(IDialect dialect, IConnectivity connectivity)
        {
            var text = Render(dialect, connectivity);
            QueryLogger.Log(text);
            return text;
        }

        protected virtual string Render(IDialect dialect, IConnectivity connectivity)
            => Text;

        public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => true;
    }
}
