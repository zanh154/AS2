using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
namespace AssignmentPRN.Presentation.Security;
public sealed class SessionDocumentActor(IHttpContextAccessor context) : IDocumentActor
{
    public int UserId => context.HttpContext?.Session.GetInt32(SessionKeys.UserId) ?? 0;
    public string? Role => context.HttpContext?.Session.GetString(SessionKeys.Role);
}
