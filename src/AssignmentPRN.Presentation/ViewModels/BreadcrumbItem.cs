namespace AssignmentPRN.Presentation.ViewModels;

/// <summary>
/// One hop in the breadcrumb trail. The last item is the current page and is
/// rendered as plain text, so it needs no controller or action.
/// </summary>
/// <param name="Text">Label shown to the user.</param>
/// <param name="Controller">Target controller; null makes the item non-clickable.</param>
/// <param name="Action">Target action.</param>
/// <param name="RouteValues">Extra route values, e.g. <c>new { id = 4 }</c>.</param>
public sealed record BreadcrumbItem(
    string Text,
    string? Controller = null,
    string? Action = null,
    object? RouteValues = null);
