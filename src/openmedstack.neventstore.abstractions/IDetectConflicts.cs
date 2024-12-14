namespace OpenMedStack.NEventStore.Abstractions;

public interface IDetectConflicts
{
    bool ConflictsWith(IEnumerable<EventMessage> committed, IEnumerable<EventMessage> attempting);
}

public class DefaultConflictDetector(IEnumerable<Func<EventMessage, EventMessage, bool>> conflictDelegates)
    : IDetectConflicts
{
    public bool ConflictsWith(IEnumerable<EventMessage> committed, IEnumerable<EventMessage> attempting)
    {
        return committed.Any(committedMessage => attempting.Any(attemptingMessage =>
            conflictDelegates.Any(x => x(committedMessage, attemptingMessage))));
    }
}
