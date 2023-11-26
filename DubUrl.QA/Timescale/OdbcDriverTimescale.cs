using NUnit.Framework;

namespace DubUrl.QA.Timescale;

[Category("Timescale")]
public class OdbcDriverTimescale : Postgresql.BaseOdbcDriverPostgresql
{
    public override string ConnectionString
    {
        get => $"odbc+ts://postgres:Password12!@localhost/DubUrl";
    }
}