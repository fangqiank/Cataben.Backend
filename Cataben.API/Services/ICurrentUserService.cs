namespace Cataben.API.Services;

/// <summary>
/// Resolves the authenticated request to the application's internal <see cref="Guid"/> user id.
/// Clerk puts the user id (a string such as "user_2AbC…") in the JWT <c>sub</c> claim, while the
/// domain <c>User.Id</c> is a <see cref="Guid"/> linked to Clerk through <c>User.ExternalId</c>.
/// This service bridges that gap so controllers work in terms of the local user id.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Returns the internal user id for the current request, or <c>null</c> when the caller is
    /// anonymous or the Clerk user has no matching row in the database.
    /// </summary>
    Task<Guid?> GetUserIdAsync(CancellationToken cancellationToken = default);
}
