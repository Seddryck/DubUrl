using System.Data;
using System.Data.Common;
using System.Reflection;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

internal static class PasswordQaPoolCancellation
{
    internal static async Task VerifyAsync(PasswordQaDriver driver)
    {
        var observed = new PasswordQaRegistry(driver.Registry);
        observed.NativeOptions["Pooling"] = true;
        var maximum = driver.Provider switch
        { "Npgsql" => "Maximum Pool Size", "MySqlConnector" => "MaximumPoolSize", _ => "Max Pool Size" };
        observed.NativeOptions[maximum] = 1;
        observed.NativeOptions["Application Name"] = "DubUrlQa_" + Guid.NewGuid().ToString("N");
        using var source = new PasswordQaSource(driver.Credentials);
        using var cancellation = new CancellationTokenSource();
        var target = driver.Target(source, observed, pooling: true);
        Task<DbConnection>? waiting = null;
        try
        {
            var held = await target.OpenAsync();
            await PasswordQaDriver.ProbeAsync(held, driver.ProbeSql);
            waiting = target.OpenAsync(cancellation.Token).AsTask();
            Assert.That(observed.Connections, Has.Count.EqualTo(2));
            Assert.That(observed.Connections[1].OpenStarted, Is.True);
            Assert.That(waiting.IsCompleted, Is.False, "The second native OpenAsync must wait while the size-one pool's authenticated connection is held.");
            cancellation.Cancel();
            await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await waiting.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(observed.Connections[1].WasDisposed, Is.True);
            Assert.That(held.State, Is.EqualTo(ConnectionState.Open));
            await PasswordQaDriver.ProbeAsync(held, driver.ProbeSql);
            Assert.That(source.Calls, Is.EqualTo(2));
            Assert.That(source.Disposed, Is.False);
        }
        finally
        {
            cancellation.Cancel();
            await CleanupAsync(observed.Connections);
            if (waiting is not null)
            {
                try { await using var connection = await waiting.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception error) when (error is OperationCanceledException or Authentication.ConnectionAuthenticationException or TimeoutException) { }
            }
        }
    }

    private static async Task CleanupAsync(IEnumerable<PasswordQaConnection> connections)
    {
        var failed = false;
        foreach (var connection in connections)
        {
            if (connection.WasDisposed) continue;
            try
            {
                await connection.CloseAsync();
                var native = connection.Native;
                var clear = native.GetType().GetMethod("ClearPool", BindingFlags.Public | BindingFlags.Static,
                    null, [native.GetType()], null) ?? throw new AssertionException("The driver has no targeted pool cleanup API.");
                clear.Invoke(null, [native]);
            }
            catch { failed = true; }
            finally { await connection.DisposeAsync(); }
        }
        Assert.That(failed, Is.False, "The fixture-owned pool must be cleaned up; native provider details are redacted.");
    }
}
