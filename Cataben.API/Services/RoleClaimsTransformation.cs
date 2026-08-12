using System.Security.Claims;
using Cataben.Application.Repositories;
using Microsoft.AspNetCore.Authentication;

namespace Cataben.API.Services;

// Injects the local User.Role into the authenticated principal as a ClaimTypes.Role claim.
// Clerk's JWT only carries the user id in "sub"; it knows nothing about our internal UserRole.
// CustomAuthorizeAttribute reads ClaimTypes.Role and was effectively dead code (every authenticated
// request hit the empty-claim branch and was forbidden) until this transformation adds the claim.
// Runs once per ClaimsPrincipal construction (i.e. once per authenticated request).
public sealed class RoleClaimsTransformation(IUserRepository userRepository) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        // Guard against double-injection if TransformAsync runs more than once.
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role))
            return principal;

        // Clerk puts the user id in "sub"; MapInboundClaims=false keeps it there.
        var externalId = identity.FindFirst("sub")?.Value
                         ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(externalId))
            return principal;

        var user = await userRepository.GetByExternalIdAsync(externalId);
        if (user is null)
            return principal;

        // Value MUST be the enum NAME ("Admin") — CustomAuthorizeAttribute parses it with
        // Enum.TryParse<UserRole>, which accepts names, not ordinal ints.
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        return principal;
    }
}
