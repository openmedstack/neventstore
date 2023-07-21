namespace OpenMedStack.NEventStore;

using Microsoft.Extensions.Logging;

public static class EventUpconverterWireupExtensions
{
    public static EventUpconverterWireup UsingEventUpconversion(this Wireup wireup, ILogger logger) => new(wireup, logger);
}