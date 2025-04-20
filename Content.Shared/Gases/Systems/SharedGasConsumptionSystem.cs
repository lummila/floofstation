using Content.Shared.Actions;
using Content.Shared.Gases.Components;
using Content.Shared.Gases.Events;
using Robust.Shared.Containers;


namespace Content.Shared.Gases.Systems;


/// <summary>
/// This handles...
/// </summary>
public abstract class SharedGasConsumptionSystem : EntitySystem
{
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<GasConsumptionComponent, GetItemActionsEvent>(OnJetpackGetAction);
        SubscribeLocalEvent<GasConsumptionComponent, ToggleGasConsumptionEvent>(OnGasConsumptionToggle);
    }

    private void OnGasConsumptionToggle(EntityUid uid, GasConsumptionComponent component, ref ToggleGasConsumptionEvent args)
    {
        if (args.Handled)
            return;

        SetEnabled(uid, component, !IsEnabled(uid));
    }

    private static void OnJetpackGetAction(EntityUid uid, GasConsumptionComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
    }

    private bool IsEnabled(EntityUid uid) => HasComp<ActiveGasConsumptionComponent>(uid);

    protected virtual bool CanEnable(EntityUid uid, GasConsumptionComponent component) => true;

    public void SetEnabled(EntityUid uid, GasConsumptionComponent component, bool enabled)
    {
        if (IsEnabled(uid) == enabled || enabled && !CanEnable(uid, component))
            return;

        if (enabled)
            EnsureComp<ActiveGasConsumptionComponent>(uid);
        else
            RemComp<ActiveGasConsumptionComponent>(uid);
    }
}
