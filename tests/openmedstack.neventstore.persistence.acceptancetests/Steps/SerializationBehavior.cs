using Microsoft.Extensions.Logging.Abstractions;
using OpenMedStack.NEventStore.Abstractions;
using OpenMedStack.NEventStore.Abstractions.Persistence;
using OpenMedStack.NEventStore.Serialization;
using TechTalk.SpecFlow;
using Xunit;

namespace OpenMedStack.NEventStore.Persistence.AcceptanceTests.Steps;

[Binding]
[Scope(Feature = "Event serialization")]
public class SerializationBehavior
{
    private Dictionary<string, object> _headers = null!;
    private List<EventMessage> _eventMessages = null!;
    private SimpleMessage _message = null!;
    private ISerialize _serializer = null!;
    private byte[] _serialized = null!;
    private SimpleMessage? _deserializedMessage;
    private List<EventMessage>? _deserializedEventMessages;
    private Dictionary<string, object>? _deserializedHeaders;
    private Snapshot _snapshot = null!;
    private Snapshot? _deserializedSnapshot;

    [Given(@"a serializer")]
    public void GivenASerializer()
    {
        _serializer = new NesJsonSerializer(NullLogger<NesJsonSerializer>.Instance);
    }

    [Given(@"a simple message")]
    public void GivenASimpleMessage()
    {
        _message = new SimpleMessage
        {
            Id = "abc", Value = "test", Count = 1, Created = new DateTime(2023, 11, 01, 0, 0, 0, DateTimeKind.Utc),
            Contents = { "1" }
        };
    }

    [When(@"serializing the simple message")]
    public void WhenSerializingTheSimpleMessage()
    {
        _serialized = _serializer.Serialize(_message);
    }

    [When(@"deserializing it back")]
    public void WhenDeserializingItBack()
    {
        _deserializedMessage = _serializer.Deserialize<SimpleMessage>(_serialized);
    }

    [Then(@"should deserialize a message which contains the same values as the serialized message")]
    public void ThenShouldDeserializeAMessageWhichContainsTheSameValuesAsTheSerializedMessage(Table table)
    {
        foreach (var row in table.Rows)
        {
            var property = row["property"];
            var propertyInfo = typeof(SimpleMessage).GetProperty(property);
            var propertyValue = propertyInfo!.GetValue(_deserializedMessage);
            var originalValue = propertyInfo.GetValue(_message);
            var equal = propertyInfo.PropertyType switch
            {
                not null when propertyInfo.PropertyType == typeof(string) => propertyValue!.ToString() ==
                    originalValue!.ToString(),
                not null when propertyInfo.PropertyType == typeof(int) => Convert.ToInt32(propertyValue!) ==
                    Convert.ToInt32(originalValue!),
                not null when propertyInfo.PropertyType == typeof(DateTime) => Convert.ToDateTime(propertyValue!) -
                    Convert.ToDateTime(originalValue!) == TimeSpan.Zero,
                not null when propertyInfo.PropertyType == typeof(List<string>) =>
                    ((List<string>)propertyValue!).Count ==
                    ((List<string>)originalValue!).Count,
                _ => false
            };

            Assert.True(equal, $"Property {property} should be equal");
        }
    }

    [Given(@"a list of event messages")]
    public void GivenAListOfEventMessages()
    {
        _eventMessages =
        [
            new("some value"),
            new(42),
            new(new SimpleMessage())
        ];
    }

    [When(@"serializing the event messages")]
    public void WhenSerializingTheEventMessages()
    {
        _serialized = _serializer.Serialize(_eventMessages);
    }

    [When(@"deserializing them back")]
    public void WhenDeserializingThemBack()
    {
        _deserializedEventMessages = _serializer.Deserialize<List<EventMessage>>(_serialized);
    }

    [Then(@"should deserialize the same number of event messages as it serialized")]
    public void ThenShouldDeserializeTheSameNumberOfEventMessagesAsItSerialized()
    {
        Assert.Equal(_eventMessages.Count, _deserializedEventMessages?.Count);
    }

    [Then(@"should deserialize the complex types within the event messages")]
    public void ThenShouldDeserializeTheComplexTypesWithinTheEventMessages()
    {
        Assert.IsType<SimpleMessage>(_deserializedEventMessages?.Last().Body);
    }

    [Given(@"a set of headers")]
    public void GivenASetOfHeaders()
    {
        _headers = new Dictionary<string, object>
        {
            { "HeaderKey", "SomeValue" },
            { "AnotherKey", 42 },
            { "AndAnotherKey", Guid.NewGuid() },
            { "LastKey", new SimpleMessage() }
        };
    }

    [When(@"serializing the headers")]
    public void WhenSerializingTheHeaders()
    {
        _serialized = _serializer.Serialize(_headers);
    }

    [When(@"deserializing the headers back")]
    public void WhenDeserializingTheHeadersBack()
    {
        _deserializedHeaders = _serializer.Deserialize<Dictionary<string, object>>(_serialized);
    }

    [Then(@"should deserialize the same number of headers as it serialized")]
    public void ThenShouldDeserializeTheSameNumberOfHeadersAsItSerialized()
    {
        Assert.Equal(_headers.Count, _deserializedHeaders?.Count);
    }

    [Then(@"should deserialize the complex types within the headers")]
    public void ThenShouldDeserializeTheComplexTypesWithinTheHeaders()
    {
        Assert.IsType<SimpleMessage>(_deserializedHeaders?.Last().Value);
    }

    [Given(@"a snapshot")]
    public void GivenASnapshot()
    {
        var payload = new Dictionary<string, List<int>>();
        _snapshot = new Snapshot(Bucket.Default, Guid.NewGuid().ToString(), 42, payload);
    }

    [When(@"serializing the snapshot")]
    public void WhenSerializingTheSnapshot()
    {
        _serialized = _serializer.Serialize(_snapshot);
    }

    [When(@"deserializing the snapshot back")]
    public void WhenDeserializingTheSnapshotBack()
    {
        _deserializedSnapshot = _serializer.Deserialize<Snapshot>(_serialized);
    }

    [Then(@"should correctly deserialize the untyped payload contents")]
    public void ThenShouldCorrectlyDeserializeTheUntypedPayloadContents()
    {
        Assert.Equal(_snapshot.Payload.GetType(), _deserializedSnapshot?.Payload.GetType());
    }
}
