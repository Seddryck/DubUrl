using NUnit.Framework;

namespace DubUrl.QA.QuestDB
{
    [Category("QuestDB")]
    [Category("AdoProvider")]
    public class AdoProviderQuestDB : Postgresql.BaseAdoProviderPostgresql
    {
        public override string ConnectionString
        {
            get => $"questdb://admin:quest@localhost:8812/";
        }

        [Test]
        public override void QueryCustomerWithWhereClause()
            => Assert.Ignore("QuestDB doesn't support string and dateTime comparison (except match and not match)");

        [Test]
        public override void QueryTimeWithDatabaseUrl()
            => Assert.Ignore("QuestDB doesn't support 'Time' type");

        [Test]
        public override void QueryIntervalWithDatabaseUrl()
            => Assert.Ignore("QuestDB doesn't support 'Interval' type");


        [Test]
        public override void QueryNullWithDatabaseUrl()
            => Assert.Ignore("QuestDB has a really personnal implementation of NULL value.");
    }
}