namespace AssignmentPRN.Presentation.ViewModels;

/// <summary>
/// Declarative table. A view lists its columns and, for each row, only the
/// fields it wants shown — the shared <c>_DataTable</c> partial owns the markup,
/// so spacing, wrapping and the action buttons stay identical on every screen.
/// </summary>
public sealed class DataTableViewModel
{
    public required IReadOnlyList<TableColumn> Columns { get; init; }

    public required IReadOnlyList<IReadOnlyList<TableCell>> Rows { get; init; }
}

/// <param name="Header">Column label. The actions column is normally "Thao tác".</param>
/// <param name="AlignEnd">Right-aligns the header, for the actions column.</param>
public sealed record TableColumn(string Header, bool AlignEnd = false);

public abstract record TableCell;

/// <summary>Plain value.</summary>
public sealed record TextCell(string? Value, bool Muted = false, bool Nowrap = false) : TableCell;

/// <summary>Two lines in one column: the name, then its code or email underneath.</summary>
public sealed record StackCell(string Primary, string? Secondary = null) : TableCell;

/// <summary>A number with an optional unit, e.g. <c>12 sinh viên</c>.</summary>
public sealed record CountCell(int Value, string? Suffix = null) : TableCell;

/// <summary>Status pill. <paramref name="Modifier"/> is a <c>status-chip--*</c> suffix.</summary>
public sealed record ChipCell(string Text, string Modifier = "") : TableCell;

public sealed record ActionsCell(IReadOnlyList<TableAction> Actions) : TableCell;

/// <summary>
/// One icon button in the actions column.
/// </summary>
/// <param name="Label">Tooltip and accessible name — the button shows only the icon.</param>
/// <param name="Icon">Key understood by the <c>_Icon</c> partial.</param>
/// <param name="Action">Target action; <paramref name="Controller"/> defaults to the current one.</param>
/// <param name="Tone">Bootstrap outline tone: secondary, primary or danger.</param>
/// <param name="IsPost">Renders a POST form (with antiforgery) instead of a link.</param>
/// <param name="Confirm">Message for the shared confirm dialog; POST actions only.</param>
public sealed record TableAction(
    string Label,
    string Icon,
    string Action,
    object? RouteValues = null,
    string? Controller = null,
    string Tone = "secondary",
    bool IsPost = false,
    string? Confirm = null);
