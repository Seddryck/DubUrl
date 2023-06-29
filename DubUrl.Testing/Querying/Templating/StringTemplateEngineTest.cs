using Amazon.Runtime.Internal.Transform;
using DubUrl.Querying.Templating;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Templating
{
    public class StringTemplateEngineTest
    {
        [Test]
        public void Render_WithoutSubTemplate_Correct()
        {
            var engine = new StringTemplateEngine();
            var response = engine.Render("Hello $name$!", new Dictionary<string, object?>() { { "name", "Cédric" } });
            Assert.That(response, Is.EqualTo("Hello Cédric!"));
        }


        [Test]
        public void Render_WithSubTemplate_Correct()
        {
            var engine = new StringTemplateEngine();
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" } };
            var subTemplates = new Dictionary<string, string>()
            {
                { "print_name", "dear $name$" },
            };
            var response = engine.Render("Hello $print_name()$!", subTemplates, parameters, null);
            Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
        }


        [Test]
        public void Render_WithMultipleSubTemplate_Correct()
        {
            var engine = new StringTemplateEngine();
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" } };
            var subTemplates = new Dictionary<string, string>() 
            { 
                { "print_name", "dear $name$" },
                { "print_end", "!" }
            };
            var response = engine.Render("Hello $print_name()$$print_end()$", subTemplates, parameters, null);
            Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
        }

        [Test]
        public void Render_WithChainingSubTemplate_Correct()
        {
            var engine = new StringTemplateEngine();
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" } };
            var subTemplates = new Dictionary<string, string>()
            {
                { "print_name", "dear $name$$print_end()$" },
                { "print_end", "!" }
            };
            var response = engine.Render("Hello $print_name()$", subTemplates, parameters, null);
            Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
        }

        [Test]
        public void Render_WithChainingSubTemplateWithVariables_Correct()
        {
            var engine = new StringTemplateEngine();
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "template", "print_end" } };
            var subTemplates = new Dictionary<string, string>()
            {
                { "print_name", "dear $(template)()$" },
                { "print_end", "$name$!" }
            };
            var response = engine.Render("Hello $print_name()$", subTemplates, parameters, null);
            Assert.That(response, Is.EqualTo("Hello dear Cédric!"));
        }

        [Test]
        public void Render_WithDictionary_Correct()
        {
            var engine = new StringTemplateEngine();
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "symbol", "LessThan" } };
            var dictionary = new Dictionary<string, object?>()
            {
                { "LessThan", "<" },
                { "GreaterThan", ">" }
            };
            var response = engine.Render("Hello $name$ $comparison.GreaterThan$$comparison.(symbol)$", new Dictionary<string, string>(), new Dictionary<string, IDictionary<string, object?>>() { { "comparison", dictionary } }, parameters, null);
            Assert.That(response, Is.EqualTo("Hello Cédric ><"));
        }


        [Test]
        public void Render_WithTemplateParsable_Correct()
        {
            var engine = new StringTemplateEngine();
            var templates = new Dictionary<string, string>()
            {
                { "bold", "bold(value)::=<b>$value$</b>" },
                { "italic", "italic(value)::=<i>$value$</i>" }
            };
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "template", "italic" } };
            var response = engine.Render("Hello $(template)(name)$", templates, new Dictionary<string, IDictionary<string, object?>>(), parameters, null);
            Assert.That(response, Is.EqualTo("Hello <i>Cédric</i>"));
        }

        [Test]
        public void Render_WithTemplateParsableMultiVar_Correct()
        {
            var engine = new StringTemplateEngine();
            var templates = new Dictionary<string, string>()
            {
                { "bold", "bold(value,value2)::=<b>$value$</b>" },
                { "italic", "italic (value, value2) ::=<i>$value$</i>$value2$" }
            };
            var parameters = new Dictionary<string, object?>() { { "name", "Cédric" }, { "template", "italic" }, { "punc", "!" } };
            var response = engine.Render("Hello $(template)(name, punc)$", templates, new Dictionary<string, IDictionary<string, object?>>(), parameters, null);
            Assert.That(response, Is.EqualTo("Hello <i>Cédric</i>!"));
        }
    }
}
