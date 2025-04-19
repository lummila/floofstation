using System.Numerics;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Movement.Components;

/// <summary>
/// This is used for launching the user in the direction they are facing.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(LaunchUserSystem))]
[AutoGenerateComponentState]
public sealed partial class LaunchUserComponent : Component
{
    /// <summary>
    /// The speed at which hit entities should be launched.
    /// </summary>
    [DataField("speed"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float Speed = 10f;

    /// <summary>
    /// How long hit entities remain in motion, max.
    /// </summary>
    [DataField("lifetime"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float Lifetime = 0.25f;
}

/// <summary>
/// This is attached to the entity that has been launched. See <see cref="LaunchUserComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(LaunchUserSystem))]
[AutoGenerateComponentState]
public sealed partial class LaunchedUserComponent : Component
{
    /// <summary>
    /// The velocity of the launch.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float Speed;

    /// <summary>
    /// How long the launch will last.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float Lifetime;

    /// <summary>
    /// At what point in time will the launch be complete?
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan LaunchedEndTime;

    /// <summary>
    /// The status to which the entity will return when the thrown ends.
    /// </summary>
    [DataField]
    public BodyStatus PreviousStatus;
}

[ByRefEvent]
public record struct LaunchedUserStartEvent(EntityUid User);

[ByRefEvent]
public record struct LaunchedUserEndEvent();
