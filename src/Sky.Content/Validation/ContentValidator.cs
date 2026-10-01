namespace Sky.Content.Validation;

/// <summary>
/// Runs every content validator in a fixed order: the layouts, then the needs, then the crew, then the scenarios, then the
/// thought catalogue.
/// </summary>
public static class ContentValidator
{
    private static readonly IContentValidator[] Validators =
    [
        new LayoutValidator(),
        new NeedsValidator(),
        new CrewValidator(),
        new ScenarioValidator(),
        new ThoughtCatalogueValidator(),
    ];

    /// <summary>Checks the content with every validator, stopping at the first failure.</summary>
    /// <param name="content">The loaded content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ContentLoadException">A value is out of range or a reference does not resolve.</exception>
    public static void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (IContentValidator validator in Validators)
        {
            validator.Validate(content);
        }
    }
}
