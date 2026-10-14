using AssignmentPRN.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Components;

public class NotificationViewComponent : ViewComponent
{
    private readonly INotificationService _service;

    public NotificationViewComponent(INotificationService service)
    {
        _service = service;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        int userId = 0;
        if (ViewContext.HttpContext.Session.GetInt32("UserId") is int uid)
        {
            userId = uid;
        }

        int count = await _service.CountUnreadAsync(userId);

        if (count > 0)
        {
            string display = count >= 99 ? "99+" : count.ToString();
            return Content($"<span class=\"badge bg-danger ms-2\">{display}</span>");
        }

        return Content("");
    }
}