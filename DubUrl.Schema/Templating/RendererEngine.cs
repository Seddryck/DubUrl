using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Didot.Core.TemplateEngines;
using Didot.Core;
using System.Reflection;

namespace DubUrl.Schema.Templating;
public abstract class RendererEngine
{
    protected ITemplateEngine Engine { get; }
    private string TemplatePath { get; }
    private Assembly Assembly { get; }

    protected RendererEngine(Assembly asm, string templatePath)
    {
        var extension = Path.GetExtension(templatePath);
        Assembly = asm;
        TemplatePath = templatePath;
        Engine = new FileBasedTemplateEngineFactory().GetByExtension(extension);
    }

    protected void AddMappings(string mapKey, IDictionary<string, object> mappings)
        => Engine.AddMappings(mapKey, mappings);

    public string Render(object value)
    {
        using (var stream = Assembly.GetManifestResourceStream(TemplatePath) ?? throw new FileNotFoundException(TemplatePath))
        return Engine.Render(stream, value);
    }
}
