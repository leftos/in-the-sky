namespace Sky.Scripting;

/// <summary>One activity's Lua module: the id it is scored and reported by, and the source that returns its table.</summary>
/// <param name="Id">The activity's module id, unique among the flight's activities, and its content name.</param>
/// <param name="Source">The Lua source, a chunk returning a table with a <c>utility</c> function.</param>
public sealed record ActivityModule(string Id, string Source);
