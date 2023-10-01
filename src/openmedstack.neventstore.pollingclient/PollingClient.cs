using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.PollingClient;

using Microsoft.Extensions.Logging;

/// <summary>
/// This is the new polling client that does not depends on RX.
/// </summary>
public class PollingClient : IDisposable
{
    private static readonly TaskStatus[] DisposableStatuses =
        { TaskStatus.RanToCompletion, TaskStatus.Faulted, TaskStatus.Canceled };

    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger;
    private readonly Func<ICommit, Task<HandlingResult>> _commitCallback;
    private readonly IPersistStreams _persistStreams;
    private readonly TimeSpan _waitInterval;
    private Task? _pollingThread;
    private Func<IAsyncEnumerable<ICommit>>? _pollingFunc;
    private long _checkpointToken;

    /// <summary>
    ///
    /// </summary>
    /// <param name="persistStreams"></param>
    /// <param name="callback"></param>
    /// <param name="logger">The <see cref="ILogger"/>.</param>
    /// <param name="waitInterval">Interval in Milliseconds to wait when the provider
    /// return no more commit and the next request</param>
    public PollingClient(
        IPersistStreams persistStreams,
        Func<ICommit, Task<HandlingResult>> callback,
        ILogger logger,
        TimeSpan waitInterval = default)
    {
        _logger = logger;
        _waitInterval = waitInterval == default ? TimeSpan.FromMilliseconds(300) : waitInterval;

        _commitCallback = callback
         ?? throw new ArgumentNullException(
                nameof(callback),
                "Cannot use polling client without callback");
        _persistStreams = persistStreams
         ?? throw new ArgumentNullException(
                nameof(persistStreams),
                "PersistStreams cannot be null");
        LastActivityTimestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Tells the caller the last tick count when the last activity occurred. This is useful for the caller
    /// to setup Health check that verify if the poller is really active and it is really loading new commits.
    /// This value is obtained with DateTime.UtcNow
    /// </summary>
    public DateTime LastActivityTimestamp { get; private set; }

    public void ConfigurePollingFunction(string bucketId)
    {
        if (_pollingThread != null)
        {
            throw new PollingClientException("Cannot configure when polling client already started polling");
        }

        _pollingFunc = () => _persistStreams.GetFrom(bucketId, _checkpointToken, _cts.Token);
    }

    public void StartFromBucket(string bucketId, long checkpointToken = 0)
    {
        if (_pollingThread != null)
        {
            throw new PollingClientException("Polling client already started");
        }

        _checkpointToken = checkpointToken;
        ConfigurePollingFunction(bucketId);
        _pollingThread = InnerPollingLoop();
    }

    public async Task Stop()
    {
        _cts.Cancel();
        if (_pollingThread != null
         && _pollingThread.Status != TaskStatus.WaitingForActivation
         && _pollingThread.Status != TaskStatus.WaitingToRun)
        {
            await _pollingThread.ConfigureAwait(false);
        }
    }

    internal Task PollNow() =>
        //if (_pollingThread == null)
        //    throw new ArgumentException("You cannot call PollNow on a poller that is not started");
        //return Task<Boolean>.Factory.StartNew(InnerPoll);
        InnerPoll();

    private int _isPolling = 0;

    private async Task InnerPollingLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (await InnerPoll().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(_waitInterval).ConfigureAwait(false);
        }
    }

    private async Task<bool> InnerPoll()
    {
        if (_pollingFunc == null)
        {
            throw new Exception($"{nameof(ConfigurePollingFunction)} must be called before polling.");
        }

        await Task.Yield();
        if (Interlocked.CompareExchange(ref _isPolling, 1, 0) == 0)
        {
            LastActivityTimestamp = DateTime.UtcNow;
            try
            {
                await foreach (var commit in _pollingFunc().ConfigureAwait(false))
                {
                    LastActivityTimestamp = DateTime.UtcNow;
                    if (_cts.IsCancellationRequested)
                    {
                        return true;
                    }

                    var result = await _commitCallback(commit).ConfigureAwait(false);
                    if (result == HandlingResult.Retry)
                    {
                        _logger.LogTrace(
                            "Commit callback ask retry for checkpointToken {0} - last dispatched {1}",
                            commit.CheckpointToken,
                            _checkpointToken);
                        break;
                    }

                    if (result == HandlingResult.Stop)
                    {
                        await Stop().ConfigureAwait(false);
                        return true;
                    }

                    _checkpointToken = commit.CheckpointToken;
                }
            }
            catch (Exception ex)
            {
                // These exceptions are expected to be transient
                _logger.LogError(ex, "Error during polling client {exception}", ex.Message);
            }

            Interlocked.Exchange(ref _isPolling, 0);
        }

        return false;
    }

    private bool _isDisposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            Stop().GetAwaiter().GetResult();
            if (_pollingThread != null && DisposableStatuses.Contains(_pollingThread.Status))
            {
                _pollingThread?.Dispose();
            }
        }

        _isDisposed = true;
    }
}
