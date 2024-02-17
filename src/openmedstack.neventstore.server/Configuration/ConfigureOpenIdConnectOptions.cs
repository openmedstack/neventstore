using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace OpenMedStack.NEventStore.Server.Configuration;

public class ConfigureOpenIdConnectOptions : IPostConfigureOptions<OpenIdConnectOptions>
{
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        options.Events = new OpenIdConnectEvents
        {
            OnTicketReceived = ctx => Task.CompletedTask,
            OnAuthenticationFailed = ctx => Task.CompletedTask,
            OnAccessDenied = ctx => Task.CompletedTask,
            OnRemoteFailure = ctx => Task.CompletedTask,
            OnMessageReceived = ctx => Task.CompletedTask,
            OnTokenValidated = ctx => Task.CompletedTask,
            OnAuthorizationCodeReceived = ctx => Task.CompletedTask,
            OnRemoteSignOut = ctx => Task.CompletedTask,
            OnTokenResponseReceived = ctx => Task.CompletedTask,
            OnUserInformationReceived = ctx => Task.CompletedTask,
            OnRedirectToIdentityProvider = ctx => Task.CompletedTask,
        };
    }
}
