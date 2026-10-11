using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Providers.MsAccess.QA;
public sealed class QaManifestTests
{
    [Test, Category("Manifest")]
    public void ManifestIsValid() => QaManifest.Validate(Path.Combine(AppContext.BaseDirectory, "qa-manifest.json"), "msaccess", "Connection", "Querying", "ODBC", "OleDB");
}
