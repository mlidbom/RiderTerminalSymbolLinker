namespace Cloyd.Links.Files;

/// <summary>
/// Where in a file a <see cref="FileReference"/> points, as the text after its path says: the line <see cref="Line"/>, or
/// the lines from it to <see cref="LastLine"/>, and a column. Counted from 1, as compilers and editors count them.
/// </summary>
sealed record PlaceInAFile(int Line, int? LastLine, int? Column);
