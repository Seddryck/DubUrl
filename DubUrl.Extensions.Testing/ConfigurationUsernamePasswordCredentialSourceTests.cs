using DubUrl.Authentication;
using DubUrl.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace DubUrl.Extensions.Testing;

public class ConfigurationUsernamePasswordCredentialSourceTests
{
    private static AuthenticationContext Context(string? user = null) => new("Npgsql", "postgres", "localhost", 5432, "app", user);

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReadsRootOrSectionWithoutTransformingOrCachingValues(bool section)
    {
        using var root = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Credentials:User"] = "alice", ["Credentials:Password"] = " sentinel ;%2B+@ " }).Build();
        IConfiguration configuration = section ? root.GetSection("Credentials") : root;
        var userKey = section ? "User" : "Credentials:User";
        var passwordKey = section ? "Password" : "Credentials:Password";
        var source = new ConfigurationUsernamePasswordCredentialSource(configuration, userKey, passwordKey);
        Assert.That(source.Resolve(Context()).Password, Is.EqualTo(" sentinel ;%2B+@ "));
        configuration[passwordKey] = "rotated";
        Assert.That((await source.ResolveAsync(Context())).Password, Is.EqualTo("rotated"));
        Assert.That(source.ToString(), Does.Not.Contain("sentinel"));
    }

    [Test]
    public void ConstructionIsLazyAndResolutionUsesOnlySelectedKeys()
    {
        var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
        var source = new ConfigurationUsernamePasswordCredentialSource(configuration.Object, "user", "password");
        configuration.VerifyNoOtherCalls();
        configuration.SetupGet(c => c["user"]).Returns("alice");
        configuration.SetupGet(c => c["password"]).Returns("sentinel");
        var result = source.Resolve(Context());
        Assert.That(result.Password, Is.EqualTo("sentinel"));
        configuration.VerifyGet(c => c["user"], Times.Once);
        configuration.VerifyGet(c => c["password"], Times.Once);
    }

    [TestCase(null, false)]
    [TestCase("", true)]
    [TestCase("sentinel", true)]
    public void PreservesAbsentAndEmptyPassword(string? password, bool success)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["password"] = password }).Build();
        var source = new ConfigurationUsernamePasswordCredentialSource(configuration, null, "password");
        if (success)
        {
            var resolved = source.Resolve(Context("alice"));
            Assert.That(resolved.Username, Is.Null);
            Assert.That(resolved.Password, Is.EqualTo(password));
        }
        else Assert.That(() => source.Resolve(Context("alice")), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("MissingCredentials"));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void RejectsInvalidKeys(string key)
        => Assert.That(() => new ConfigurationUsernamePasswordCredentialSource(Mock.Of<IConfiguration>(), null, key), Throws.ArgumentException);

    [Test]
    public async Task FailuresAreSafeAndCancellationDoesNotRead()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(c => c["password"]).Throws(new InvalidOperationException("sentinel", new Exception("sentinel")));
        var source = new ConfigurationUsernamePasswordCredentialSource(configuration.Object, null, "password");
        var error = Assert.Throws<ConnectionAuthenticationException>(() => source.Resolve(Context("alice")));
        Assert.That(error!.ToString(), Does.Not.Contain("sentinel"));
        Assert.That(error.InnerException, Is.Null);
        configuration.Invocations.Clear();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await source.ResolveAsync(Context("alice"), cancellation.Token));
        configuration.VerifyNoOtherCalls();
    }
}
