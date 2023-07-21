using System.Linq;
using Npgsql;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.PostgresClient;

using System.IO;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System.Threading;
using System.Threading.Tasks;

public abstract class PgPublicationClient
{
    private readonly string _replicationSlotName;
    private readonly string _publicationName;
    private readonly string _connectionString;
    private readonly ISerialize _serializer;
    private readonly ILogger _logger;

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
            await using var logicalReplicationConnection = new LogicalReplicationConnection(_connectionString);
            await logicalReplicationConnection.Open(cancellationToken);

            await logicalReplicationConnection.CreatePgOutputReplicationSlot(_replicationSlotName,
                cancellationToken: cancellationToken);

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
                await using var logicalReplicationConnection = new LogicalReplicationConnection(_connectionString);
                await logicalReplicationConnection.Open(stoppingToken);
                var replication = logicalReplicationConnection.StartReplication(
                    new PgOutputReplicationSlot(_replicationSlotName),
                    new PgOutputReplicationOptions(_publicationName, 1),
                    stoppingToken);
                await foreach (var item in replication.WithCancellation(stoppingToken))
                {
                    if (item is InsertMessage insert)
                    {
                        await Handle(insert, stoppingToken);
                    }

                    logicalReplicationConnection.SetReplicationStatus(item.WalEnd);
                    await logicalReplicationConnection.SendStatusUpdate(stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{error}", e.Message);
            }
        }, stoppingToken);
    }

    private async Task Handle(InsertMessage insertMessage, CancellationToken cancellationToken)
    {
        var columnNumber = 0;
        await foreach (var c in insertMessage.NewRow.WithCancellation(cancellationToken))
        {
            switch (columnNumber)
            {
                case 9:
                {
                    await HandleHeaders(cancellationToken, c);
                }
                    break;
                case 10:
                    await HandlePayload(c, cancellationToken);

                    break;
            }

            columnNumber++;
        }
    }

    private async Task HandlePayload(ReplicationValue c, CancellationToken cancellationToken)
    {
        try
        {
            await using var t = c.GetStream();
            var bytes = DecodeHex(t);
//                var json = Encoding.UTF8.GetString(bytes);
            await using var ms = new MemoryStream(bytes);
            var payload = _serializer.Deserialize<List<EventMessage>>(ms);
            if (payload != null)
            {
                foreach (var message in payload)
                {
                    await HandleMessage(message.Body.GetType(), message.Body, cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{error}", e.Message);
        }
    }

    private async Task HandleHeaders(CancellationToken cancellationToken, ReplicationValue c)
    {
        await using var stream = c.GetStream();
        var bytes = DecodeHex(stream);
        var headers = _serializer.Deserialize<Dictionary<string, object>>(bytes);
        if (headers != null)
        {
            foreach (var (_, value) in headers
                .Where(x => x.Key.StartsWith("UndispatchedMessage."))
                .OrderBy(x => x.Key))
            {
                await HandleMessage(value.GetType(), value, cancellationToken);
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
        byte GetByteValue(int hex)
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
}