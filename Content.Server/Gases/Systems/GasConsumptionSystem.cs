using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Gases.Components;
using Content.Shared.Gases.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;


namespace Content.Server.Gases.Systems;

/// <summary>
/// This handles serverside consumption of gas in enabled items with the
/// GasConsumptionComponent.
/// </summary>
public sealed class GasConsumptionSystem : SharedGasConsumptionSystem
{
    [Dependency] private readonly GasTankSystem _gasTank = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Is the component able to be enabled, does uid have GasTankComponent and does
    /// that gas tank have more or the same amount of moles in it as the component's
    /// mole usage?
    /// </summary>
    protected override bool CanEnable(EntityUid uid, GasConsumptionComponent component)
    {
        return base.CanEnable(uid, component) &&
            TryComp<GasTankComponent>(uid, out var gasTank) &&
            gasTank.Air.TotalMoles >= component.MoleUsage;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toDisable = new ValueList<(EntityUid Uid, GasConsumptionComponent Component)>();
        var query = EntityQueryEnumerator<ActiveGasConsumptionComponent, GasConsumptionComponent, GasTankComponent>();

        while (query.MoveNext(out var uid, out var active, out var comp, out var gasTankComp))
        {
            var gasTank = (uid, gasTankComp);
            var usedAir = _gasTank.RemoveAir(gasTank, comp.MoleUsage);

            if (usedAir == null)
                continue;

            var usedEnoughAir =
                MathHelper.CloseTo(usedAir.TotalMoles, comp.MoleUsage, comp.MoleUsage/100);

            if (!usedEnoughAir)
            {
                toDisable.Add((uid, comp));
            }

            _gasTank.UpdateUserInterface(gasTank);
        }

        foreach (var (uid, comp) in toDisable)
        {
            SetEnabled(uid, comp, false);
        }
    }
}
