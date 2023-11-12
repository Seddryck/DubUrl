using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Reading;

namespace DubUrl.Querying
{
    public class CommandProviderFactory
    {
        protected ICommandType DefaultCommandType { get; }
        protected IQueryLogger QueryLogger { get; }
        protected IDictionary<ICommandType, Func<string, IQueryLogger, ICommandProvider>> Providers { get; } = new Dictionary<ICommandType, Func<string, IQueryLogger, ICommandProvider>>();

        public CommandProviderFactory(IQueryLogger logger)
            : this(logger, DirectCommand.Instance) { }

        public CommandProviderFactory(IQueryLogger logger, ICommandType commandType)
        {
            (QueryLogger, DefaultCommandType) = (logger, commandType);
            Initialize();
        }

        protected virtual void Initialize()
        {
            Add<InlineSqlProvider>(DirectCommand.Instance);
        }

        public ICommandProvider Instantiate(string query)
            => Instantiate(query, DefaultCommandType);

        public virtual ICommandProvider Instantiate(string query, ICommandType commandType)
        {
            if (Providers.TryGetValue(commandType, out var provider))
                return provider.Invoke(query, QueryLogger);
            throw new ArgumentOutOfRangeException(nameof(commandType), $"No registered ICommandProvider for the CommandType '{commandType}'");
        }

        protected internal virtual Func<string, IQueryLogger, ICommandProvider> GetInstantiator<T>() where T : ICommandProvider
        {
            var paramQuery = Expression.Parameter(typeof(string), "query");
            var paramLogger = Expression.Parameter(typeof(IQueryLogger), "logger");
            var ctor = typeof(T).GetConstructor(new[] { typeof(string), typeof(IQueryLogger) })
                            ?? throw new NullReferenceException();
            var lambda = Expression.Lambda<Func<string, IQueryLogger, ICommandProvider>>(
                            Expression.New(ctor, new[] { paramQuery, paramLogger }), paramQuery, paramLogger
                        );
            var func = lambda.Compile();
            return func;
        }

        public void Add<T>(ICommandType commandType) where T : ICommandProvider
        {
            if (Providers.ContainsKey(commandType))
                throw new InvalidOperationException($"An ICommandProvider '{typeof(T).Name}' is already registered for the CommandType '{commandType}'");
            Providers.Add(commandType, GetInstantiator<T>());
        }

        public void Remove(ICommandType commandType)
            => Providers.Remove(commandType);

        public void Clear()
            => Providers.Clear();
    }
}
