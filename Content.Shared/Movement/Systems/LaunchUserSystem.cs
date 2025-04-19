using System.Numerics;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;


namespace Content.Shared.Movement.Systems;


/// <summary>
/// This handles <see cref="LaunchUserComponent"/>.
/// Reverse engineered from MeleeThrowOnHitSystem.
/// </summary>
public sealed class LaunchUserSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LaunchUserComponent, UseInHandEvent>(OnLaunchUser);
        SubscribeLocalEvent<LaunchedUserComponent, ComponentStartup>(OnLaunchedStartup);
        SubscribeLocalEvent<LaunchedUserComponent, ComponentShutdown>(OnLaunchedShutdown);
        SubscribeLocalEvent<LaunchedUserComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnLaunchUser(Entity<LaunchUserComponent> ent, ref UseInHandEvent args)
    {
        var (_, comp) = ent;

        // In case the old component still persists.
        RemComp<LaunchedUserComponent>(ent);

        var ev = new LaunchedUserStartEvent(ent);
        RaiseLocalEvent(args.User, ref ev);

        var launchedComp = new LaunchedUserComponent
        {
            Speed = comp.Speed,
            Lifetime = comp.Lifetime,
        };

        // Attach LaunchedUserComponent to the user to send them flying.
        AddComp(args.User, launchedComp);
    }

    private void OnLaunchedStartup(Entity<LaunchedUserComponent> ent, ref ComponentStartup args)
    {
        // Get the PhysicsComponent out of the entity if possible, else quit.
        if (!TryComp<PhysicsComponent>(ent, out var body) ||
            (body.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) == 0x0)
            return;

        // Getting the LaunchedUserComponent from the entity.
        var (_, comp) = ent;

        var dir = body.LinearVelocity.Normalized();

        // Entity is standing still
        if (dir == Vector2.Zero)
        {
            RemCompDeferred(ent, comp);
            return;
        }

        comp.PreviousStatus = body.BodyStatus;
        comp.LaunchedEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.Lifetime);
        _physics.SetBodyStatus(ent, body, BodyStatus.InAir);
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: body);
        _physics.ApplyLinearImpulse(ent, dir * comp.Speed * body.Mass, body: body);

        Dirty(ent, comp);
    }

    private void OnLaunchedShutdown(Entity<LaunchedUserComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<PhysicsComponent>(ent, out var body))
            _physics.SetBodyStatus(ent, body, ent.Comp.PreviousStatus);

        var ev = new LaunchedUserEndEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnStartCollide(Entity<LaunchedUserComponent> ent, ref StartCollideEvent args)
    {
        // Getting the LaunchedUserComponent from the entity.
        var (_, comp) = ent;

        if (!args.OtherFixture.Hard || !args.OtherBody.CanCollide || !args.OurFixture.Hard || !args.OurBody.CanCollide)
            return;



        RemCompDeferred(ent, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LaunchedUserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime > comp.LaunchedEndTime)
                RemCompDeferred(uid, comp);
        }
    }
}
