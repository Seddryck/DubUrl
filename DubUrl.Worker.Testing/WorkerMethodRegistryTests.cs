using NUnit.Framework;

namespace DubUrl.Worker.Testing;

public class WorkerMethodRegistryTests
{
    [Test]
    public void Register_WithDuplicateName_Throws()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<object, object>("test.echo", "Echoes input.", Echo);

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register<object, object>("test.echo", "Echoes input.", Echo));

        Assert.That(exception!.Message, Does.Contain("test.echo"));
    }

    [Test]
    public void Capabilities_AreSortedByName()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<object, object>("test.second", "Second.", Echo);
        registry.Register<object, object>("test.first", "First.", Echo);

        Assert.That(
            registry.Capabilities.Select(capability => capability.Name),
            Is.EqualTo(new[] { "test.first", "test.second" }));
    }

    private static ValueTask<object> Echo(object parameters, CancellationToken cancellationToken)
        => ValueTask.FromResult(parameters);
}
