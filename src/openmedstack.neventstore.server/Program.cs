using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenMedStack.Autofac;
using OpenMedStack.Autofac.MassTransit;
using OpenMedStack.Autofac.NEventstore;
using OpenMedStack.Autofac.NEventstore.Sql;
using OpenMedStack.NEventStore.Server.Configuration;
using OpenMedStack.NEventStore.Server.Service;

namespace OpenMedStack.NEventStore.Server;

using Npgsql;
using OpenMedStack;
using OpenMedStack.Web.Autofac;
using Persistence.Sql.SqlDialects;

internal class Program
{
    public static async Task Main()
    {
        var configuration = new EventStoreConfiguration
        {
            Name = typeof(EventStoreController).Assembly.GetName().Name!,
            Urls = ["https://localhost:5001"],
            TenantPrefix = "openmedstack",
            QueueName = "eventstore",
            ServiceBus = new Uri("loopback://localhost"),
            ConnectionString = "",
            TokenService = "https://identity.reimers.dk",
            ClientId = "eventstore",

        };

        var chassis = Chassis.From(configuration)
            .UsingNEventStore()
            .UsingSqlEventStore<EventStoreConfiguration, PostgreSqlDialect>(NpgsqlFactory.Instance)
            .AddAutofacModules((c, _) => new ServerModule(c))
            //.UsingMassTransitOverRabbitMq()
            .UsingInMemoryMassTransit()
            .UsingWebServer(_ => new DelegateWebApplicationConfiguration(
                services =>
                {
                    services.AddResponseCompression();
                    services.AddGrpc();
                    services.AddAuthorization();
                    services.AddControllers();
                    services.AddAuthentication(o =>
                        {
                            o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                            o.DefaultAuthenticateScheme = OpenIdConnectDefaults.AuthenticationScheme;
                            o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                        })
                        .AddCookie(c =>
                        {
                            c.SlidingExpiration = true;
                            c.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                        })
                        .AddJwtBearer(
                            options =>
                            {
                                options.SaveToken = true;
                                options.Authority = configuration.TokenService;
                                options.RequireHttpsMetadata = false;
                                options.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateActor = false,
                                    ValidateLifetime = true,
                                    ValidateTokenReplay = true,
                                    LogValidationExceptions = true,
                                    ValidateAudience = false,
                                    ValidateIssuer = true,
                                    ValidateIssuerSigningKey = true,
                                    ValidIssuers = new[] { configuration.TokenService }
                                };
                            })
                        .AddOpenIdConnect(options =>
                        {
                            options.DisableTelemetry = true;
                            options.Scope.Add("uma_protection");
                            options.DataProtectionProvider = new EphemeralDataProtectionProvider();
                            options.SaveTokens = true;
                            options.Authority = "https://identity.reimers.dk";
                            options.AuthenticationMethod = OpenIdConnectRedirectBehavior.RedirectGet;
                            options.UsePkce = true;
                            options.RequireHttpsMetadata = false;
                            options.GetClaimsFromUserInfoEndpoint = false;
                            options.ResponseType = OpenIdConnectResponseType.Code;
                            options.ResponseMode = OpenIdConnectResponseMode.Query;
                            options.ProtocolValidator.RequireNonce = true;
                            options.ProtocolValidator.RequireState = false;
                            options.ClientId = configuration.ClientId;
                            options.ClientSecret = configuration.Secret;
                        });
                    services.ConfigureOptions<ConfigureMvcNewtonsoftJsonOptions>()
                        .ConfigureOptions<ConfigureOpenIdConnectOptions>()
                        .AddHealthChecks()
                        .AddNpgSql(configuration.ConnectionString, failureStatus: HealthStatus.Unhealthy);
                },
                app =>
                {var forwardedHeadersOptions = new ForwardedHeadersOptions
                        { ForwardedHeaders = ForwardedHeaders.All, ForwardLimit = null };
                    forwardedHeadersOptions.KnownNetworks.Clear();
                    forwardedHeadersOptions.KnownProxies.Clear();

                    app.UseForwardedHeaders(forwardedHeadersOptions);
                    app.UseHsts();
                    app.UseResponseCompression();
                    app.UseCors(p => p.AllowAnyOrigin());
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e =>
                    {
                        e.MapGrpcService<EventStoreService>();
                        e.MapControllers();
                        e.MapHealthChecks("/health");
                    });
                }));
        chassis.Start();
        await chassis.DisposeAsync().ConfigureAwait(false);
    }
}
