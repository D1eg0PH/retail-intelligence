using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;

namespace RetailIntelligence.Api.Integrations.Eleventa;

public sealed class EleventaApiKeyFilter(AppDbContext db) : IAsyncActionFilter
{
    public const string AuthenticatedSourceItem = "EleventaSourceId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auth = context.HttpContext.Request.Headers.Authorization.ToString();
        var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth[7..].Trim()
            : string.Empty;
        if (token.Length is < 32 or > 512)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var candidates = await db.EleventaSources.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new { s.SourceId, s.CredentialSha256 })
            .ToListAsync(context.HttpContext.RequestAborted);
        var sourceId = candidates.FirstOrDefault(candidate =>
        {
            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    tokenHash, Convert.FromHexString(candidate.CredentialSha256.Trim()));
            }
            catch (FormatException)
            {
                return false;
            }
        })?.SourceId;

        if (sourceId is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        context.HttpContext.Items[AuthenticatedSourceItem] = sourceId.Value;
        await next();
    }
}

public sealed class EleventaBatchConflictException(string message) : Exception(message);
