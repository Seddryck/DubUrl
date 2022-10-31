using DubUrl.Mapping;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading.ResourceManagement;
using DubUrl.Querying.Reading.ResourceMatching;
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
    public class EmbeddedSqlFileCommand: ICommandProvider
    {
        protected internal string BasePath { get; }
        private IResourceManager ResourceManager { get; }
        private IResourceMatcherFactory ResourceMatcherFactory { get; }

        public EmbeddedSqlFileCommand(string basePath)
            : this(new EmbeddedSqlFileResourceManager(Assembly.GetCallingAssembly()), new ResourceMatcherFactory(), basePath) { }

        internal EmbeddedSqlFileCommand(IResourceManager resourceManager, IResourceMatcherFactory resourceMatcherFactory, string basePath)
            => (BasePath, ResourceManager, ResourceMatcherFactory) = (basePath, resourceManager, resourceMatcherFactory);

        internal string Locate(IConnectivity connectivity, IDialect dialect)
        {
            var matcher = ResourceMatcherFactory.Instantiate(connectivity, dialect.Aliases);
            var resourceName = matcher.Execute(BasePath, ResourceManager.ListResources());
            if (string.IsNullOrEmpty(resourceName))
                throw new MissingCommandForDialectException(this, dialect);

            return resourceName;
        }

        public string Read(IDialect dialect, IConnectivity connectivity)
            => ResourceManager.ReadCommandText(Locate(connectivity, dialect));
    }

}
