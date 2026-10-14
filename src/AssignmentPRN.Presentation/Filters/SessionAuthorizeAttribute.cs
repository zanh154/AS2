using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AssignmentPRN.Presentation.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SessionAuthorizeAttribute(params string[] allowedRoles) : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles = allowedRoles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        if (!session.GetInt32(SessionKeys.UserId).HasValue)
        {
            var request = context.HttpContext.Request;
            var returnUrl = $"{request.PathBase}{request.Path}{request.QueryString}";

            context.Result = new RedirectToPageResult(
                "/Account/Login",
                new { returnUrl });
            return;
        }

        if (_allowedRoles.Length == 0)
        {
            return;
        }

        var sessionRole = session.GetString(SessionKeys.Role);
        var isAuthorized = Array.Exists(
            _allowedRoles,
            role => string.Equals(role, sessionRole, StringComparison.OrdinalIgnoreCase));

        if (!isAuthorized)
        {
            context.Result = new RedirectToPageResult("/Account/AccessDenied");
        }
    }
}
