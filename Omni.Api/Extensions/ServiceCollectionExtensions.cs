using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Omni.Api.Configuration;
using Omni.Application.Interfaces;
using Omni.Application.Services;
using Omni.Application.Validators;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Repositories;
using System.Text;

namespace Omni.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IConversationAssignmentRepository, ConversationAssignmentRepository>();
        services.AddScoped<IConversationStatusRepository, ConversationStatusRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IInternalNoteRepository, InternalNoteRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IChannelAccountRepository, ChannelAccountRepository>();
        services.AddScoped<IOrganizationSettingRepository, OrganizationSettingRepository>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddControllers();
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
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireOrganizationAdmin", policy => policy.RequireRole("OrganizationAdmin", "PlatformSuperAdmin"));
            options.AddPolicy("RequireAgent", policy => policy.RequireRole("Agent", "Supervisor", "OrganizationAdmin", "PlatformSuperAdmin"));
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
        });

        return services;
    }
}
