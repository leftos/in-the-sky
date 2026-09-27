namespace Sky.Sim;

/// <summary>Names this assembly, and holds one use of each project it references so the compiler keeps every reference.</summary>
public static class AssemblyMarker
{
    /// <summary>One type from each project this one references.</summary>
    public static readonly Type[] Dependencies = [typeof(Sky.Session.AssemblyMarker)];

    /// <summary>The simulator's entry point, which runs nothing.</summary>
    private static void Main() { }
}
