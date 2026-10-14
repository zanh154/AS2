using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Account;

public sealed class RegisterModel(IAuthService authService) : PageModel
{
    [BindProperty]
    public RegisterViewModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authService.RegisterAsync(Input.FullName, Input.Email, Input.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể đăng ký tài khoản.");
            return Page();
        }

        TempData["SuccessMessage"] = "Đăng ký thành công. Vui lòng đăng nhập.";
        return RedirectToPage("/Account/Login");
    }
}
