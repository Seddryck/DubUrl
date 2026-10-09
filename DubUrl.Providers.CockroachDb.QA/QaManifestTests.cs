using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.CockroachDb.QA;

public sealed class QaManifestTests
{
    [Test, Category("Manifest")]
    public void ManifestIsValid()
        => QaManifest.Validate(Path.Combine(AppContext.BaseDirectory, "qa-manifest.json"), "cockroachdb", "Connection", "Querying");
}
