#nullable enable

namespace SpecialPG;

/// <summary>Read-only discrete WASD rates used by shell movement (runtime-tunable via <c>GameRoot</c>).</summary>
public interface IWasdMovementRates
{
    float StepsPerSecond { get; }

    int MaxSubStepsPerPhysicsFrame { get; }
}
