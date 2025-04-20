using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Gases.Components;

/// <summary>
/// A component for items that consume gas when they are active.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GasConsumptionComponent : Component
{
    [DataField]
    public float MoleUsage = 0.012f;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleGasConsumption";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;


}
