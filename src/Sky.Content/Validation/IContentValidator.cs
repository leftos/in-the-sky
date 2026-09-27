namespace Sky.Content.Validation;

/// <summary>
/// Checks what the loader leaves out of a loaded content tree: references across files and value ranges. A validator stops at
/// the first failure with a <see cref="ContentLoadException"/> naming the content file relative to the root, the JSON path of
/// the offending value and what was expected (R10).
/// </summary>
public interface IContentValidator
{
    /// <summary>Checks the content.</summary>
    /// <param name="content">The loaded content.</param>
    /// <exception cref="ContentLoadException">A value is out of range or a reference does not resolve.</exception>
    void Validate(ContentSet content);
}
