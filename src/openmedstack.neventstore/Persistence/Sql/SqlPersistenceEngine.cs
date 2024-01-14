using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;

namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NEventStore;
using Persistence;
using Serialization;

public class SqlPersistenceEngine : IManagePersistence, ICommitEvents, IAccessSnapshots
{
    private readonly ILogger<SqlPersistenceEngine> _logger;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;
    private readonly int _pageSize;
    private readonly ISerialize _serializer;
    private int _initialized;
    private readonly IStreamIdHasher _streamIdHasher;

    public SqlPersistenceEngine(
        IConnectionFactory? connectionFactory,
        ISqlDialect? dialect,
        ISerialize serializer,
        int pageSize,
        IStreamIdHasher streamIdHasher,
        ILogger<SqlPersistenceEngine> logger)
    {
        if (pageSize < 0)
        {
            throw new ArgumentException("Invalid page size", nameof(pageSize));
        }

        if (streamIdHasher == null)
        {
            throw new ArgumentNullException(nameof(streamIdHasher));
        }

        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _pageSize = pageSize;
        _logger = logger;
        _streamIdHasher = new StreamIdHasherValidator(streamIdHasher);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual async Task Initialize()
    {
        if (Interlocked.Increment(ref _initialized) > 1)
        {
            return;
        }

        _logger.LogDebug(PersistenceMessages.InitializingStorage);

        var statements = _dialect.InitializeStorage.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var s in statements)
        {
            await ExecuteCommand(statement => statement.ExecuteWithoutExceptions(s.Trim())).ConfigureAwait(false);
        }
    }

