namespace Sky.Content;

/// <summary>
/// A content tree failed to load (R10): a file is missing, unreadable or malformed, a field is unknown or missing, an id is
/// duplicated or unknown, or the Engine refused a value. It names the file, and within a JSON file the path of the value.
/// </summary>
/// <param name="file">The file's path relative to the content root, with forward slashes.</param>
/// <param name="jsonPath">The JSON path of the offending value, or null when the failure is not inside a JSON document.</param>
/// <param name="expected">What was expected and what was found.</param>
/// <param name="innerException">The exception that caused the failure, or null when the loader found it itself.</param>
public sealed class ContentLoadException(string file, string? jsonPath, string expected, Exception? innerException)
    : Exception(Describe(file, jsonPath, expected), innerException)
{
    /// <summary>Gets the file's path relative to the content root, with forward slashes.</summary>
    public string File { get; } = file;

    /// <summary>Gets the JSON path of the offending value, or null when the failure is not inside a JSON document.</summary>
    public string? JsonPath { get; } = jsonPath;

    private static string Describe(string file, string? jsonPath, string expected) =>
        jsonPath is null ? $"{file}: {expected}" : $"{file} at {jsonPath}: {expected}";
}
