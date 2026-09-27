namespace Sky.Engine.Tests;

/// <summary>Pins the project references Sky.Engine is allowed, read from the compiled assembly's own metadata.</summary>
public sealed class ReferenceTests
{
    /// <summary>The engine references no other Sky project, and neither Godot nor a Lua runtime.</summary>
    [Fact]
    public void ReferencesNoOtherProject()
    {
        Assert.Empty(SkyReferenceNames());

        string[] forbidden = ["Godot", "MoonSharp", "NLua", "KeraLua"];
        string[] offending =
        [
            .. typeof(AssemblyMarker)
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .OfType<string>()
                .Where(name => forbidden.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))),
        ];
        Assert.Empty(offending);
    }

    private static string[] SkyReferenceNames() =>
        [
            .. typeof(AssemblyMarker)
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .OfType<string>()
                .Where(name => name.StartsWith("Sky.", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
}
