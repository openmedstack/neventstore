namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public class SimpleMessage
{
    public SimpleMessage()
    {
        Contents = new List<string>();
    }

    public string? Id { get; set; }
    public DateTime Created { get; set; }
    public string? Value { get; set; }
    public int Count { get; set; }

    [SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists",
        Justification = "This is an acceptance test DTO and the structure doesn't really matter.")]
    public List<string> Contents { get; }
}
