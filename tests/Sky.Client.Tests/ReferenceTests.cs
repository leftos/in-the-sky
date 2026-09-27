using Sky.Client.Scripts;

namespace Sky.Client.Tests;

/// <summary>Pins the project references Sky.Client is allowed, read from the compiled assembly's own metadata.</summary>
public sealed class ReferenceTests
{
    /// <summary>Sky.Client references exactly the Sky projects its row of the dependency table allows.</summary>
    [Fact]
    public void ReferencesOnlyItsAllowedSkyProjects()
    {
        string[] expected = ["Sky.Session"];

        Assert.Equal(expected, SkyReferenceNames());

        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "GodotSharp");
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
