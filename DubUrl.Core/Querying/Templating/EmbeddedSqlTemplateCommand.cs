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
using DubUrl.Mapping;
using System.Resources;

namespace DubUrl.Querying.Templating
{
    public class EmbeddedSqlTemplateCommand : EmbeddedSqlFileCommand
    {
        public IDictionary<string, object?> Parameters { get; }
        public string SubTemplatesPath { get; }
        public string DictionariesPath { get; }


        protected new IResourceTemplateManager ResourceManager { get => (IResourceTemplateManager)base.ResourceManager; }

        public EmbeddedSqlTemplateCommand(string basePath, IDictionary<string, object?> parameters)
            : this(new EmbeddedSqlTemplateResourceManager(Assembly.GetCallingAssembly()), basePath, string.Empty, string.Empty, parameters) { }

        public EmbeddedSqlTemplateCommand(string basePath, string subTemplatesPath, string dictionariesPath, IDictionary<string, object?> parameters)
            : this(new EmbeddedSqlTemplateResourceManager(Assembly.GetCallingAssembly()), basePath, subTemplatesPath, dictionariesPath, parameters) { }

        internal EmbeddedSqlTemplateCommand(IResourceManager resourceManager, string basePath, string subTemplatesPath, string dictionariesPath, IDictionary<string, object?> parameters)
            : base(resourceManager, basePath)
        {
            Parameters = parameters;
            SubTemplatesPath = subTemplatesPath;
            DictionariesPath = dictionariesPath;
        }

        protected IDictionary<string, string> ReadSubTemplates(IDialect dialect, IConnectivity connectivity)
        {
            var dico = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(SubTemplatesPath))
                return dico;
            else
            {
                var resources = ResourceManager.ListResources(SubTemplatesPath, dialect.Aliases, connectivity.Alias, "sql.st");
                foreach (var resource in resources)
                    dico.Add(resource.Key, ResourceManager.ReadResource(resource.Value));
            }
            return dico;
        }

        protected IDictionary<string, IDictionary<string, object?>> ReadDictionaries(IDialect dialect, IConnectivity connectivity)
        {
            var dico = new Dictionary<string, IDictionary<string, object?>>();
            if (string.IsNullOrEmpty(DictionariesPath))
                return dico;
            else
            {
                var resources = ResourceManager.ListResources(DictionariesPath, dialect.Aliases, connectivity.Alias, "dic.st");
                foreach (var resource in resources)
                    dico.Add(resource.Key, ResourceManager.ReadDictionary(resource.Value));
            }
            return dico;
        }

        public override string Read(IDialect dialect, IConnectivity connectivity)
            => new StringTemplateEngine().Render(
                    base.Read(dialect, connectivity)
                    , ReadSubTemplates(dialect, connectivity)
                    , ReadDictionaries(dialect, connectivity)
                    , Parameters
                    , dialect.Renderer
                );
    }
}
