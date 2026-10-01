using APIViewModel.Common;
using System.IO;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;
using Services.AccountService;
using Services.AccessTokenService;
using Services.AuthService;
using Services.AvatarStorageService;
using Services.EmailService;
using Services.EmailVerificationService;
using Services.AuditLogService;
using Services.CoachService;
using Services.MemberService;
using Services.MembershipPackageService;
using Services.MembershipInvoiceService;
using Services.MemberSubscriptionService;
using Services.PasswordHashService;
using Services.ReceptionistService;
using Services.Utils;
using SportsCenterManagement.Filter;
using SportsCenterManagement.Middleware;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SportsCenterManagement;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // wwwroot holds uploaded avatars. It must exist before the host resolves
        // WebRootPath: on a fresh clone the folder is absent (it is git-ignored),
        // WebRootFileProvider would then be a null provider and every uploaded
        // avatar would 404 even though the file is written correctly.
        Directory.CreateDirectory(
            Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

        builder.Services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    Dictionary<string, string[]> fieldErrors = context.ModelState
                        .Where(entry => entry.Value != null && entry.Value.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors
                                .Select(error => error.ErrorMessage)
                                .ToArray()
                        );

                    ApiErrorResponseAPIViewModel response = new ApiErrorResponseAPIViewModel
                    {
                        Success = false,
                        Error = new ApiErrorAPIViewModel
                        {
                            Code = "VALIDATION_ERROR",
                            Message = "Request validation failed.",
                            Details = fieldErrors
                        },
                        TraceId = context.HttpContext.TraceIdentifier
                    };

                    return new BadRequestObjectResult(response);
                };
            });

        builder.Services.AddMemoryCache();

        builder.Services.AddDbContext<SportsCenterManagementContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.Configure<JwtOptions>(
            builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<EmailOptions>(
            builder.Configuration.GetSection(EmailOptions.SectionName));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
            });

        // Read JwtOptions lazily so test factories that call AddInMemoryCollection
        // after host configuration are honoured. ValidateIssuerSigningKey remains
        // unconditionally true - missing key throws at request time (fail-closed).
        builder.Services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptionsMonitor<JwtOptions>>((bearerOptions, jwtMonitor) =>
            {
                JwtOptions jwtOpts = jwtMonitor.CurrentValue;
                if (string.IsNullOrWhiteSpace(jwtOpts.SigningKey))
                {
                    throw new InvalidOperationException("Jwt:SigningKey must not be empty.");
                }

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidAudience = jwtOpts.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOpts.SigningKey)),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    ClockSkew = TimeSpan.Zero
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        ApiErrorResponseAPIViewModel body = new ApiErrorResponseAPIViewModel
                        {
                            Success = false,
                            Error = new ApiErrorAPIViewModel
                            {
                                Code = "UNAUTHORIZED",
                                Message = "Authentication is required or the access token is invalid.",
                                Details = null
                            },
                            TraceId = context.HttpContext.TraceIdentifier
                        };

                        string json = JsonSerializer.Serialize(body, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        await context.Response.WriteAsync(json);
                    },

                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        ApiErrorResponseAPIViewModel body = new ApiErrorResponseAPIViewModel
                        {
                            Success = false,
                            Error = new ApiErrorAPIViewModel
                            {
                                Code = "FORBIDDEN",
                                Message = "You do not have permission to access this resource.",
                                Details = null
                            },
                            TraceId = context.HttpContext.TraceIdentifier
                        };

                        string json = JsonSerializer.Serialize(body, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        await context.Response.WriteAsync(json);
                    }
                };
            });

        builder.Services.AddAuthorization();

        string[] allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }
                // When allowedOrigins is empty, no origin is added - all cross-origin
                // requests are denied until Cors:AllowedOrigins is populated.

                policy
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Authorization", "Content-Type", "Accept");
            });
        });

        builder.Services.AddOpenApiDocument(document =>
        {
            document.Title = "SportsCenterManagement API";
            document.AddSecurity("Bearer", Array.Empty<string>(), new OpenApiSecurityScheme
            {
                Type = OpenApiSecuritySchemeType.ApiKey,
                Name = "Authorization",
                In = OpenApiSecurityApiKeyLocation.Header,
                Description = "Paste the complete value: Bearer <access-token>"
            });
            document.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
        });

        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            containerBuilder.RegisterType<AccountService>().As<IAccountService>();
            containerBuilder.RegisterType<AuthService>().As<IAuthService>();
            containerBuilder.RegisterType<EmailService>().As<IEmailService>();
            containerBuilder.RegisterType<EmailVerificationService>()
                .As<IEmailVerificationService>();
            containerBuilder.RegisterType<MemberService>().As<IMemberService>();
            containerBuilder.RegisterType<MembershipPackageService>().As<IMembershipPackageService>();
            containerBuilder.RegisterType<MembershipInvoiceService>().As<IMembershipInvoiceService>();
            containerBuilder.RegisterType<MemberSubscriptionService>().As<IMemberSubscriptionService>();
            containerBuilder.RegisterType<CoachService>().As<ICoachService>();
            containerBuilder.RegisterType<ReceptionistService>().As<IReceptionistService>();
            containerBuilder.RegisterType<AuditLogService>().As<IAuditLogService>();
            containerBuilder.RegisterType<AccessTokenService>().As<IAccessTokenService>();
            containerBuilder.RegisterType<PasswordHashService>().As<IPasswordHashService>();
            containerBuilder.RegisterType<AvatarStorageService>().As<IAvatarStorageService>();
            containerBuilder.RegisterType<AuthFilter>().AsSelf();
        });

        WebApplication app = builder.Build();

        app.UseMiddleware<GlobalExceptionMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseOpenApi();
            app.UseSwaggerUi();
            app.UseReDoc(options =>
            {
                options.Path = "/redoc";
            });
        }

        app.UseHttpsRedirection();
        app.UseCors();

        // Serves uploaded avatars from wwwroot/uploads/avatars.
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
