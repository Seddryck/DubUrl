using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ddt = Didot.Core;
using System.Reflection;
using DubUrl.Querying.Dialects.Renderers;
using System.Text.Encodings.Web;

namespace DubUrl.Schema.Templating;
public abstract class RendererEngine
{
    protected Ddt.ITemplateEngine Engine { get; }
    private string TemplatePath { get; }
    private Assembly Assembly { get; }

    protected RendererEngine(Assembly asm, string templatePath)
    {
        var extension = Path.GetExtension(templatePath);
        Assembly = asm;
        TemplatePath = templatePath;
        var factory = Ddt.TemplateEngineFactory.Default;
        Engine = factory.Create(extension);
    }

    protected static Dictionary<string, Func<object?, string>> CreateHelpers(IRenderer renderer)
    {
        KeyValuePair<string, Func<object?, string>> create(string name) =>
            new(name, value => renderer.Render(value, name));

        return new([create("value"), create("identity")]);
    }

    protected void AddMappings(string mapKey, IDictionary<string, object> mappings)
        => Engine.AddMappings(mapKey, mappings);

    protected void AddFormatter(string functionName, Func<object?, string> function)
        => Engine.AddFormatter(functionName, function);

    public string Render(object value)
    {
        using (var stream = Assembly.GetManifestResourceStream(TemplatePath) ?? throw new FileNotFoundException(TemplatePath))
        return Engine.Render(stream, value);
    }
}
