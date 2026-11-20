namespace Cloyd.Ides;

/// <summary>
/// What an IDE's local API answers <c>GET /windows</c>: <c>{"windows": [{"project", "title", "window", "desktop"}]}</c>,
/// of which Cloyd reads each window's project and desktop.
/// </summary>
sealed record WindowsAnIdeListed(IReadOnlyList<WindowAnIdeListed> Windows);
