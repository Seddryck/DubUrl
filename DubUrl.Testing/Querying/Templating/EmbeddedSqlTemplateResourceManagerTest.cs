using DubUrl.Querying.Dialects;
using DubUrl.Querying.Templating;
using Moq;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading
{
    
    public class EmbeddedSqlTemplateResourceManagerTest
    {
        private class FakeEmbeddedSqlTemplateResourceManager : EmbeddedSqlTemplateResourceManager
        {
            private string[] resourceNames;
            public override string[] ResourceNames { get => resourceNames; }
            public FakeEmbeddedSqlTemplateResourceManager(string[] resourceNames)
                : base(Assembly.GetCallingAssembly())
                => this.resourceNames = resourceNames;
        }

        private class FakeEmbeddedSqlTemplateResourceManagerForDictionary : EmbeddedSqlTemplateResourceManager
        {
            private string Content;
            protected override TextReader GetResourceReader(string resourceName)
                => new StringReader(Content);
            
            public FakeEmbeddedSqlTemplateResourceManagerForDictionary(string content)
                : base(Assembly.GetCallingAssembly())
                => this.Content = content;
        }

        [Test]
        public void ListResources_SingleTemplate_SingleResult()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManager(new[] { "Foo.QueryId.sql.st", "Foo.Bar.DuckDb.T0.sql.st" });
            var resources = resourceManager.ListResources("Foo.Bar", new[] { "duck", "duckdb" }, string.Empty);
            Assert.That(resources, Has.Count.EqualTo(1));
            Assert.That(resources, Does.ContainKey("T0"));
            Assert.That(resources["T0"], Is.EqualTo("Foo.Bar.DuckDb.T0.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ListResources_MultipleTemplates_MultipleResults()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManager(new[] 
            { 
                "Foo.QueryId.sql.st", 
                "Foo.Bar.DuckDb.T0.sql.st",
                "Foo.Bar.MsSQL.T0.sql.st",
                "Foo.Bar.DuckDb.T1.sql.st",
                "Foo.Bar.T1.sql.st",
                "Foo.Bar.T2.sql.st"
            });
            var resources = resourceManager.ListResources("Foo.Bar", new[] { "duck", "duckdb" }, string.Empty);
            Assert.That(resources, Has.Count.EqualTo(3));
            Assert.That(resources, Does.ContainKey("T0"));
            Assert.That(resources["T0"], Is.EqualTo("Foo.Bar.DuckDb.T0.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T1"));
            Assert.That(resources["T1"], Is.EqualTo("Foo.Bar.DuckDb.T1.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T2"));
            Assert.That(resources["T2"], Is.EqualTo("Foo.Bar.T2.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ListResources_MultipleTemplatesWithSubFolders_MultipleResults()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManager(new[]
            {
                "Foo.QueryId.sql.st",
                "Foo.Bar.Qrz.DuckDb.T0.sql.st",
                "Foo.Bar.Qrz.MsSQL.T0.sql.st",
                "Foo.Bar.DuckDb.T1.sql.st",
                "Foo.Bar.T1.sql.st",
                "Foo.Bar.Baz.Bob.T2.sql.st"
            });
            var resources = resourceManager.ListResources("Foo.Bar", new[] { "duck", "duckdb" }, string.Empty);
            Assert.That(resources, Has.Count.EqualTo(3));
            Assert.That(resources, Does.ContainKey("T0"));
            Assert.That(resources["T0"], Is.EqualTo("Foo.Bar.Qrz.DuckDb.T0.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T1"));
            Assert.That(resources["T1"], Is.EqualTo("Foo.Bar.DuckDb.T1.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T2"));
            Assert.That(resources["T2"], Is.EqualTo("Foo.Bar.Baz.Bob.T2.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ListResources_TemplateWithSubFoldersNoMatchingDialect_MultipleResults()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManager(new[]
            {
                "Foo.Bar.Baz.Bob.MsSQL.T2.sql.st",
                "Foo.Bar.Baz.Bob.T2.sql.st",
            });
            var resources = resourceManager.ListResources("Foo.Bar", new[] { "duck", "duckdb" }, string.Empty);
            Assert.That(resources, Has.Count.EqualTo(1));
            Assert.That(resources, Does.ContainKey("T2"));
            Assert.That(resources["T2"], Is.EqualTo("Foo.Bar.Baz.Bob.T2.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ListResources_MultipleTemplatesWithSubFoldersNotEndingByDialect_MultipleResults()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManager(new[]
            {
                "Foo.QueryId.sql.st",
                "Foo.Bar.DuckDb.Qrz.T0.sql.st",
                "Foo.Bar.MsSQL.Qrz.T0.sql.st",
                "Foo.Bar.DuckDb.Qrz.T1.sql.st",
                "Foo.Bar.Qrz.T1.sql.st",
                "Foo.Bar.DuckDb.Baz.T2.sql.st"
            });
            var resources = resourceManager.ListResources("Foo.Bar", new[] { "duck", "duckdb" }, string.Empty);
            Assert.That(resources, Has.Count.EqualTo(3));
            Assert.That(resources, Does.ContainKey("T0"));
            Assert.That(resources["T0"], Is.EqualTo("Foo.Bar.DuckDb.Qrz.T0.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T1"));
            Assert.That(resources["T1"], Is.EqualTo("Foo.Bar.DuckDb.Qrz.T1.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T2"));
            Assert.That(resources["T2"], Is.EqualTo("Foo.Bar.DuckDb.Baz.T2.sql.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ListResources_MultipleDictionaries_MultipleResults()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManager(new[]
            {
                "Foo.QueryId.dic.st",
                "Foo.Bar.DuckDb.Qrz.T0.dic.st",
                "Foo.Bar.MsSQL.Qrz.T0.dic.st",
                "Foo.Bar.DuckDb.Qrz.T1.dic.st",
                "Foo.Bar.Qrz.T1.dic.st",
                "Foo.Bar.DuckDb.Baz.T2.dic.st"
            });
            var resources = resourceManager.ListResources("Foo.Bar", new[] { "duck", "duckdb" }, string.Empty, ".dic.st");
            Assert.That(resources, Has.Count.EqualTo(3));
            Assert.That(resources, Does.ContainKey("T0"));
            Assert.That(resources["T0"], Is.EqualTo("Foo.Bar.DuckDb.Qrz.T0.dic.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T1"));
            Assert.That(resources["T1"], Is.EqualTo("Foo.Bar.DuckDb.Qrz.T1.dic.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("T2"));
            Assert.That(resources["T2"], Is.EqualTo("Foo.Bar.DuckDb.Baz.T2.dic.st").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }

        [Test]
        public void ReadDict_SingleDictionary_MultipleResults()
        {
            var resourceManager = new FakeEmbeddedSqlTemplateResourceManagerForDictionary("firstName:\"Cédric\"\r\nage:44");
            var resources = resourceManager.ReadDictionary("resourceName");
            Assert.That(resources, Has.Count.EqualTo(2));
            Assert.That(resources, Does.ContainKey("firstName"));
            Assert.That(resources["firstName"], Is.EqualTo("Cédric").Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
            Assert.That(resources, Does.ContainKey("age"));
            Assert.That(resources["age"], Is.EqualTo(44).Using((IComparer)StringComparer.InvariantCultureIgnoreCase));
        }
    }
}
