using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Account;

public sealed class LoginModel(IAuthService authService) : PageModel
{
    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet(string? returnUrl)
    {
        if (HttpContext.Session.GetInt32(SessionKeys.UserId).HasValue)
        {
            return RedirectToRoleHome(HttpContext.Session.GetString(SessionKeys.Role));
        }

        Input.ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await authService.LoginAsync(Input.Email, Input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return Page();
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
        HttpContext.Session.SetString(SessionKeys.Email, user.Email);
        HttpContext.Session.SetString(SessionKeys.Role, user.RoleName);

        if (!string.IsNullOrWhiteSpace(Input.ReturnUrl) && Url.IsLocalUrl(Input.ReturnUrl))
        {
            return LocalRedirect(Input.ReturnUrl);
        }

        return RedirectToRoleHome(user.RoleName);
    }

    private IActionResult RedirectToRoleHome(string? role) => role switch
    {
        RoleNames.Admin => RedirectToAction("Index", "Admin"),
        RoleNames.Lecturer => RedirectToAction("Index", "Lecturer"),
        RoleNames.Student => RedirectToAction("Index", "Student"),
        _ => RedirectToPage("/Account/AccessDenied")
    };
}
