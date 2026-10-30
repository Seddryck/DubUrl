using DubUrl.Authentication;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CredOsNamedCredentialStoreTests
{
    [Test]
    public async Task ConvertsCredentialsAndForwardsNamesReplacementAndCancellation()
    {
        var backend = new FakeStore();
        INamedCredentialStore store = new CredOsNamedCredentialStore(backend);
        using var source = new CancellationTokenSource();
        await store.AddAsync("Orders-Dev", new("alice", " \0é\n "), true, source.Token);
        Assert.That(backend.Name, Is.EqualTo("orders-dev"));
        Assert.That(backend.Token, Is.EqualTo(source.Token));
        Assert.That(backend.Replace, Is.True);
        Assert.That(backend.Value!.Username, Is.EqualTo("alice"));
        Assert.That(backend.Value.Password == " \0é\n ", Is.True);
        var read = await store.ReadAsync("Orders-Dev", source.Token);
        Assert.That(read.Username, Is.EqualTo("alice"));
        Assert.That(read.Password == backend.Value.Password, Is.True);
        Assert.That(await store.ExistsAsync("Orders-Dev", source.Token), Is.True);
        await store.RemoveAsync("Orders-Dev", source.Token);
        Assert.That(await store.ExistsAsync("Orders-Dev", source.Token), Is.False);
        Assert.That(backend.Token, Is.EqualTo(source.Token));
    }

    [TestCase("duplicate")]
    [TestCase("missing")]
    [TestCase("unavailable")]
    [TestCase("unsupported")]
    [TestCase("unexpected")]
    public async Task TranslatesFailuresWithoutExposingBackendDetails(string failure)
    {
        var backend = new FakeStore { Failure = failure switch
        {
            "duplicate" => new CredOS.CredentialAlreadyExistsException(),
            "missing" => new CredOS.CredentialNotFoundException(),
            "unavailable" => new CredOS.CredentialStoreUnavailableException(),
            "unsupported" => new CredOS.CredentialStoreUnsupportedPlatformException(),
            _ => new IOException("secret backend details")
        }};
        var store = new CredOsNamedCredentialStore(backend);
        async Task Check(Func<Task> action)
        {
            var error = await Assert.ThrowsAsync<CredentialStoreException>(async () => await action());
            Assert.That(error!.AlreadyExists, Is.EqualTo(failure == "duplicate"));
            Assert.That(error.InnerException, Is.Null);
            Assert.That(error.ToString(), Does.Not.Contain("secret backend details"));
        }
        await Check(async () => await store.ExistsAsync("entry"));
        await Check(async () => await store.ReadAsync("entry"));
        await Check(async () => await store.AddAsync("entry", new("alice", ""), false));
        await Check(async () => await store.RemoveAsync("entry"));
    }

    [Test]
    public async Task PreservesCancellationAcrossAllOperations()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var store = new CredOsNamedCredentialStore(new FakeStore { Failure = new OperationCanceledException(source.Token) });
        await Assert.ThatAsync(async () => await store.ExistsAsync("entry", source.Token), Throws.InstanceOf<OperationCanceledException>());
        await Assert.ThatAsync(async () => await store.ReadAsync("entry", source.Token), Throws.InstanceOf<OperationCanceledException>());
        await Assert.ThatAsync(async () => await store.AddAsync("entry", new("alice", ""), false, source.Token), Throws.InstanceOf<OperationCanceledException>());
        await Assert.ThatAsync(async () => await store.RemoveAsync("entry", source.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase(512, 1280, true)]
    [TestCase(513, 0, false)]
    [TestCase(1, 1281, false)]
    public async Task ValidatesPortableLimitsBeforeBackendAccess(int usernameLength, int passwordLength, bool valid)
    {
        var backend = new FakeStore();
        var store = new CredOsNamedCredentialStore(backend);
        var credentials = new UsernamePasswordCredentials(new string('u', usernameLength), new string('p', passwordLength));
        if (valid) await store.AddAsync("entry", credentials, false);
        else await Assert.ThrowsAsync<ArgumentException>(async () => await store.AddAsync("entry", credentials, false));
        Assert.That(backend.Name is not null, Is.EqualTo(valid));
    }

    [Test]
    public async Task RejectsMalformedUnicodeBeforeBackendAccess()
    {
        var backend = new FakeStore();
        var store = new CredOsNamedCredentialStore(backend);
        foreach (var credentials in new[] { new UsernamePasswordCredentials("\ud800", ""), new UsernamePasswordCredentials("alice", "\udc00") })
            await Assert.ThatAsync(async () => await store.AddAsync("entry", credentials, false), Throws.InstanceOf<ArgumentException>());
        Assert.That(backend.Name, Is.Null);
    }

    private sealed class FakeStore : CredOS.ICredentialStore
    {
        public Exception? Failure { get; init; }
        public string? Name { get; private set; }
        public CancellationToken Token { get; private set; }
        public bool Replace { get; private set; }
        public CredOS.UsernamePasswordCredentials? Value { get; private set; }
        private void Record(string name, CancellationToken token)
        {
            Name = name;
            Token = token;
            if (Failure is not null) throw Failure;
        }
        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        { Record(name, cancellationToken); return ValueTask.FromResult(Value is not null); }
        public ValueTask<CredOS.UsernamePasswordCredentials> ReadAsync(string name, CancellationToken cancellationToken = default)
        { Record(name, cancellationToken); return ValueTask.FromResult(Value!); }
        public ValueTask AddAsync(string name, CredOS.UsernamePasswordCredentials credentials, bool replace = false, CancellationToken cancellationToken = default)
        { Record(name, cancellationToken); Value = credentials; Replace = replace; return ValueTask.CompletedTask; }
        public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default)
        { Record(name, cancellationToken); Value = null; return ValueTask.CompletedTask; }
        public bool Exists(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public CredOS.UsernamePasswordCredentials Read(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(string name, CredOS.UsernamePasswordCredentials credentials, bool replace = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