    public virtual IAsyncEnumerable<ICommit> Get(
        string bucketId,
        string streamId,
        int minRevision,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(PersistenceMessages.GettingAllCommitsBetween, streamId, minRevision, maxRevision);
        streamId = _streamIdHasher.GetHash(streamId);

        async IAsyncEnumerable<ICommit> Query(IDbStatement query, [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetCommitsFromStartingRevision;
            query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
            query.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
            query.AddParameter(_dialect.StreamRevision, minRevision);
            query.AddParameter(_dialect.MaxStreamRevision, maxRevision);
            query.AddParameter(_dialect.CommitSequence, 0);
            await foreach (var item in query.ExecuteWithQuery(statement, token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return item.GetCommit(_serializer, _dialect);
            }
        }

        return ExecuteQuery(Query, cancellationToken);
    }

    public virtual IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        DateTimeOffset start,
        CancellationToken cancellationToken = default)
    {
        start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
        start = start < DateTimeOffset.UnixEpoch ? DateTimeOffset.UnixEpoch : start;

        _logger.LogDebug(PersistenceMessages.GettingAllCommitsFrom, start, bucketId);

        async IAsyncEnumerable<ICommit> Query(IDbStatement query, [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetCommitsFromInstant;
            query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
            query.AddParameter(_dialect.CommitStamp, start);
            var enumerable = query.ExecuteWithQuery(statement, token);
            await foreach (var item in enumerable.WithCancellation(token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return item.GetCommit(_serializer, _dialect);
            }
        }

        return ExecuteQuery(Query, cancellationToken);
    }

    public virtual IAsyncEnumerable<ICommit> GetFromTo(
        string bucketId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
        start = start < DateTimeOffset.UnixEpoch ? DateTimeOffset.UnixEpoch : start;
        end = end < DateTimeOffset.UnixEpoch ? DateTimeOffset.UnixEpoch : end;

        _logger.LogDebug(PersistenceMessages.GettingAllCommitsFromTo, start, end);

        async IAsyncEnumerable<ICommit> Query(IDbStatement query, [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetCommitsFromToInstant;
            query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
            query.AddParameter(_dialect.CommitStampStart, start);
            query.AddParameter(_dialect.CommitStampEnd, end);
            var enumerable = query.ExecuteWithQuery(statement, token);
            await foreach (var record in enumerable.WithCancellation(token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return record.GetCommit(_serializer, _dialect);
            }
        }

        var result = ExecuteQuery(Query, cancellationToken);

        return result;
    }

    public async Task<ICommit?> Commit(IEventStream eventStream, Guid? commitId, CancellationToken cancellationToken)
    {
        if (eventStream.UncommittedEvents.Count == 0)
        {
            return null;
        }

        var id = commitId ?? Guid.NewGuid();
        var attempt = CommitAttempt.FromStream(eventStream, id);
        ICommit commit;
        try
        {
            commit = await PersistCommit(attempt).ConfigureAwait(false);
            _logger.LogDebug(PersistenceMessages.CommitPersisted, attempt.CommitId);
        }
        catch (UniqueKeyViolationException e)
        {
            if (await DetectDuplicate(attempt).ConfigureAwait(false))
            {
                _logger.LogInformation(PersistenceMessages.DuplicateCommit);
                throw new DuplicateCommitException(e.Message, e);
            }

            _logger.LogInformation(PersistenceMessages.ConcurrentWriteDetected);

            var currentRevision = eventStream.StreamRevision - eventStream.UncommittedEvents.Count;
            await eventStream.Update(this, cancellationToken).ConfigureAwait(false);
            if (eventStream.StreamRevision <= currentRevision)
            {
                throw;
            }

            return await PersistCommit(CommitAttempt.FromStream(eventStream, id));
        }

        return commit;
    }

    public virtual IAsyncEnumerable<IStreamHead> GetStreamsToSnapshot(
        string bucketId,
        int maxThreshold,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(PersistenceMessages.GettingStreamsToSnapshot);

        async IAsyncEnumerable<IStreamHead> Query(
            IDbStatement query,
            [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetStreamsRequiringSnapshots;
            query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
            query.AddParameter(_dialect.Threshold, maxThreshold);
            await foreach (var record in query.ExecuteWithQuery(
                    statement,
                    //(q, s) => q.SetParameter(
                    //    _dialect.StreamId,
                    //    s == null ? null : _dialect.CoalesceParameterValue(s.StreamId()),
                    //    DbType.AnsiString),
                    token)
                .ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return record.GetStreamToSnapshot();
            }
        }

        return ExecuteQuery(Query, cancellationToken);
    }

    public virtual Task<ISnapshot?> GetSnapshot(
        string bucketId,
        string streamId,
        int maxRevision,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(PersistenceMessages.GettingRevision, streamId, maxRevision);
        var streamIdHash = _streamIdHasher.GetHash(streamId);

        async IAsyncEnumerable<ISnapshot> Query(
            IDbStatement query,
            [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetSnapshot;
            query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
            query.AddParameter(_dialect.StreamId, streamIdHash!, DbType.AnsiString);
            query.AddParameter(_dialect.StreamRevision, maxRevision);
            var dataRecords = query.ExecuteWithQuery(statement, token).ConfigureAwait(false);
            await foreach (var record in dataRecords.ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return record.GetSnapshot(_serializer, streamId);
            }
        }

        return ExecuteQuery(Query, cancellationToken).FirstOrDefault(cancellationToken);
    }

    public virtual async Task<bool> AddSnapshot(ISnapshot snapshot)
    {
        _logger.LogDebug(PersistenceMessages.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
        var streamId = _streamIdHasher.GetHash(snapshot.StreamId);
        return await ExecuteCommand(
                    async (connection, cmd) =>
                    {
                        cmd.AddParameter(_dialect.BucketId, snapshot.BucketId, DbType.AnsiString);
                        cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
                        cmd.AddParameter(_dialect.StreamRevision, snapshot.StreamRevision);
                        var payload = _serializer.Serialize(snapshot.Payload);
                        _dialect.AddPayloadParamater(_connectionFactory, connection, cmd, payload);
                        return await cmd.ExecuteWithoutExceptions(_dialect.AppendSnapshotToCommit)
                            .ConfigureAwait(false);
                    })
                .ConfigureAwait(false)
          > 0;
    }

    public virtual async Task<bool> Purge()
    {
        _logger.LogWarning(PersistenceMessages.PurgingStorage);
        return await ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.PurgeStorage)).ConfigureAwait(false) > 0;
    }

    public async Task<bool> Purge(string bucketId)
    {
        _logger.LogWarning(PersistenceMessages.PurgingBucket, bucketId);
        return await ExecuteCommand(
            cmd =>
            {
                cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                return cmd.ExecuteNonQuery(_dialect.PurgeBucket);
            }).ConfigureAwait(false) > 0;
    }

    public async Task<bool> Drop()
    {
        _logger.LogWarning(PersistenceMessages.DroppingTables);
        return await ExecuteCommand(cmd => cmd.ExecuteWithoutExceptions(_dialect.Drop)).ConfigureAwait(false) > 0;
    }

    public async Task<bool> DeleteStream(string bucketId, string streamId)
    {
        _logger.LogWarning(PersistenceMessages.DeletingStream, streamId, bucketId);
        streamId = _streamIdHasher.GetHash(streamId);
        return await ExecuteCommand(
            cmd =>
            {
                cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
                return cmd.ExecuteNonQuery(_dialect.DeleteStream);
            }).ConfigureAwait(false) > 0;
    }

    public IAsyncEnumerable<ICommit> GetFrom(
        string bucketId,
        long checkpointToken,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(PersistenceMessages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);

        async IAsyncEnumerable<ICommit> Query(IDbStatement query, [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetCommitsFromBucketAndCheckpoint;
            query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
            query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
            var enumerable = query.ExecuteWithQuery(statement, token);
            await foreach (var item in enumerable.WithCancellation(token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                yield return item.GetCommit(_serializer, _dialect);
            }
        }

        return ExecuteQuery(Query, cancellationToken);
    }

    public IAsyncEnumerable<ICommit> GetFrom(long checkpointToken, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(PersistenceMessages.GettingAllCommitsFromCheckpoint, checkpointToken);

        async IAsyncEnumerable<ICommit> Query(IDbStatement query, [EnumeratorCancellation] CancellationToken token)
        {
            var statement = _dialect.GetCommitsFromCheckpoint;
            query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
            var enumerable = query.ExecuteWithQuery(statement, token);
            await foreach (var record in enumerable.WithCancellation(token).ConfigureAwait(false))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                var commit = record.GetCommit(_serializer, _dialect);
                yield return commit;
            }
        }

        var result = ExecuteQuery(Query, cancellationToken);

        return result;
    }

    public bool IsDisposed { get; private set; }

    private static readonly string[] Separator = ["__"];

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed)
        {
            return;
        }

        _logger.LogDebug(PersistenceMessages.ShuttingDownPersistence);
        IsDisposed = true;
    }

    protected virtual void OnPersistCommit(IDbStatement cmd, CommitAttempt attempt)
    {
    }

    private async Task<ICommit> PersistCommit(CommitAttempt attempt)
    {
        _logger.LogDebug(
            PersistenceMessages.AttemptingToCommit,
            attempt.Events.Count,
            attempt.StreamId,
            attempt.CommitSequence,
            attempt.BucketId);
        var streamId = _streamIdHasher.GetHash(attempt.StreamId);
        return await ExecuteCommand(
                async (connection, cmd) =>
                {
                    cmd.AddParameter(_dialect.BucketId, attempt.BucketId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.StreamIdOriginal, attempt.StreamId);
                    cmd.AddParameter(_dialect.StreamRevision, attempt.StreamRevision);
                    cmd.AddParameter(_dialect.Items, attempt.Events.Count);
                    cmd.AddParameter(_dialect.CommitId, attempt.CommitId);
                    cmd.AddParameter(_dialect.CommitSequence, attempt.CommitSequence);
                    cmd.AddParameter(_dialect.CommitStamp, attempt.CommitStamp, DbType.DateTimeOffset);
                    var headers = _serializer.Serialize(attempt.Headers);
                    cmd.AddParameter(_dialect.Headers, headers);
                    var payload = _serializer.Serialize(attempt.Events.ToList());
                    _dialect.AddPayloadParamater(_connectionFactory, connection, cmd, payload);
                    OnPersistCommit(cmd, attempt);
                    var scalar = await cmd.ExecuteScalar(_dialect.PersistCommit).ConfigureAwait(false);
                    var checkpointNumber = scalar!.ToLong();
                    return new Commit(
                        attempt.BucketId,
                        attempt.StreamId,
                        attempt.StreamRevision,
                        attempt.CommitId,
                        attempt.CommitSequence,
                        attempt.CommitStamp,
                        checkpointNumber,
                        attempt.Headers,
                        attempt.Events);
                })
            .ConfigureAwait(false);
    }

    private async Task<bool> DetectDuplicate(CommitAttempt attempt)
    {
        var streamId = _streamIdHasher.GetHash(attempt.StreamId);
        return await ExecuteCommand(
                async cmd =>
                {
                    cmd.AddParameter(_dialect.BucketId, attempt.BucketId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.CommitId, attempt.CommitId);
                    cmd.AddParameter(_dialect.CommitSequence, attempt.CommitSequence);
                    var value = await cmd.ExecuteScalar(_dialect.DuplicateCommit).ConfigureAwait(false);
                    return (value is long l ? l : (int)value!) > 0;
                })
            .ConfigureAwait(false);
    }

    protected virtual IAsyncEnumerable<T> ExecuteQuery<T>(
        Func<IDbStatement, CancellationToken, IAsyncEnumerable<T>> query,
        CancellationToken cancellationToken)
    {
        ThrowWhenDisposed();

        IDbConnection connection = null!;
        IDbStatement statement = null!;

        try
        {
            connection = _connectionFactory.Open();
            statement = _dialect.BuildStatement(connection);
            statement.PageSize = _pageSize;

            _logger.LogTrace(PersistenceMessages.ExecutingQuery);
            return query(statement, cancellationToken);
        }
        catch (Exception e)
        {
            statement.Dispose();
            connection.Dispose();
            _logger.LogDebug(PersistenceMessages.StorageThrewException, e.GetType());
            if (e is StorageUnavailableException)
            {
                throw;
            }

            throw new StorageException(e.Message, e);
        }
    }

    private void ThrowWhenDisposed()
    {
        if (!IsDisposed)
        {
            return;
        }

        _logger.LogWarning(PersistenceMessages.AlreadyDisposed);
        throw new ObjectDisposedException(PersistenceMessages.AlreadyDisposed);
    }

    private async Task<T> ExecuteCommand<T>(Func<IDbStatement, Task<T>> command)
    {
        return await ExecuteCommand((_, statement) => command(statement));
    }

    protected virtual async Task<T> ExecuteCommand<T>(Func<IDbConnection, IDbStatement, Task<T>> command)
    {
        ThrowWhenDisposed();

        using var connection = _connectionFactory.Open();
        using var statement = _dialect.BuildStatement(connection);
        try
        {
            _logger.LogTrace(PersistenceMessages.ExecutingCommand);
            var rowsAffected = await command(connection, statement).ConfigureAwait(false);
            _logger.LogTrace(PersistenceMessages.CommandExecuted, rowsAffected);

            return rowsAffected;
        }
        catch (Exception e)
        {
            _logger.LogDebug(PersistenceMessages.StorageThrewException, e.GetType());
            if (!RecoverableException(e))
            {
                throw new StorageException(e.Message, e);
            }

            _logger.LogInformation(PersistenceMessages.RecoverableExceptionCompletesScope);

            throw;
        }
    }

    private static bool RecoverableException(Exception e) =>
        e is UniqueKeyViolationException or StorageUnavailableException;

    private class StreamIdHasherValidator : IStreamIdHasher
    {
        private readonly IStreamIdHasher _streamIdHasher;
        private const int MaxStreamIdHashLength = 40;

        public StreamIdHasherValidator(IStreamIdHasher streamIdHasher)
        {
            _streamIdHasher = streamIdHasher ?? throw new ArgumentNullException(nameof(streamIdHasher));
        }

        public string GetHash(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                throw new ArgumentException(PersistenceMessages.StreamIdIsNullEmptyOrWhiteSpace);
            }

            var streamIdHash = _streamIdHasher.GetHash(streamId);
            if (string.IsNullOrWhiteSpace(streamIdHash))
            {
                throw new InvalidOperationException(PersistenceMessages.StreamIdHashIsNullEmptyOrWhiteSpace);
            }

            if (streamIdHash.Length > MaxStreamIdHashLength)
            {
                throw new InvalidOperationException(
                    string.Format(
                        PersistenceMessages.StreamIdHashTooLong,
                        streamId,
                        streamIdHash,
                        streamIdHash.Length,
                        MaxStreamIdHashLength));
            }

            return streamIdHash;
        }
    }
}
