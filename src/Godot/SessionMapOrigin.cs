namespace SpecialPG;

/// <summary>How the current <see cref="SpecialPG.Core.Maps.WorldMap"/> was chosen (menu / workbench routing).</summary>
public enum SessionMapOrigin
{
    Unknown,
    JsonLoaded,
    ProceduralWorkbench,
    /// <summary>Procedural world from <c>config.ini</c> cold-start parameters (not workbench apply).</summary>
    ProceduralColdStart,

    /// <summary>Loaded from a persisted <see cref="SpecialPG.Core.Maps.MapSaveEnvelope"/> (generation snapshot for HUD).</summary>
    SaveEnvelopeLoaded,
}
