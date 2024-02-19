using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing.Mapping;
public class SchemeNormalizerTest
{
    [Test]
    public void Normalize_SingleScheme_Normalized()
    {
        var normalizer = new SchemeNormalizer(new Dictionary<string, ISchemeHandler>() { { "odbc", new OdbcConnectivity() } });
        var result = normalizer.Normalize("mssql");
        Assert.That(result, Is.EqualTo("mssql"));
    }

    [Test]
    [TestCase("odbc+mssql")]
    [TestCase("mssql+odbc")]
    public void Normalize_WrapperScheme_Normalized(string scheme)
    {
        var normalizer = new SchemeNormalizer(new Dictionary<string, ISchemeHandler>() { { "odbc", new OdbcConnectivity() } });
        var result = normalizer.Normalize(scheme);
        Assert.That(result, Is.EqualTo("odbc+mssql"));
    }

    [Test]
    [TestCase("odbc+mssql+fake")]
    [TestCase("mssql+odbc+fake")]
    [TestCase("fake+mssql+odbc")]
    [TestCase("fake+odbc+mssql")]
    [TestCase("mssql+fake+odbc")]
    [TestCase("odbc+fake+mssql")]
    public void Normalize_TripleScheme_Normalized(string scheme)
    {
        var fakeSchemeHandler = Mock.Of<ISchemeHandler>(x => x.Schemes == new string[] { "fake" });
        var normalizer = new SchemeNormalizer(
            new Dictionary<string, ISchemeHandler>()
            {
                { "odbc", new OdbcConnectivity() },
                { "fake", fakeSchemeHandler }
            });
        var result = normalizer.Normalize(scheme);
        Assert.That(result, Is.EqualTo("odbc+mssql+fake"));
    }
}
