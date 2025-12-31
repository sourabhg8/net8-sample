using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microservices.Configuration;
using Microservices.Core.Interfaces;
using Microservices.Infrastructure.Repositories;
using Microservices.Infrastructure.Services;

namespace Microservices.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to configure services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds application configuration using IOptions pattern
    /// </summary>
    public static IServiceCollection AddAppConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<CosmosDbSettings>(configuration.GetSection(CosmosDbSettings.SectionName));
        services.Configure<AppSettings>(configuration.GetSection(AppSettings.SectionName));
        services.Configure<PasswordSettings>(configuration.GetSection(PasswordSettings.SectionName));

        return services;
    }

    /// <summary>
    /// Adds Cosmos DB client as a singleton
    /// </summary>
    public static IServiceCollection AddCosmosDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cosmosDbSettings = configuration.GetSection(CosmosDbSettings.SectionName).Get<CosmosDbSettings>()
            ?? throw new InvalidOperationException("Cosmos DB settings not configured");

        var cosmosClientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Gateway,
            ApplicationName = "Microservices-API"
        };

        var cosmosClient = new CosmosClient(
            cosmosDbSettings.ConnectionString,
            cosmosClientOptions);

        services.AddSingleton(cosmosClient);

        return services;
    }

    /// <summary>
    /// Adds application services with dependency injection
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Cosmos DB repositories
        services.AddScoped<IUserRepository, CosmosUserRepository>();
        services.AddScoped<IOrganizationRepository, CosmosOrganizationRepository>();
        
        // Search repository remains in-memory (mock data)
        services.AddSingleton<ISearchRepository, SearchRepository>();

        // Register services
        services.AddSingleton<IPasswordService, PasswordService>(); // Singleton - stateless service
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISearchService, SearchService>();

        return services;
    }

    /// <summary>
    /// Configures JWT authentication
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not configured");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    
                    logger.LogWarning(
                        context.Exception,
                        "Authentication failed: {Message}",
                        context.Exception.Message);
                    
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    
                    var userName = context.Principal?.Identity?.Name;
                    logger.LogDebug("Token validated for user: {UserName}", userName);
                    
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    
                    logger.LogWarning(
                        "Authentication challenge issued. Error: {Error}, Description: {Description}",
                        context.Error,
                        context.ErrorDescription);
                    
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Configures Swagger/OpenAPI documentation
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Microservices API",
                Version = "v1",
                Description = "A .NET 8 API with JWT authentication, repository pattern, and best practices",
                Contact = new OpenApiContact
                {
                    Name = "API Support",
                    Email = "support@example.com"
                }
            });

            // Add JWT authentication to Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme.\n\n" +
                              "Enter 'Bearer' [space] and then your token in the text input below.\n\n" +
                              "Example: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Add correlation ID header parameter
            options.OperationFilter<CorrelationIdOperationFilter>();
        });

        return services;
    }
}

/// <summary>
/// Swagger operation filter to add correlation ID header
/// </summary>
public class CorrelationIdOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Correlation-ID",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Correlation ID for request tracing (auto-generated if not provided)",
            Schema = new OpenApiSchema
            {
                Type = "string"
            }
        });
    }
}
