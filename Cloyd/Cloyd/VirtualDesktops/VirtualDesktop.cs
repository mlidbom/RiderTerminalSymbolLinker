namespace Cloyd.VirtualDesktops;

/// <summary>
/// A virtual desktop of Windows, by the GUID Windows identifies it with, which stays the same across restarts: as an IDE's
/// local API names the desktop each of its windows is on.
/// </summary>
readonly record struct VirtualDesktop(Guid Id)
{
   public override string ToString() => Id.ToString();
}
