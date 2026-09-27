namespace Sky.Scripting;

/// <summary>One event's Lua module: the id it is fired and reported by, and the source that returns its table.</summary>
/// <param name="Id">The event's content id, lowercase with hyphens, unique among the flight's modules.</param>
/// <param name="Source">
/// The Lua source, a chunk returning a table with <c>id</c>, <c>phases</c>, <c>trigger</c>, <c>describe</c>,
/// <c>choices</c> and <c>effects</c> (<c>docs/design/events.md</c> section 1).
/// </param>
public sealed record EventModule(string Id, string Source);
