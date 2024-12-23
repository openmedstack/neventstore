namespace OpenMedStack.NEventStore.Abstractions;

/// <summary>
///     Represents a general failure of the storage engine or persistence infrastructure.
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the StorageException class.
    /// </summary>
    protected StorageException()
    { }

    /// <summary>
    ///     Initializes a new instance of the StorageException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageException(string message)
        : base(message)
    { }

    /// <summary>
    ///     Initializes a new instance of the StorageException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The message that is the cause of the current exception.</param>
    public StorageException(string message, Exception innerException)
        : base(message, innerException)
    { }
}
