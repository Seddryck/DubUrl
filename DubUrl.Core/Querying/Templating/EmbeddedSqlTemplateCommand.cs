using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Antlr4.StringTemplate;

namespace DubUrl.Querying.Templating
{
    public class EmbeddedSqlTemplateCommand : EmbeddedSqlFileCommand
    {
        public IDictionary<string, object> Parameters { get; }

        public EmbeddedSqlTemplateCommand(string basePath, IDictionary<string, object> parameters)
            : this(new EmbeddedSqlTemplateResourceManager(Assembly.GetCallingAssembly()), basePath, parameters) { }

        internal EmbeddedSqlTemplateCommand(IResourceManager resourceManager, string basePath, IDictionary<string, object> parameters)
            : base(resourceManager, basePath) => Parameters = parameters;

        public override string Read(IDialect dialect)
            => new StringTemplateEngine().Render(base.Read(dialect), Parameters, dialect.Renderer);
    }
}
