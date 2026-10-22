using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AssignmentPRN.Presentation.Pages.Questions;
/// <summary>The CSV template the import screen expects.</summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public sealed class TemplateModel : PageModel
{
    // The bytes, not the string: File(string, ...) reads its first argument as a file path.
    public IActionResult OnGet() => File(
        System.Text.Encoding.UTF8.GetBytes(CsvImportRules.Content),
        "text/csv; charset=utf-8",
        "mau-nhap-cau-hoi.csv");
}
