namespace Cloyd.Hub;

/// <summary>
/// The hub's answer that it didn't do what it was asked, such as typing <c>claude</c> into a shell that runs a program,
/// with why, for a human.
/// </summary>
sealed class RefusedByTheHub(string why) : Exception(why);
