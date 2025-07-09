using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace CijeneScraper.Services.Security
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var enabled = _configuration.GetValue<bool>("ApiKey:Enabled");
            if (!enabled)
            {
                // Skip authentication, allow all requests
                var anonymousClaims = new[] { new Claim(ClaimTypes.Name, "Anonymous") };
                var anonymousIdentity = new ClaimsIdentity(anonymousClaims, Options.AuthenticationType);
                var anonymousPrincipal = new ClaimsPrincipal(anonymousIdentity);
                var anonymousTicket = new AuthenticationTicket(anonymousPrincipal, ApiKeyAuthenticationOptions.DefaultScheme);
                return Task.FromResult(AuthenticateResult.Success(anonymousTicket));
            }

            // Get the API key from the request headers
            if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
            {
                return Task.FromResult(AuthenticateResult.Fail("API Key is missing"));
            }

            // Get the expected API key from configuration
            var expectedApiKey = _configuration["ApiKey:MobileApp"];
            if (string.IsNullOrEmpty(expectedApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("API Key is not configured"));
            }

            // Check if the provided API key matches the expected one
            var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
            if (!string.Equals(expectedApiKey, providedApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
            }

            // Create authenticated user
            var claims = new[] { new Claim(ClaimTypes.Name, "MobileApp") };
            var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "ApiKey";
        public string AuthenticationType = DefaultScheme;
    }

    public static class ApiKeyAuthenticationExtensions
    {
        public static AuthenticationBuilder AddApiKeyAuthentication(
            this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationOptions.DefaultScheme, null);
        }
    }
}