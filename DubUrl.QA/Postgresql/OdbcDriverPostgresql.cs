using NUnit.Framework;

namespace DubUrl.QA.Postgresql
{
    [Category("Postgresql")]
    public class OdbcDriverPostgresql : BaseOdbcDriverPostgresql
    {
        public override string ConnectionString
        {
            get => $"odbc+pgsql://postgres:Password12!@localhost/DubUrl?TrustServerCertificate=Yes";
        }
    }
}