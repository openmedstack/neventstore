namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;

[Serializable]
public class ConfigurationErrorsException : Exception
{
    public ConfigurationErrorsException() { }
    public ConfigurationErrorsException(string message) : base(message) { }
    public ConfigurationErrorsException(string message, Exception inner) : base(message, inner) { }
}
