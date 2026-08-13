using System.Security.Claims;

namespace Cataben.API.Services;

public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    IUserRepository userRepository) : ICurrentUserService
{
    private const string CacheKey = "__current_user_id";

    public async Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        // Resolve once per request (several endpoints call this in a single handler).
        if (httpContext.Items.TryGetValue(CacheKey, out var cached) && cached is Resolved resolved)
            return resolved.Value;

        // Clerk issues the user id as a string (e.g. "user_2AbC…") in the JWT "sub" claim, NOT a
        // Guid. Map it to the local Guid user id via the ExternalId link.
        var externalId = httpContext.User.FindFirst("sub")?.Value
                         ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Guid? userId = null;
        if (!string.IsNullOrEmpty(externalId))
        {
            var user = await userRepository.GetByExternalIdAsync(externalId, cancellationToken);
            userId = user?.Id;
        }

        httpContext.Items[CacheKey] = new Resolved(userId);
        return userId;
    }

    private sealed record Resolved(Guid? Value);
}
