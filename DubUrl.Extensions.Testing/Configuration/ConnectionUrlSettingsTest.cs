using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Extensions.Testing.Configuration;

public class ConnectionUrlSettingsTest
{
    [Test]
    [TestCase("mssql")]
    [TestCase("oledb+mssql")]
    public void ToString_Scheme_Valid(string scheme)
    {
        var settings = new ConnectionUrlSettings()
        {
            Scheme = scheme,
            Host = "localhost"
        };
        Assert.That(settings.ToString(), Is.EqualTo($"{scheme}://localhost/"));
    }

    [Test]
    [TestCase("127.0.0.1")]
    [TestCase("localhost")]
    [TestCase("database.url.com")]
    public void ToString_Host_Valid(string host)
    {
        var settings = new ConnectionUrlSettings()
        {
            Scheme = "mssql",
            Host = host
        };
        Assert.That(settings.ToString(), Is.EqualTo($"mssql://{host}/"));
    }

    [Test]
    [TestCase(1234)]
    public void ToString_Port_Valid(int port)
    {
        var settings = new ConnectionUrlSettings()
        {
            Scheme = "mssql",
            Host = "localhost",
            Port = port
        };
        Assert.That(settings.ToString(), Is.EqualTo($"mssql://localhost:{port}/"));
    }

    [Test]
    [TestCase("foo", "bar")]
    public void ToString_Credentials_Valid(string username, string password)
    {
        var settings = new ConnectionUrlSettings()
        {
            Scheme = "mssql",
            Host = "localhost",
            Username = username,
            Password = password
        };
        Assert.That(settings.ToString(), Is.EqualTo($"mssql://foo:bar@localhost/"));
    }

    [Test]
    [TestCase("foo")]
    [TestCase("foo:bar")]
    [TestCase("foo:bar:brz")]
    public void ToString_Segments_Valid(string segments)
    {
        var settings = new ConnectionUrlSettings()
        {
            Scheme = "mssql",
            Host = "localhost",
            Segments = segments.Split(":")
        };
        Assert.That(settings.ToString(), Is.EqualTo($"mssql://localhost/{segments.Replace(':','/')}"));
    }

    [Test]
    [TestCase("foo:1", "bar:2")]
    public void ToString_Parameters_Valid(string param1, string param2)
    {
        var settings = new ConnectionUrlSettings()
        {
            Scheme = "mssql",
            Host = "localhost",
            Keys = new[] { param1.Split(':')[0], param2.Split(':')[0] },
            Values = new[] { param1.Split(':')[1], param2.Split(':')[1] }
        };
        Assert.That(settings.ToString(), Is.EqualTo($"mssql://localhost/?foo=1&bar=2"));
    }
}
