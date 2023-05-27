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
    }
}