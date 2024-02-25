namespace OpenMedStack.NEventStore.Abstractions;

/// <summary>
/// Defines the result of handling a commit.
/// </summary>
public enum HandlingResult
{
    /// <summary>
    /// The commit was handled successfully and the dispatcher should move to the next commit.
    /// </summary>
    MoveToNext = 0,

    /// <summary>
    /// The commit was not handled successfully and the dispatcher should retry handling the commit.
    /// </summary>
    Retry = 1,

    /// <summary>
    /// The dispatcher should stop handling commits.
    /// </summary>
    Stop = 2
}
