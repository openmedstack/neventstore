using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OpenMedStack.NEventStore
{
    using System.Linq;

    /// <summary>
    ///     Represents a single element in a stream of events.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class EventMessage
    {
        /// <summary>
        ///     Initializes a new instance of the EventMessage class.
        /// </summary>
        public EventMessage(object body, IDictionary<string, object>? headers = null)
        {
            Headers = headers == null ? new Dictionary<string, object>() : headers.ToDictionary(x => x.Key, x => x.Value);
            Body = body;
        }

        /// <summary>
        ///     Gets the metadata which provides additional, unstructured information about this message.
        /// </summary>
        [DataMember]
        public Dictionary<string, object> Headers { get; private set; }

        /// <summary>
        ///     Gets or sets the actual event message body.
        /// </summary>
        [DataMember]
        public object Body { get; private set; }
    }
}