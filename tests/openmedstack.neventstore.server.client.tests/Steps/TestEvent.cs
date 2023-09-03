using OpenMedStack.Events;

namespace OpenMedStack.NEventStore.Server.Tests.Steps;

internal record TestEvent : DomainEvent
{
    public TestEvent(string value, string source, int version, DateTimeOffset timeStamp, string? correlationId = null) :
        base(source, version, timeStamp, correlationId)
    {
        Value = value;
    }

    public string Value { get; }
}
