namespace AssignmentPRN.Presentation.ViewModels;

/// <summary>
/// One hop in the breadcrumb trail. The last item is the current page and is
/// rendered as plain text, so it needs no target page.
/// </summary>
/// <param name="Text">Label shown to the user.</param>
/// <param name="Page">Target Razor Page path, e.g. <c>/Classes/Index</c>; null makes the item non-clickable.</param>
/// <param name="RouteValues">Extra route values, e.g. <c>new { id = 4 }</c>.</param>
public sealed record BreadcrumbItem(
    string Text,
    string? Page = null,
    object? RouteValues = null);
