using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;

namespace DubUrl.QA.QuestDB
{
    [Category("QuestDB")]
    public class OdbcDriverQuestDB : Postgresql.BaseOdbcDriverPostgresql
    {
        public override string ConnectionString
        {
            get => "odbc+questdb://admin:quest@localhost:8812/?TrustServerCertificate=Yes";
        }

        public override void QueryCustomerWithParams()
            => Assert.Ignore("ERROR [00000] ERROR: invalid constant: int4");
    }
}