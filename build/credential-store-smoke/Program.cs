using DubUrl.Cli.Testing;

// Invoke the same assertions directly: Windows SDK testhost dependency manifests are
// incompatible with some Linux SDK runners. Compilation/runtime stay entirely in /tmp.
var tests = new NativeNamedCredentialStoreTests();
await tests.NativeLifecycleAndCliCredentialResolution();
Console.WriteLine("PASS NativeLifecycleAndCliCredentialResolution");
await tests.ConcurrentCreateHasOneWinner();
Console.WriteLine("PASS ConcurrentCreateHasOneWinner");
await tests.PrecancelledNativeOperationDoesNotWrite();
Console.WriteLine("PASS PrecancelledNativeOperationDoesNotWrite");
await new LinuxNamedCredentialStoreTests().LockedCollectionPreservesPresenceAndRejectsSecretOperations();
Console.WriteLine("PASS LockedCollectionPreservesPresenceAndRejectsSecretOperations");
