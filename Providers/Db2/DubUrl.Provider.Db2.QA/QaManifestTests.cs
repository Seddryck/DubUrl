using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Provider.Db2.QA;
public sealed class QaManifestTests
{
    [Test, Category("Manifest")]
    public void ManifestIsValid() => QaManifest.Validate(Path.Combine(AppContext.BaseDirectory,"qa-manifest.json"),"db2","Connection","Querying","Schema");
}
