using Cataben.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cataben.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CustomAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly UserRole? _minimumRole;
        private readonly bool _allowHigherRoles;

        public CustomAuthorizeAttribute() { }

        public CustomAuthorizeAttribute(UserRole minimumRole, bool allowHigherRoles = true)
        {
            _minimumRole = minimumRole;
            _allowHigherRoles = allowHigherRoles;
        }
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (!_minimumRole.HasValue)
                return;

            var roleClaim = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if(string.IsNullOrEmpty(roleClaim) || !Enum.TryParse<UserRole>(roleClaim, out var userRole))
            {
                context.Result = new ForbidResult();
                return;
            }

            var hasRequiredRole = _allowHigherRoles
                ? (int)userRole >= (int)_minimumRole.Value
                : userRole == _minimumRole.Value;

            if(!hasRequiredRole) 
                context.Result = new ForbidResult();
        }
    }
}
