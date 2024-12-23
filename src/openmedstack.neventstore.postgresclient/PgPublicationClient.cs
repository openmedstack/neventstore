﻿namespace OpenMedStack.NEventStore.PostgresClient;

using Npgsql;
using Abstractions;
using System.IO;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System.Threading;
using System.Threading.Tasks;

public abstract class PgPublicationClient : IAsyncDisposable
{
    private readonly string _replicationSlotName;
    private readonly string _publicationName;
    private readonly string _connectionString;
    private readonly ISerialize _serializer;
    private readonly ILogger _logger;
    private LogicalReplicationConnection? _logicalReplicationConnection;
    private PgOutputReplicationSlot? _slot = null;

    protected PgPublicationClient(
        string replicationSlotName,
        string publicationName,
        string connectionString,
        ISerialize serializer,
        ILogger logger)
    {
        _replicationSlotName = replicationSlotName;
        _publicationName = publicationName;
        _connectionString = connectionString;
        _serializer = serializer;
        _logger = logger;
    }

    public async Task CreateSubscriptionSlot(CancellationToken cancellationToken)
    {
        try
        {
            _logicalReplicationConnection = new LogicalReplicationConnection(_connectionString);
            await _logicalReplicationConnection.Open(cancellationToken).ConfigureAwait(false);
            try
            {
                await _logicalReplicationConnection.DropReplicationSlot(_replicationSlotName, true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Empty
            }

            _slot = await _logicalReplicationConnection.CreatePgOutputReplicationSlot(_replicationSlotName,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Replication slot created");
        }
        catch (PostgresException)
        {
            _logger.LogInformation("Replication slot already created");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{error}", e.Message);
        }
    }

    public Task Subscribe(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (_logicalReplicationConnection == null)
                {
                    await CreateSubscriptionSlot(stoppingToken).ConfigureAwait(false);
                }

                var replication = _logicalReplicationConnection!.StartReplication(
                    _slot!,
                    new PgOutputReplicationOptions(_publicationName, 1),
                    stoppingToken);
                await foreach (var item in replication.ConfigureAwait(false))
                {
                    if (item is InsertMessage insert)
                    {
                        await Handle(insert, stoppingToken).ConfigureAwait(false);
                    }

                    _logicalReplicationConnection!.SetReplicationStatus(item.WalEnd);
                    await _logicalReplicationConnection.SendStatusUpdate(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (PostgresException p)
            {
                _logger.LogError(p, "{Error}", p.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Error}", e.Message);
            }
        }, stoppingToken);
    }

    private async Task Handle(InsertMessage insertMessage, CancellationToken cancellationToken)
    {
        var columnNumber = 0;
        await foreach (var c in insertMessage.NewRow.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            switch (columnNumber)
            {
                case 9:
                {
                    await HandleHeaders(c, cancellationToken).ConfigureAwait(false);
                }
                    break;
                case 10:
                    await HandlePayload(c, cancellationToken).ConfigureAwait(false);

                    break;
            }

            columnNumber++;
        }
    }

    private async Task HandlePayload(ReplicationValue c, CancellationToken cancellationToken)
    {
        try
        {
            var t = c.GetStream();
            await using var _ = t.ConfigureAwait(false);
            var bytes = DecodeHex(t);
            var ms = new MemoryStream(bytes);
            await using var __ = ms.ConfigureAwait(false);
            var payload = _serializer.Deserialize<List<EventMessage>>(ms);
            if (payload != null)
            {
                foreach (var message in payload)
                {
                    await HandleMessage(message.Body.GetType(), message.Body, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Error}", e.Message);
        }
    }

    private async Task HandleHeaders(ReplicationValue c, CancellationToken cancellationToken)
    {
        var stream = c.GetStream();
        await using var _ = stream.ConfigureAwait(false);
        var headers = _serializer.Deserialize<Dictionary<string, object>>(DecodeHex(stream));
        if (headers != null)
        {
            foreach (var (_, value) in headers
                .Where(x => x.Key.StartsWith("UndispatchedMessage."))
                .OrderBy(x => x.Key))
            {
                await HandleMessage(value.GetType(), value, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static byte[] DecodeHex(Stream hexString)
    {
        hexString.ReadByte();
        hexString.ReadByte();
        var l = (int)(hexString.Length >> 1) - 1;
        byte[] buffer = new byte[l];

        Span<byte> b = stackalloc byte[2];
        for (var i = 0; i < l; i++)
        {
            _ = hexString.Read(b);
            buffer[i] = GetHexVal(b);
        }

        return buffer;
    }

    private static byte GetHexVal(Span<byte> span)
    {
        static byte GetByteValue(byte hex)
        {
            return (byte)(hex - (hex < 58 ? 48 : 87));
        }

        var fb = GetByteValue(span[0]) << 4;
        var sb = GetByteValue(span[1]);
        //byte val = (byte)hex;
        //For uppercase A-F letters:
        //return val - (val < 58 ? 48 : 55);
        //For lowercase a-f letters:
        return (byte)(fb + sb);
        //Or the two combined, but a bit slower:
        //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
    }

    protected abstract Task HandleMessage(
        Type type,
        object value,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_logicalReplicationConnection != null)
        {
            await _logicalReplicationConnection.DropReplicationSlot(_replicationSlotName, true).ConfigureAwait(false);
            await _logicalReplicationConnection.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
