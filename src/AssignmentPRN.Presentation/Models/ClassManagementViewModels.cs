using AssignmentPRN.Business;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.Models;

public sealed class ClassListViewModel
{
    public IReadOnlyList<AcademicClassResponse> Classes { get; init; } = Array.Empty<AcademicClassResponse>();

    public string Query { get; init; } = string.Empty;

    public string? Error { get; init; }
}

public sealed class ClassDetailViewModel
{
    public AcademicClassResponse Class { get; init; } = new();

    public ClassRosterResponse Roster { get; init; } = new();

    [StringLength(255)]
    public string Query { get; init; } = string.Empty;

    public IReadOnlyList<PersonResponse> Matches { get; init; } = Array.Empty<PersonResponse>();
}
