using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Tests.Client;

using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PollingClient;

/// <summary>
/// Represents a client that poll the storage for latest commits.
/// </summary>
public sealed class PollingClientRx
{
    private readonly PollingClient _pollingClient2;

    private readonly Subject<ICommit> _subject;

    public PollingClientRx(
        IManagePersistence managePersistence,
        TimeSpan waitInterval = default)
    {
        if (waitInterval == default)
        {
            waitInterval = TimeSpan.FromMilliseconds(5000);
        }

        if (waitInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Must be greater than 0", nameof(waitInterval));
        }

        _subject = new Subject<ICommit>();
        _pollingClient2 = new PollingClient(
            managePersistence,
            c =>
            {
                _subject.OnNext(c);
                return Task.FromResult(HandlingResult.MoveToNext);
            },
            NullLogger.Instance,
            waitInterval: waitInterval);
    }

    private long _checkpointToObserveFrom;

    public IObservable<ICommit> ObserveFrom(long checkpointToken = 0)
    {
        _checkpointToObserveFrom = checkpointToken;
        return _subject;
    }


    internal void Start()
    {
        _pollingClient2.StartFromBucket(Bucket.Default, _checkpointToObserveFrom);
    }

    internal void Dispose()
    {
        _pollingClient2.Dispose();
    }


    internal void StartFromBucket(string bucketId)
    {
        _pollingClient2.StartFromBucket(bucketId);
    }
}
