using DubUrl.Adomd.Mapping;
using DubUrl.Adomd.Wrappers;
using DubUrl.Mapping;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing;

public class ConnectionUrlTest
{
    [SetUp]
    public void DefaultRegistration()
        => DbProviderFactories.RegisterFactory("Microsoft.AnalysisServices.AdomdClient"
            , AdomdFactory.Instance);

    [Test]
    public void Connect_ValidUrl_AdomdConnection()
    {
        var schemeMapperBuilder = new SchemeMapperBuilder(new[] { typeof(PowerBiPremiumMapper).Assembly });
        var url = "powerbi://api.powerbi.com/v1.0/foo/bar";
        var connectionUrl = new ConnectionUrl(url, schemeMapperBuilder);
        var conn = connectionUrl.Connect();
        Assert.That(conn, Is.Not.Null);
        Assert.That(conn, Is.TypeOf<AdomdConnectionWrapper>());
    }
}
