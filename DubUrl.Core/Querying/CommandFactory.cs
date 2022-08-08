using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    public class CommandFactory
    {

        private ICommandReader Reader { get; }

        private string[]? resourceNames = null;
        public string[] ResourceNames
        { get => resourceNames ??= Reader.GetAllResourceNames(); }

        public CommandFactory(ICommandReader reader)
            => Reader = reader;

        public virtual IDbCommand Execute(IDbConnection conn, string queryId, string[] dialects)
        {
            if (!IsExistingQuery(queryId))
                throw new ArgumentException();

            var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = Reader.ReadCommandText(RetrieveBestMatch(queryId, dialects));
            foreach (var paramInfo in Reader.ReadParameters(RetrieveBestMatch(queryId, dialects)))
            {
                var dbParam = cmd.CreateParameter();
                dbParam.ParameterName = paramInfo.Name;
                dbParam.DbType = (DbType)Enum.Parse(typeof(DbType), paramInfo.DbTypeText);
                dbParam.Direction = (ParameterDirection)Enum.Parse(typeof(ParameterDirection), paramInfo.DirectionText);
                cmd.Parameters.Add(dbParam);
            }
            return cmd;
        }

        public bool IsExistingQuery(string id)
            => ResourceNames.Any(x => x.StartsWith(id) && x.EndsWith(".sql"));

        protected internal virtual string RetrieveBestMatch(string id, string[] dialects)
        {
            var resourceNames = new List<string>();
            foreach (var dialect in dialects)
            {
                resourceNames.AddRange(
                    ResourceNames
                    .Where(x =>
                        x.Equals($"{id}.{dialect}.sql", StringComparison.InvariantCultureIgnoreCase)
                        || x.Equals($"{id}.sql", StringComparison.InvariantCultureIgnoreCase)
                    )
                );
            }
            return resourceNames
                .OrderByDescending(x => x.Length)
                .First();
        }
    }
}
