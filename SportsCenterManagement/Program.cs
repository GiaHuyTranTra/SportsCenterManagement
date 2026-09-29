using Autofac;
using Autofac.Extensions.DependencyInjection;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;
using Services.AccountService;
using Services.AccessTokenService;
using Services.AuthService;
using Services.MemberService;
using Services.MembershipPackageService;
using Services.MembershipInvoiceService;
using Services.MemberSubscriptionService;
using Services.PasswordHashService;
using Services.Utils;
using SportsCenterManagement.Filter;
using System.Text;

namespace SportsCenterManagement;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        builder.Services.AddDbContext<SportsCenterManagementContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        IConfigurationSection jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        JwtOptions jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing Jwt configuration.");

        builder.Services.Configure<JwtOptions>(jwtSection);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();
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
            containerBuilder.RegisterType<MemberService>().As<IMemberService>();
            containerBuilder.RegisterType<MembershipPackageService>().As<IMembershipPackageService>();
            containerBuilder.RegisterType<MembershipInvoiceService>().As<IMembershipInvoiceService>();
            containerBuilder.RegisterType<MemberSubscriptionService>().As<IMemberSubscriptionService>();
            containerBuilder.RegisterType<AccessTokenService>().As<IAccessTokenService>();
            containerBuilder.RegisterType<PasswordHashService>().As<IPasswordHashService>();
            containerBuilder.RegisterType<AuthFilter>().AsSelf();
        });

        WebApplication app = builder.Build();

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
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
