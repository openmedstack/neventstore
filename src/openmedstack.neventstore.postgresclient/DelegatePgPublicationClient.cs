using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.PostgresClient;

using System.Threading;
using System;
using System.Threading.Tasks;

public class DelegatePgPublicationClient : PgPublicationClient
{
    private readonly Func<Type, object, CancellationToken, Task> _handler;

    /// <inheritdoc />
    public DelegatePgPublicationClient(
        string replicationSlotName,
        string publicationName,
        string connectionString,
        ISerialize serializer,
        Func<Type, object, CancellationToken, Task> handler)
        : base(replicationSlotName, publicationName, connectionString, serializer, NullLogger.Instance)
    {
        _handler = handler;
    }

    /// <inheritdoc />
    protected override async Task HandleMessage(Type type, object value, CancellationToken cancellationToken)
    {
        await _handler(type, value, cancellationToken).ConfigureAwait(false);
    }
}
