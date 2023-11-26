using NUnit.Framework;

namespace DubUrl.QA.Timescale;

[Category("Timescale")]
[Category("AdoProvider")]
public class AdoProviderTimescale : Postgresql.BaseAdoProviderPostgresql
{
    public override string ConnectionString
    {
        get => $"ts://postgres:Password12!@localhost/DubUrl";
    }
}