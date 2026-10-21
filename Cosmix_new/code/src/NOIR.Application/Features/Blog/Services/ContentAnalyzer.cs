namespace NOIR.Application.Features.Blog.Services;

/// <summary>
/// Analyzes blog post HTML content to detect features that require
/// specialized frontend renderers (syntax highlighting, math, diagrams, etc.).
/// </summary>
public interface IContentAnalyzer
{
    /// <summary>
    /// Analyzes HTML content and returns metadata about detected features.
    /// </summary>
    ContentMetadata Analyze(string? htmlContent);
}

/// <summary>
/// Regex-based content analyzer for detecting HTML content features.
/// Registered as transient since it holds no state.
/// </summary>
public sealed class ContentAnalyzer : IContentAnalyzer, ITransientService
{
    private static readonly Regex InlineMathRegex = new(
        @"(?<!\$)\$(?!\$)(?:[^$\\]|\\.)+?\$(?!\$)",
        RegexOptions.Compiled);

    public ContentMetadata Analyze(string? htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return new ContentMetadata();

        return new ContentMetadata
        {
            HasCodeBlocks = DetectCodeBlocks(htmlContent),
            HasMathFormulas = DetectMathFormulas(htmlContent),
            HasMermaidDiagrams = DetectMermaidDiagrams(htmlContent),
            HasTables = DetectTables(htmlContent),
            HasEmbeddedMedia = DetectEmbeddedMedia(htmlContent),
        };
    }

    /// <summary>
    /// Detects &lt;pre&gt;&lt;code&gt; blocks (syntax-highlighted code).
    /// </summary>
    private static bool DetectCodeBlocks(string html) =>
        html.Contains("<pre", StringComparison.OrdinalIgnoreCase) &&
        html.Contains("<code", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Detects LaTeX math: block ($$...$$) or inline ($...$).
    /// </summary>
    private static bool DetectMathFormulas(string html) =>
        html.Contains("$$", StringComparison.Ordinal) ||
        InlineMathRegex.IsMatch(html);

    /// <summary>
    /// Detects Mermaid diagram blocks (class="language-mermaid").
    /// </summary>
    private static bool DetectMermaidDiagrams(string html) =>
        html.Contains("language-mermaid", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Detects HTML tables.
    /// </summary>
    private static bool DetectTables(string html) =>
        html.Contains("<table", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Detects embedded media elements (iframe, video, audio, embed).
    /// </summary>
    private static bool DetectEmbeddedMedia(string html) =>
        html.Contains("<iframe", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("<video", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("<audio", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("<embed", StringComparison.OrdinalIgnoreCase);
}
