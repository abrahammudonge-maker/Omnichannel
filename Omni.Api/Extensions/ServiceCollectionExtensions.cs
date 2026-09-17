using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Omni.Api.Authentication;
using Omni.Api.Configuration;
using Omni.Api.Hubs;
using Omni.Application.Configuration;
using Omni.Application.Interfaces;
using Omni.Application.Services;
using Omni.Application.Validators;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Providers;
using Omni.Infrastructure.Repositories;
using System.Text;
using System.Threading.RateLimiting;

namespace Omni.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<MetaSettings>(configuration.GetSection("Meta"));
        services.AddDataProtection()
            .SetApplicationName("Omnichannel")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(contentRootPath, "App_Data", "keys")));
        services.AddMemoryCache();
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IConversationAssignmentRepository, ConversationAssignmentRepository>();
        services.AddScoped<IConversationTagRepository, ConversationTagRepository>();
        services.AddScoped<IConversationStatusRepository, ConversationStatusRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IInternalNoteRepository, InternalNoteRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IChannelAccountRepository, ChannelAccountRepository>();
        services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<ITemplateMessageService, TemplateMessageService>();
        services.AddScoped<ICallRepository, CallRepository>();
        services.AddScoped<IPhoneNumberRepository, PhoneNumberRepository>();
        services.AddScoped<ICallQueueRepository, CallQueueRepository>();
        services.AddScoped<ICallQueueMemberRepository, CallQueueMemberRepository>();
        services.AddScoped<ICallEventRepository, CallEventRepository>();
        services.AddScoped<IVoiceService, VoiceService>();
        services.AddScoped<IVoiceNotifier, SignalRVoiceNotifier>();
        services.AddSingleton<IVoiceProvider, FakeVoiceProvider>();
        services.AddSingleton<IVoiceProvider, TwilioVoiceProvider>();
        services.AddSingleton<IVoiceProviderFactory, VoiceProviderFactory>();
        services.AddSignalR();
        services.AddScoped<IOrganizationSettingRepository, OrganizationSettingRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddHostedService<EmailInboxPollingService>();
        services.AddHttpClient("GraphApi");
        services.AddSingleton<IMetaMessageSender, GraphApiMessageSender>();
        services.AddSingleton<IMetaTemplateService, GraphApiTemplateService>();
        services.AddScoped<IInboundMessageForwarder, HttpInboundMessageForwarder>();
        services.AddScoped<IMetaEmbeddedSignupService, MetaEmbeddedSignupService>();
        services.AddHttpClient("Twilio");
        services.AddSingleton<ISmsSender, TwilioSmsSender>();

        services.AddControllers();
        services.AddHealthChecks();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("auth", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
            options.AddPolicy("messages", httpContext =>
            {
                var partitionKey = httpContext.User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
            options.AddPolicy("integrations", httpContext =>
            {
                var partitionKey = httpContext.User.Claims.FirstOrDefault(c => c.Type == "ApiKeyId")?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings?.Issuer ?? "https://localhost",
                    ValidAudience = jwtSettings?.Audience ?? "omnichannel",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key ?? "super-secret-key-for-development-1234567890"))
                };
                // Browsers can't set an Authorization header on a WebSocket handshake, so the SignalR JS
                // client sends the token as ?access_token=... instead — only honored for the hub path.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/voice"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireOrganizationAdmin", policy => policy.RequireRole("OrganizationAdmin", "PlatformSuperAdmin"));
            options.AddPolicy("RequireAgent", policy => policy.RequireRole("Agent", "Supervisor", "OrganizationAdmin", "PlatformSuperAdmin"));
            options.AddPolicy("RequirePlatformSuperAdmin", policy => policy.RequireRole("PlatformSuperAdmin"));
        });

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "Enter 'Bearer' followed by your token"
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
