namespace OpenMedStack.NEventStore.Persistence.Sql;

using System;
using System.Runtime.Serialization;

/// <summary>
///     Indicates that a unique constraint or duplicate key violation occurred.
/// </summary>
public class UniqueKeyViolationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the UniqueKeyViolationException class.
    /// </summary>
    public UniqueKeyViolationException()
    {}

    /// <summary>
    ///     Initializes a new instance of the UniqueKeyViolationException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UniqueKeyViolationException(string message)
        : base(message)
    {}

    /// <summary>
    ///     Initializes a new instance of the UniqueKeyViolationException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The message that is the cause of the current exception.</param>
    public UniqueKeyViolationException(string message, Exception innerException)
        : base(message, innerException)
    {}
}
