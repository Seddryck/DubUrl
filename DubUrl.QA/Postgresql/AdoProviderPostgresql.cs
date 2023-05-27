using NUnit.Framework;

namespace DubUrl.QA.Postgresql
{
    [Category("Postgresql")]
    [Category("AdoProvider")]
    public class AdoProviderPostgresql : BaseAdoProviderPostgresql
    {
        public override string ConnectionString
        {
            get => $"pgsql://postgres:Password12!@localhost/DubUrl";
        }
    }
}