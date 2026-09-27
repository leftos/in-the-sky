namespace Sky.Engine.Needs;

/// <summary>The kind of source a rate modifier comes from; factors of one class multiply, classes add.</summary>
public enum SourceClass
{
    /// <summary>A passenger's own trait.</summary>
    Trait,

    /// <summary>The passenger's surroundings, such as their seat or neighbours.</summary>
    Context,

    /// <summary>Another need driving this one.</summary>
    Cascade,

    /// <summary>A crew service the player scheduled.</summary>
    Service,

    /// <summary>A cabin event in play.</summary>
    Event,

    /// <summary>The flight phase.</summary>
    Phase,
}
