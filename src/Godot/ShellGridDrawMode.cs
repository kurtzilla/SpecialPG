#nullable enable

namespace SpecialPG;

/// <summary>How <see cref="GameRoot"/> draws the shell grid overlay.</summary>
public enum ShellGridDrawMode
{
    /// <summary>Full per-cell grid when zoomed in; chunk borders only when visible span exceeds threshold.</summary>
    Auto,

    /// <summary>Always draw every visible cell edge.</summary>
    Full,

    /// <summary>Only draw chunk-aligned borders (every <see cref="ShellAppConfig.ChunkWidthCells"/> / height).</summary>
    ChunkOnly,
}
