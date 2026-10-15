using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Presentation.ViewModels;

public sealed class ExamStudentSearchViewModel
{
    public ExamStudentSearch Filter { get; init; } = new();
    public ExamStudentSearchResult Result { get; init; } = new();
    public string? Error { get; init; }
}
