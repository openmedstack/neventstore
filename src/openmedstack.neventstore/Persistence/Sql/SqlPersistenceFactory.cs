namespace OpenMedStack.NEventStore.Persistence.Sql
{
    using System;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using Serialization;

    public class SqlPersistenceFactory : IPersistenceFactory
    {
        private readonly ILogger _logger;
        private const int DefaultPageSize = 128;

        public SqlPersistenceFactory(
            IConnectionFactory factory,
            ISerialize serializer,
            ISqlDialect dialect,
            ILogger logger,
            IStreamIdHasher? streamIdHasher = null,
            int pageSize = DefaultPageSize)
        {
            _logger = logger;
            ConnectionFactory = factory;
            Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            Serializer = serializer;
            StreamIdHasher = streamIdHasher ?? new Sha1StreamIdHasher();
            PageSize = pageSize;
        }

        protected virtual IConnectionFactory? ConnectionFactory { get; }

        protected virtual ISqlDialect? Dialect { get; }

        protected virtual ISerialize Serializer { get; }

        protected virtual IStreamIdHasher StreamIdHasher { get; }

        protected int PageSize { get; set; }

        public virtual IPersistStreams Build() =>
            new SqlPersistenceEngine(ConnectionFactory, Dialect, Serializer, PageSize, StreamIdHasher, _logger);
    }
}
