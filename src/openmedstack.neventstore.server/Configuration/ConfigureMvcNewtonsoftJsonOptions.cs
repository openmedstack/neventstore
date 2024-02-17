using Microsoft.AspNetCore.Mvc;

namespace OpenMedStack.NEventStore.Server.Configuration;

using Microsoft.Extensions.Options;
using Newtonsoft.Json;

public class ConfigureMvcNewtonsoftJsonOptions : IPostConfigureOptions<MvcNewtonsoftJsonOptions>
{
    private readonly JsonSerializerSettings _settings;

    public ConfigureMvcNewtonsoftJsonOptions(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public void PostConfigure(string? name, MvcNewtonsoftJsonOptions options)
    {
        options.SerializerSettings.ContractResolver = _settings.ContractResolver;
        options.SerializerSettings.DateFormatHandling = _settings.DateFormatHandling;
        options.SerializerSettings.DateTimeZoneHandling = _settings.DateTimeZoneHandling;
        options.SerializerSettings.NullValueHandling = _settings.NullValueHandling;
        options.SerializerSettings.DefaultValueHandling = _settings.DefaultValueHandling;
        options.SerializerSettings.TypeNameHandling = _settings.TypeNameHandling;
        options.SerializerSettings.Formatting = _settings.Formatting;
        options.SerializerSettings.DateParseHandling = _settings.DateParseHandling;
    }
}
