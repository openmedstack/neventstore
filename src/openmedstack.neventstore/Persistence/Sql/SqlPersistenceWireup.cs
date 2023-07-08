// ReSharper disable once CheckNamespace

using OpenMedStack.NEventStore.Abstractions;

namespace NEventStore
{
    using System;
    using Microsoft.Extensions.Logging;
    using OpenMedStack.NEventStore;

    using OpenMedStack.NEventStore.Persistence.Sql;

    public class SqlPersistenceWireup : PersistenceWireup
    {
        private readonly ILogger _logger;
        private const int DefaultPageSize = 512;
        private int _pageSize = DefaultPageSize;

        public SqlPersistenceWireup(Wireup wireup, IConnectionFactory connectionFactory)
            : base(wireup)
        {
            _logger = wireup.Logger;
            _logger.LogDebug(PersistenceMessages.ConnectionFactorySpecified, connectionFactory);

            _logger.LogTrace(PersistenceMessages.AutoDetectDialect);
            Container.Register<ISqlDialect>(_ => null); // auto-detect
            Container.Register<IStreamIdHasher>(_ => new Sha1StreamIdHasher());

            Container.Register(
                c => new SqlPersistenceFactory(
                    connectionFactory,
                    c.Resolve<ISerialize>() ?? throw new Exception($"Could not resolve {nameof(ISerialize)}"),
                    c.Resolve<ISqlDialect>() ?? throw new Exception($"Could not resolve {nameof(ISqlDialect)}"),
                    _logger,
                    c.Resolve<IStreamIdHasher>() ?? throw new Exception($"Could not resolve {nameof(IStreamIdHasher)}"),
                    _pageSize).Build());
        }

        public virtual SqlPersistenceWireup WithDialect(ISqlDialect instance)
        {
            _logger.LogDebug(PersistenceMessages.DialectSpecified, instance.GetType());
            Container.Register(instance);
            return this;
        }

        public virtual SqlPersistenceWireup PageEvery(int records)
        {
            _logger.LogDebug(PersistenceMessages.PagingSpecified, records);
            _pageSize = records;
            return this;
        }

        public virtual SqlPersistenceWireup WithStreamIdHasher(IStreamIdHasher instance)
        {
            _logger.LogDebug(PersistenceMessages.StreamIdHasherSpecified, instance.GetType());
            Container.Register(instance);
            return this;
        }

        public virtual SqlPersistenceWireup WithStreamIdHasher(Func<string, string> getStreamIdHash) =>
            WithStreamIdHasher(new DelegateStreamIdHasher(getStreamIdHash));
    }
}
