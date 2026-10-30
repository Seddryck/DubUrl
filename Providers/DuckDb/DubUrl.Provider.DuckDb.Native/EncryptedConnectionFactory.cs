using System.Data.Common;
using DubUrl.Authentication;

namespace DubUrl.Provider.DuckDb.Native;

public sealed class EncryptedConnectionFactory : IFileSecretConnectionFactory
{
    public DbConnection Create(string connectionString, FileSecretCredentials? credentials)
        => new EncryptedDuckDbConnection(connectionString, credentials);
}
