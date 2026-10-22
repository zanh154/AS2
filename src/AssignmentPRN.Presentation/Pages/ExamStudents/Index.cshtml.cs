using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.ExamStudents;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public sealed class IndexModel(IExamSessionService service) : PageModel
{
    public ExamStudentSearchViewModel Search { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync([Bind(Prefix = "")] ExamStudentSearch filter, CancellationToken cancellationToken)
    {
        // Scope is derived only from the authenticated session, never the query string.
        int? lecturerId = HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Admin
            ? null : HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        var response = ModelState.IsValid
            ? await service.SearchExamStudentsAsync(filter, lecturerId, cancellationToken)
            : ServiceResponse<ExamStudentSearchResult>.Fail("Bộ lọc không hợp lệ. Vui lòng kiểm tra ngày, phiên thi và trạng thái.");
        Search = new ExamStudentSearchViewModel
        {
            Filter = filter, Result = response.Data ?? new(), Error = response.Error
        };
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            ? Partial("/Pages/Shared/ExamStudentsResults.cshtml", Search) : Page();
    }
}
