using System.Reflection;
using System.Runtime.Loader;
using NUnit.Framework;

namespace DubUrl.BulkCopy.Testing;

public class BulkCopyDependencyResolverTests
{
    private const string DependencyName = "DubUrl.BulkCopy.Testing.Dependency";
    private const string HelperType = DependencyName + ".OptionalHelper";
    private static string DependencyDirectory => Path.Combine(TestContext.CurrentContext.TestDirectory, "optional-dependencies");

    [Test]
    public void ConstructorAndScope_DoNotLoadOptionalAssembly()
    {
        var context = new AssemblyLoadContext("bulk-lazy", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            var resolver = new BulkCopyDependencyResolver([DependencyDirectory]);
            using var scope = resolver.EnterScope(anchor);

            Assert.That(context.Assemblies.Select(x => x.GetName().Name), Does.Not.Contain(DependencyName));
        }
        finally { context.Unload(); }
    }

    [Test]
    public void ExplicitDirectory_LoadsUnreferencedDllInDriversContext()
    {
        var context = new AssemblyLoadContext("bulk-optional", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            var resolver = new BulkCopyDependencyResolver([DependencyDirectory]);

            var type = resolver.ResolveType(anchor, DependencyName, HelperType);
            var value = type.GetMethod("GetValue")!.Invoke(null, null);

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo("loaded lazily"));
                Assert.That(AssemblyLoadContext.GetLoadContext(type.Assembly), Is.SameAs(context));
                Assert.That(AssemblyLoadContext.Default.Assemblies.Select(x => x.GetName().Name), Does.Not.Contain(DependencyName));
                Assert.That(typeof(BulkCopyDependencyResolverTests).Assembly.GetReferencedAssemblies()
                    .Select(x => x.Name), Does.Not.Contain(DependencyName));
            });
        }
        finally { context.Unload(); }
    }

    [Test]
    public void Scope_ResolvesDriverTriggeredDependencyAndDetachesOnSuccess()
    {
        var context = new AssemblyLoadContext("bulk-scoped", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            using (new BulkCopyDependencyResolver([DependencyDirectory]).EnterScope(anchor))
                Assert.That(context.LoadFromAssemblyName(new AssemblyName(DependencyName)), Is.Not.Null);

            var exception = Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("AbsentAfterSuccess")));
            Assert.That(exception!.ToString(), Does.Not.Contain("Optional bulk-copy dependency"));
        }
        finally { context.Unload(); }
    }

    [Test]
    public void MissingDependency_HasActionableErrorAndScopeDetachesAfterFailure()
    {
        var context = new AssemblyLoadContext("bulk-missing", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            var resolver = new BulkCopyDependencyResolver();
            var exception = Assert.Throws<FileNotFoundException>(() => resolver.ResolveType(anchor, "MissingBulkHelper", "Example.Helper"));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.ToString(), Does.Contain("MissingBulkHelper"));
                Assert.That(exception.ToString(), Does.Contain("search directories"));
                Assert.That(exception.ToString(), Does.Contain("No compile-time reference"));
            });
            var after = Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("AbsentAfterFailure")));
            Assert.That(after!.ToString(), Does.Not.Contain("Optional bulk-copy dependency"));
        }
        finally { context.Unload(); }
    }

    [Test]
    public void DriverDirectory_IsProbedWithoutExplicitSearchDirectories()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DubUrl-bulk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var context = new AssemblyLoadContext("bulk-adjacent", isCollectible: true);
        try
        {
            var source = typeof(IBulkCopyEngine).Assembly.Location;
            var anchorPath = Path.Combine(directory, Path.GetFileName(source));
            File.Copy(source, anchorPath);
            File.Copy(Path.Combine(DependencyDirectory, DependencyName + ".dll"), Path.Combine(directory, DependencyName + ".dll"));
            var anchor = context.LoadFromAssemblyPath(anchorPath);

            var type = BulkCopyDependencyResolver.Default.ResolveType(anchor, DependencyName, HelperType);

            Assert.That(AssemblyLoadContext.GetLoadContext(type.Assembly), Is.SameAs(context));
        }
        finally
        {
            context.Unload();
            // Windows can retain mapped assemblies until the collectible context is reclaimed.
            try
            {
                File.Delete(Path.Combine(directory, "DubUrl.Core.dll"));
                File.Delete(Path.Combine(directory, DependencyName + ".dll"));
                Directory.Delete(directory);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Test]
    public void MissingSatelliteAssembly_AllowsNormalResourceFallback()
    {
        var context = new AssemblyLoadContext("bulk-resources", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            using var scope = BulkCopyDependencyResolver.Default.EnterScope(anchor);
            var error = Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("Missing.resources")));
            Assert.That(error!.ToString(), Does.Not.Contain("Optional bulk-copy dependency"));
        }
        finally { context.Unload(); }
    }

    [Test]
    public void ConcurrentScopes_AllowLaterResolversToSupplyDependencies()
    {
        var context = new AssemblyLoadContext("bulk-multiple-scopes", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            using var empty = BulkCopyDependencyResolver.Default.EnterScope(anchor);
            using var configured = new BulkCopyDependencyResolver([DependencyDirectory]).EnterScope(anchor);

            var assembly = context.LoadFromAssemblyName(new AssemblyName(DependencyName));

            Assert.That(AssemblyLoadContext.GetLoadContext(assembly), Is.SameAs(context));
        }
        finally { context.Unload(); }
    }

    [Test]
    public void DriverTriggeredMissingDependency_ReportsReflectedFailureAndDetaches()
    {
        var context = new AssemblyLoadContext("bulk-reflected-failure", isCollectible: true);
        try
        {
            var anchor = LoadAnchor(context);
            var exception = Assert.Throws<FileNotFoundException>(() => BulkCopyDependencyResolver.Default.Execute(anchor,
                () => throw new TargetInvocationException(new FileNotFoundException("missing", "AdditionalDriverHelper"))));

            Assert.That(exception!.Message, Does.Contain("AdditionalDriverHelper").And.Contain("search directories"));
            var after = Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("AbsentAfterReflectedFailure")));
            Assert.That(after!.ToString(), Does.Not.Contain("Optional bulk-copy dependency"));
        }
        finally { context.Unload(); }
    }

    private static Assembly LoadAnchor(AssemblyLoadContext context)
    {
        using var stream = File.OpenRead(typeof(IBulkCopyEngine).Assembly.Location);
        return context.LoadFromStream(stream);
    }
}
