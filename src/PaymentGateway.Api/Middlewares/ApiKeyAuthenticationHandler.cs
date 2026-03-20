using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.Services;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.Redis;

namespace PaymentGateway.Api.Middlewares;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IRedisService _redisService;
    private readonly IUnitOfWork _unitOfWork;
    private const string ApiKeyHeaderName = "X-API-Key";
    private static readonly TimeSpan ApiKeyCacheTtl = TimeSpan.FromMinutes(10);

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IRedisService redisService,
        IUnitOfWork unitOfWork)
        : base(options, logger, encoder)
    {
        _redisService = redisService;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if (string.IsNullOrEmpty(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var cacheKey = $"apikey:{providedApiKey}";
        var parsedMerchantId = Guid.Empty;

        try
        {
            var cachedMerchantId = await _redisService.GetAsync<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cachedMerchantId) &&
                Guid.TryParse(cachedMerchantId, out var merchantIdFromCache) &&
                merchantIdFromCache != Guid.Empty)
            {
                parsedMerchantId = merchantIdFromCache;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to read API key cache entry");
        }

        if (parsedMerchantId == Guid.Empty)
        {
            var merchant = await _unitOfWork.Merchants.GetByApiKeyAsync(providedApiKey);
            if (merchant == null || merchant.Id == Guid.Empty)
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            if (merchant.Status != MerchantStatus.Active)
            {
                return AuthenticateResult.Fail("Merchant is not active");
            }

            parsedMerchantId = merchant.Id;

            try
            {
                await _redisService.SetAsync(cacheKey, parsedMerchantId.ToString(), ApiKeyCacheTtl);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to cache API key lookup result");
            }
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, parsedMerchantId.ToString()),
            new Claim("ApiKey", providedApiKey),
            new Claim(ClaimTypes.Role, "Merchant")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

public static class AuthenticationExtensions
{
    private const string ApiKeySchemeName = "ApiKey";
    private const string GatewayAuthSchemeName = "GatewayAuth";
    private const string ApiKeyHeaderName = "X-API-Key";

    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthentikOptions>(
            configuration.GetSection(AuthentikOptions.SectionName));

        var authentikOptions = configuration
            .GetSection(AuthentikOptions.SectionName)
            .Get<AuthentikOptions>() ?? new AuthentikOptions();

        var authority = ConfigurationGuard.Require(
            authentikOptions.Authority,
            $"{AuthentikOptions.SectionName}:{nameof(AuthentikOptions.Authority)}");
        var audience = ConfigurationGuard.Require(
            authentikOptions.Audience,
            $"{AuthentikOptions.SectionName}:{nameof(AuthentikOptions.Audience)}");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = GatewayAuthSchemeName;
                options.DefaultChallengeScheme = GatewayAuthSchemeName;
                options.DefaultScheme = GatewayAuthSchemeName;
            })
            .AddPolicyScheme(GatewayAuthSchemeName, GatewayAuthSchemeName, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // Prefer API Key when present (avoids JWKS/TLS issues with self-signed certs)
                    if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out StringValues apiKeyHeader) &&
                        !StringValues.IsNullOrEmpty(apiKeyHeader))
                    {
                        return ApiKeySchemeName;
                    }

                    var authorizationHeader = context.Request.Headers.Authorization.ToString();
                    if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
                        authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeySchemeName, _ => { })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = authority.TrimEnd('/');
                options.RequireHttpsMetadata = authentikOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = BuildTokenValidationParameters(authentikOptions, audience);

                // Allow self-signed certificates when HTTPS metadata validation is disabled
                if (!authentikOptions.RequireHttpsMetadata)
                {
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                }

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuth");
                        logger.LogError(context.Exception, "JWT authentication failed: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is not ClaimsIdentity identity)
                            return Task.CompletedTask;

                        AddRoleClaims(identity, authentikOptions.RoleClaims, authentikOptions.AdminRoles);
                        AddMerchantIdClaim(identity, authentikOptions.MerchantIdClaim);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static TokenValidationParameters BuildTokenValidationParameters(AuthentikOptions options, string audience)
    {
        var allAudiences = options.ValidAudiences
            .Concat(new[] { audience })
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var parameters = new TokenValidationParameters
        {
            ValidateAudience = allAudiences.Length > 0,
            ValidAudience = audience,
            ValidAudiences = allAudiences,
            ValidateIssuer = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };

        if (!string.IsNullOrWhiteSpace(options.ValidIssuer))
        {
            parameters.ValidIssuer = options.ValidIssuer;
        }

        return parameters;
    }

    private static void AddRoleClaims(
        ClaimsIdentity identity,
        IEnumerable<string> roleClaimTypes,
        IEnumerable<string> adminRoleNames)
    {
        var existingRoles = identity.FindAll(ClaimTypes.Role)
            .SelectMany(c => ExpandClaimValues(c.Value))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var adminAliases = adminRoleNames
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sourceClaimTypes = roleClaimTypes
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Concat(new[] { ClaimTypes.Role })
            .Distinct(StringComparer.Ordinal);

        foreach (var claimType in sourceClaimTypes)
        {
            var claims = identity.FindAll(claimType);
            foreach (var claim in claims)
            {
                foreach (var role in ExpandClaimValues(claim.Value))
                {
                    if (string.IsNullOrWhiteSpace(role))
                        continue;

                    if (existingRoles.Add(role))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }

                    if (adminAliases.Contains(role) && existingRoles.Add("admin"))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, "admin"));
                    }
                }
            }
        }
    }

    private static void AddMerchantIdClaim(ClaimsIdentity identity, string merchantIdClaimType)
    {
        if (identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier && Guid.TryParse(c.Value, out _)))
            return;

        var candidateClaims = new[]
            {
                merchantIdClaimType,
                "merchant_id",
                "merchantId",
                "sub"
            }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal);

        foreach (var claimType in candidateClaims)
        {
            var claim = identity.FindFirst(claimType);
            if (claim == null)
                continue;

            if (Guid.TryParse(claim.Value, out var merchantId) && merchantId != Guid.Empty)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, merchantId.ToString()));
                return;
            }
        }
    }

    private static IEnumerable<string> ExpandClaimValues(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            yield break;

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            List<string>? values = null;
            try
            {
                values = JsonSerializer.Deserialize<List<string>>(trimmed);
            }
            catch
            {
                values = null;
            }

            if (values != null)
            {
                foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
                    yield return value.Trim();
                yield break;
            }
        }

        foreach (var value in trimmed.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            yield return value.Trim();
    }
}
