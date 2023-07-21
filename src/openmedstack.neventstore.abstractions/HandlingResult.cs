namespace OpenMedStack.NEventStore.Abstractions;

public enum HandlingResult
{
    MoveToNext = 0,
    Retry = 1,
    Stop = 2
}
