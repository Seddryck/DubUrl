using DubUrl.QA.Postgresql;
using NUnit.Framework;
using System.Data;

namespace DubUrl.QA.GlareDb
{
    [Category("GlareDB")]
    [Category("AdoProvider")]
    public class AdoProviderGlareDb : BaseAdoProviderPostgresql
    {
        public override string ConnectionString
        {
            get => $"glaredb://nSmxJjB9UtT6:glaredb_pw_RV3brIVH3RkCFsqf8qlhirvOV84N@o_kBhBpr0/hidden_cherry";
        }

        [Test]
        [Category("ConnectionUrl")]
        public void ConnectTune()
        {
            Console.WriteLine(ConnectionString);
            var connectionUrl = new ConnectionUrl(ConnectionString);
            Console.WriteLine(connectionUrl.Parse());

            using var conn = connectionUrl.Open();
            Assert.That(conn.State, Is.EqualTo(ConnectionState.Open));
        }
    }
}