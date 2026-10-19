using DubUrl.Mapping;
using DubUrl.Parsing;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class ResolutionDiagnosticsTest
{
    [Test]
    public void TryResolve_UnknownScheme_ReturnsStructuredDiagnostic()
    {
        var registry = new SchemeRegistry([]);
        var connectionUri = new ConnectionUri { Scheme = new SchemeKey(["unknown"]) };

        var success = registry.TryResolve(connectionUri, out var resolved, out var diagnostic);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(resolved, Is.Null);
            Assert.That(diagnostic?.Code, Is.EqualTo(ConnectionResolutionDiagnosticCode.SchemeNotFound));
        });
    }
}
