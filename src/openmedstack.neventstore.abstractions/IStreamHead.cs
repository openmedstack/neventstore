namespace OpenMedStack.NEventStore.Abstractions;

/// <summary>
///     Indicates the most recent information representing the head of a given stream.
/// </summary>
public interface IStreamHead
{
    /// <summary>
    ///     Gets the value which uniquely identifies the stream where the last snapshot exceeds the allowed threshold.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    ///     Gets the value which uniquely identifies the stream where the last snapshot exceeds the allowed threshold.
    /// </summary>
    string StreamId { get; }

    /// <summary>
    ///     Gets the value which indicates the revision, length, or number of events committed to the stream.
    /// </summary>
    int HeadRevision { get; }

    /// <summary>
    ///     Gets the value which indicates the revision at which the last snapshot was taken.
    /// </summary>
    int SnapshotRevision { get; }
}
