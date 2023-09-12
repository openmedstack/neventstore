namespace OpenMedStack.NEventStore.PollingClient;

/// <summary>
/// ApplicationException has been deprecated in .NET Core
/// </summary>
[Serializable]
public class PollingClientException : Exception
{
    public PollingClientException() { }
    public PollingClientException(string message) : base(message) { }
    public PollingClientException(string message, Exception inner) : base(message, inner) { }
}
