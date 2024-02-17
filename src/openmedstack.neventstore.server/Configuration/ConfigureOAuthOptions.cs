namespace OpenMedStack.NEventStore.Server.Configuration;

using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

public class ConfigureOAuthOptions : IPostConfigureOptions<OAuthOptions>
{
    public void PostConfigure(string? name, OAuthOptions options)
    {
        options.Events.OnRedirectToAuthorizationEndpoint = ctx =>
        {
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    }
}
