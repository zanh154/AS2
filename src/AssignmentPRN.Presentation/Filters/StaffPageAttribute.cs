using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace AssignmentPRN.Presentation.Filters;
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class StaffPageAttribute : Attribute, IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        if (session.GetInt32(SessionKeys.UserId) is not > 0)
        {
            context.Result = new RedirectToPageResult("/Account/Login", new { returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString });
            return;
        }
        if (session.GetString(SessionKeys.Role) is not (RoleNames.Admin or RoleNames.Lecturer))
        {
            context.Result = new StatusCodeResult(403);
            return;
        }
        await next();
    }
}
