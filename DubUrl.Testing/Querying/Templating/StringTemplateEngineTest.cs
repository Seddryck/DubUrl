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
    }
}
