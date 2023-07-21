using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Persistence;

namespace OpenMedStack.NEventStore.Conversion;

using Microsoft.Extensions.Logging;

public class EventUpconverterPipelineHook : PipelineHookBase
{
    private readonly ILogger _logger;
    private readonly IDictionary<Type, Func<object, object>> _converters;

    public EventUpconverterPipelineHook(IDictionary<Type, Func<object, object>> converters, ILogger logger)
    {
        _logger = logger;
        _converters = converters ?? throw new ArgumentNullException(nameof(converters));
    }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public override Task<ICommit> Select(ICommit committed)
    {
        var converted = false;
        var eventMessages = committed
            .Events
            .Select(eventMessage =>
            {
                var convert = Convert(eventMessage.Body);
                if (ReferenceEquals(convert, eventMessage.Body))
                {
                    return eventMessage;
                }
                converted = true;
                return new EventMessage(convert, eventMessage.Headers);
            })
            .ToList();
        if (!converted)
        {
            return Task.FromResult(committed);
        }

        return Task.FromResult<ICommit>(
            new Commit(
                committed.BucketId,
                committed.StreamId,
                committed.StreamRevision,
                committed.CommitId,
                committed.CommitSequence,
                committed.CommitStamp,
                committed.CheckpointToken,
                committed.Headers,
                eventMessages));
    }

    protected virtual void Dispose(bool disposing)
    {
        _converters.Clear();
    }

    private object Convert(object source)
    {
        if (!_converters.TryGetValue(source.GetType(), out var converter))
        {
            return source;
        }

        var target = converter(source);
        _logger.LogDebug(Resources.ConvertingEvent, source.GetType(), target.GetType());

        return Convert(target);
    }
}