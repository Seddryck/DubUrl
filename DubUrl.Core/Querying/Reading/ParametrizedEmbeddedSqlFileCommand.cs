using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class ParametrizedEmbeddedSqlFileCommand : EmbeddedSqlFileCommand, IParametrizedCommand
    {
        public ImmutableArray<DubUrlParameter> Parameters { get; }

        public ParametrizedEmbeddedSqlFileCommand(string basePath, DubUrlParameterCollection parameters)
            : this(basePath, parameters.ToArray()) { }

        public ParametrizedEmbeddedSqlFileCommand(string basePath, DubUrlParameter[] parameters)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), basePath, parameters) { }

        public ParametrizedEmbeddedSqlFileCommand(IResourceManager resourceManager, string basePath, DubUrlParameterCollection parameters)
            : this(resourceManager, basePath, parameters.ToArray()) { }

        public ParametrizedEmbeddedSqlFileCommand(IResourceManager resourceManager, string basePath, DubUrlParameter[] parameters)
            : base(resourceManager, basePath)
            => Parameters = parameters.ToImmutableArray();
    }
}
