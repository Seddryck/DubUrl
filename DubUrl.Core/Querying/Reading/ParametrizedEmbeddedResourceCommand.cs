using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading;

public class ParametrizedEmbeddedResourceCommand : EmbeddedResourceCommand, IParametrizedCommand
{
    public ImmutableArray<DubUrlParameter> Parameters { get; }

    public ParametrizedEmbeddedResourceCommand(string basePath, DubUrlParameterCollection parameters, IQueryLogger queryLogger)
        : this(basePath, parameters.ToArray(), queryLogger) { }

    public ParametrizedEmbeddedResourceCommand(string basePath, DubUrlParameter[] parameters, IQueryLogger queryLogger)
        : this(new EmbeddedResourceManager(Assembly.GetCallingAssembly()), basePath, parameters, queryLogger) { }

    public ParametrizedEmbeddedResourceCommand(IResourceManager resourceManager, string basePath, DubUrlParameterCollection parameters, IQueryLogger queryLogger)
        : this(resourceManager, basePath, parameters.ToArray(), queryLogger) { }

    public ParametrizedEmbeddedResourceCommand(IResourceManager resourceManager, string basePath, DubUrlParameter[] parameters, IQueryLogger queryLogger)
        : base(resourceManager, basePath, queryLogger)
        => Parameters = parameters.ToImmutableArray();
}
