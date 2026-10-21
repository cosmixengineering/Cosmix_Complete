namespace NOIR.Domain.ValueObjects;

/// <summary>
/// Metadata about content features detected during save.
/// Allows frontend to conditionally load heavy renderers (syntax highlighting, math, diagrams).
/// </summary>
public sealed record ContentMetadata
{
    /// <summary>
    /// Content contains &lt;pre&gt;&lt;code&gt; blocks requiring syntax highlighting.
    /// </summary>
    public bool HasCodeBlocks { get; init; }

    /// <summary>
    /// Content contains LaTeX math formulas ($...$ or $$...$$).
    /// </summary>
    public bool HasMathFormulas { get; init; }

    /// <summary>
    /// Content contains Mermaid diagram blocks (language-mermaid).
    /// </summary>
    public bool HasMermaidDiagrams { get; init; }

    /// <summary>
    /// Content contains HTML tables.
    /// </summary>
    public bool HasTables { get; init; }

    /// <summary>
    /// Content contains embedded media (iframe, video, audio, embed).
    /// </summary>
    public bool HasEmbeddedMedia { get; init; }
}
