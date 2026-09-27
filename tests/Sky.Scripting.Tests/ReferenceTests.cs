namespace Sky.Scripting.Tests;

/// <summary>Pins the project references Sky.Scripting is allowed, read from the compiled assembly's own metadata.</summary>
public sealed class ReferenceTests
{
    /// <summary>Sky.Scripting references exactly the Sky projects its row of the dependency table allows.</summary>
    [Fact]
    public void ReferencesOnlyItsAllowedSkyProjects()
    {
        string[] expected = ["Sky.Engine"];

        Assert.Equal(expected, SkyReferenceNames());
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
