using DubUrl.Parsing;
using NUnit.Framework;

namespace DubUrl.Testing.Parsing;

public class ConnectionUriTest
{
    [Test]
    public void Constructor_MutableCollections_DefensiveCopies()
    {
        var schemes = new[] { "mssql" };
        var segments = new[] { "database" };
        var options = new Dictionary<string, string> { ["Encrypt"] = "true" };

        var connectionUri = new ConnectionUri
        {
            Scheme = new SchemeKey(schemes),
            Segments = segments,
            Options = options,
        };
        schemes[0] = "pgsql";
        segments[0] = "other";
        options["Encrypt"] = "false";

        Assert.Multiple(() =>
        {
            Assert.That(connectionUri.Scheme.Parts, Is.EqualTo(new[] { "mssql" }));
            Assert.That(connectionUri.Segments, Is.EqualTo(new[] { "database" }));
            Assert.That(connectionUri.Options["Encrypt"], Is.EqualTo("true"));
        });
    }

    [Test]
    public void Collections_MutationAttempt_NotSupported()
    {
        var connectionUri = new ConnectionUri
        {
            Scheme = new SchemeKey(["mssql"]),
            Segments = ["database"],
            Options = new Dictionary<string, string> { ["Encrypt"] = "true" },
        };

        Assert.Multiple(() =>
        {
            Assert.Throws<NotSupportedException>(() => ((IList<string>)connectionUri.Scheme.Parts).Add("odbc"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)connectionUri.Segments).Add("other"));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)connectionUri.Options).Add("Timeout", "10"));
        });
    }

    [Test]
    public void WithOption_ExistingInstance_RemainsUnchanged()
    {
        var original = new ConnectionUri { Scheme = new SchemeKey(["mssql"]) };
        var updated = original.WithOption("Encrypt", "true");

        Assert.Multiple(() =>
        {
            Assert.That(original.Options, Is.Empty);
            Assert.That(updated.Options["Encrypt"], Is.EqualTo("true"));
        });
    }

    [Test]
    public void Parse_EncodedPath_PercentDecodesAndPreservesPlus()
    {
        var result = ConnectionUri.Parse("mssql://host/my%20database+archive");
        Assert.That(result.Segments, Is.EqualTo(new[] { "my database+archive" }));
    }

    [Test]
    public void TryParse_DuplicateOption_ReturnsStructuredDiagnostic()
    {
        var success = ConnectionUri.TryParse(
            "mssql://user:secret@host/db?Encrypt=true&encrypt=false",
            out var result,
            out var diagnostic);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(diagnostic?.Code, Is.EqualTo(ConnectionUriDiagnosticCode.DuplicateOption));
            Assert.That(diagnostic?.Message, Does.Not.Contain("secret"));
        });
    }

    [Test]
    public void Parse_DuplicateOption_ThrowsStructuredException()
    {
        var exception = Assert.Throws<ConnectionUriParseException>(() =>
            ConnectionUri.Parse("mssql://host/db?Encrypt=true&encrypt=false"));
        Assert.That(exception!.Diagnostic.Code, Is.EqualTo(ConnectionUriDiagnosticCode.DuplicateOption));
    }

    [Test]
    public void ToString_Password_RedactedByDefault()
    {
        var connectionUri = ConnectionUri.Parse("mssql://user:p%40ss%3Aword@host/db");

        Assert.Multiple(() =>
        {
            Assert.That(connectionUri.ToString(), Does.Contain("user:***@"));
            Assert.That(connectionUri.ToString(), Does.Not.Contain("p%40ss%3Aword"));
            Assert.That(connectionUri.ToString(redactPassword: false), Does.Contain("p%40ss%3Aword"));
        });
    }

    [Test]
    public void ValueEquality_EquivalentInstances_AreEqual()
    {
        var first = ConnectionUri.Parse("MSSQL://host/db?Encrypt=true");
        var second = ConnectionUri.Parse("mssql://HOST/db?encrypt=true");
        Assert.That(first, Is.EqualTo(second));
    }
}
