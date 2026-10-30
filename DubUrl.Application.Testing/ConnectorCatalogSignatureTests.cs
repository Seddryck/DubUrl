using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class ConnectorCatalogSignatureTests
{
    [TestCase("provider-name_1.2", true)]
    [TestCase("0", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("-provider", false)]
    [TestCase("provider/name", false)]
    [TestCase("provider\n", false)]
    [TestCase("éprovider", false)]
    [TestCase("provider１", false)]
    public void SafeNames_KeepTheCatalogAsciiIdentifierContract(string? name, bool expected)
        => Assert.That(ConnectorCatalogSignature.IsSafeName(name), Is.EqualTo(expected));

    [Test]
    public void SafeNames_EnforceTheMaximumLengthBeforeScanning()
    {
        Assert.That(ConnectorCatalogSignature.IsSafeName(new string('a', 128)), Is.True);
        Assert.That(ConnectorCatalogSignature.IsSafeName(new string('a', 129)), Is.False);
    }
}
