using Amazon.Runtime.Internal.Transform;
using DubUrl.Querying.Templating;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Templating;

public class DidotEngineTest
{
    [Test]
    [TestCase(".st", "Hello $name$!")]
    [TestCase(".scriban", "Hello {{name}}!")]
    [TestCase(".hdb", "Hello {{name}}!")]
    [TestCase(".mustache", "Hello {{name}}!")]
    [TestCase(".liquid", "Hello {{name}}!")]
    public void Render_WithoutSubTemplate_Correct(string extension, string template)
    {
        var engine = new DidotEngine(extension);
        var renderer = engine.Prepare(template);
        var response = renderer.Render(new Dictionary<string, object?>() { { "name", "Cédric" } });
        Assert.That(response, Is.EqualTo("Hello Cédric!"));
    }

    [Test]
    public void Render_WithSubTemplate_Correct()
    {
        var engine = new DidotEngine(".st");
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" } };
        var subTemplates = new Dictionary<string, string>()
        {
            { "print_name", "dear $name$" },
        };
        var renderer = engine.Prepare("Hello $print_name()$!", subTemplates);
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
    }

    [Test]
    public void Render_WithMultipleSubTemplate_Correct()
    {
        var engine = new DidotEngine(".st");
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" } };
        var subTemplates = new Dictionary<string, string>() 
        { 
            { "print_name", "dear $name$" },
            { "print_end", "!" }
        };
        var renderer = engine.Prepare("Hello $print_name()$$print_end()$", subTemplates);
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
    }

    [Test]
    public void Render_WithChainingSubTemplate_Correct()
    {
        var engine = new DidotEngine(".st");
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" } };
        var subTemplates = new Dictionary<string, string>()
        {
            { "print_name", "dear $name$$print_end()$" },
            { "print_end", "!" }
        };
        var renderer = engine.Prepare("Hello $print_name()$", subTemplates);
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
    }

    [Test]
    public void Render_WithChainingSubTemplateWithVariables_Correct()
    {
        var engine = new DidotEngine(".st");
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "template", "print_end" } };
        var subTemplates = new Dictionary<string, string>()
        {
            { "print_name", "dear $(template)()$" },
            { "print_end", "$name$!" }
        };
        var renderer = engine.Prepare("Hello $print_name()$", subTemplates);
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
    }

    [Test]
    public void Render_WithDictionary_Correct()
    {
        var engine = new DidotEngine(".st");
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "symbol", "LessThan" } };
        var dictionary = new Dictionary<string, object?>()
        {
            { "LessThan", "<" },
            { "GreaterThan", ">" }
        };
        var renderer = engine.Prepare("Hello $name$ $comparison.GreaterThan$$comparison.(symbol)$", dictionaries: new Dictionary<string, IDictionary<string, object?>>() { { "comparison", dictionary } });
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello Cédric ><"));
    }


    [Test]
    public void Render_WithTemplateParsable_Correct()
    {
        var engine = new DidotEngine(".st");
        var templates = new Dictionary<string, string>()
        {
            { "bold", "bold(value)::=<b>$value$</b>" },
            { "italic", "italic(value)::=<i>$value$</i>" }
        };
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "template", "italic" } };
        var renderer = engine.Prepare("Hello $(template)(name)$", templates);
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello <i>Cédric</i>"));
    }

    [Test]
    public void Render_WithTemplateParsableMultiVar_Correct()
    {
        var engine = new DidotEngine(".st");
        var templates = new Dictionary<string, string>()
        {
            { "bold", "bold(value,value2)::=<b>$value$</b>" },
            { "italic", "italic (value, value2) ::=<i>$value$</i>$value2$" }
        };
        var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "template", "italic" }, { "punc", "!" } };
        var renderer = engine.Prepare("Hello $(template)(name, punc)$", templates);
        var response = renderer.Render(parameters);
        Assert.That(response, Is.EqualTo("Hello <i>Cédric</i>!"));
    }
}
