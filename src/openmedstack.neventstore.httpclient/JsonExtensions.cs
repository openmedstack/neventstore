using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenMedStack.NEventStore.HttpClient;

internal static class JsonExtensions
{
    /// <summary>
    /// Asynchronously load and synchronously deserialize values from a stream containing a JSON array.  The root object of the JSON stream must in fact be an array, or an exception is thrown
    /// </summary>
    public static async Task<T?> DeserializeAsync<T>(
        this Stream stream,
        JsonSerializerSettings? settings = default,
        CancellationToken cancellationToken = default)
    {
        var serializer = JsonSerializer.CreateDefault(settings);

        // StreamReader and JsonTextReader do not implement IAsyncDisposable so let the caller dispose the stream.
        using var textReader = new StreamReader(stream, leaveOpen: true);
        var reader = new JsonTextReader(textReader) { CloseInput = false };
        await using var _ = reader.ConfigureAwait(false);
        var token = await JToken.LoadAsync(reader, cancellationToken).ConfigureAwait(false);
        return token.ToObject<T>(serializer);
    }

    /// <summary>
    /// Asynchronously load and synchronously deserialize values from a stream containing a JSON array.  The root object of the JSON stream must in fact be an array, or an exception is thrown
    /// </summary>
    public static async IAsyncEnumerable<T?> DeserializeAsyncEnumerable<T>(
        this Stream stream,
        JsonSerializerSettings? settings = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var serializer = JsonSerializer.CreateDefault(settings);
        var loadSettings = new JsonLoadSettings
            { LineInfoHandling = LineInfoHandling.Ignore }; // For performance do not load line info.
        using var textReader = new StreamReader(stream, leaveOpen: true);
        var reader = new JsonTextReader(textReader) { CloseInput = false };
        await using var _ = reader.ConfigureAwait(false);
        await foreach (var token in LoadAsyncEnumerable(reader, loadSettings, cancellationToken).ConfigureAwait(false))
        {
            yield return token.ToObject<T>(serializer);
        }
    }

    /// <summary>
    /// Asynchronously load and return JToken values from a stream containing a JSON array.  The root object of the JSON stream must in fact be an array, or an exception is thrown
    /// </summary>
    private static async IAsyncEnumerable<JToken> LoadAsyncEnumerable(
        JsonTextReader reader,
        JsonLoadSettings? settings = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        (await reader.MoveToContentAndAssertAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            .AssertTokenType(JsonToken.StartArray);
        cancellationToken.ThrowIfCancellationRequested();
        while ((await reader.ReadToContentAndAssert(cancellationToken).ConfigureAwait(false)).TokenType !=
            JsonToken.EndArray)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await JToken.LoadAsync(reader, settings, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void AssertTokenType(this JsonReader reader, JsonToken tokenType)
    {
        if (reader.TokenType != tokenType)
        {
            throw new JsonSerializationException($"Unexpected token {reader.TokenType}, expected {tokenType}");
        }
    }

    private static async Task<JsonReader> ReadToContentAndAssert(
        this JsonReader reader,
        CancellationToken cancellationToken = default) =>
        await (await reader.ReadAndAssertAsync(cancellationToken).ConfigureAwait(false))
            .MoveToContentAndAssertAsync(cancellationToken).ConfigureAwait(false);

    private static async Task<JsonReader> MoveToContentAndAssertAsync(
        this JsonReader reader,
        CancellationToken cancellationToken = default)
    {
        if (reader.TokenType == JsonToken.None) // Skip past beginning of stream.
        {
            await reader.ReadAndAssertAsync(cancellationToken).ConfigureAwait(false);
        }

        while (reader.TokenType == JsonToken.Comment) // Skip past comments.
            await reader.ReadAndAssertAsync(cancellationToken).ConfigureAwait(false);
        return reader;
    }

    private static async Task<JsonReader> ReadAndAssertAsync(
        this JsonReader reader,
        CancellationToken cancellationToken = default)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new JsonReaderException("Unexpected end of JSON stream.");
        }

        return reader;
    }
}
