using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.FloofStation.GameTicking;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Components;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Psionics.Glimmer;
using Content.Shared.Psionics.Glimmer;
namespace Content.Server.StationEvents;

public sealed class EventManagerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] public readonly GameTicker GameTicker = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!; //Nyano - Summary: pulls in the glimmer system.

    public bool EventsEnabled { get; private set; }
    private void SetEnabled(bool value) => EventsEnabled = value;

    private StationEventCondition.Dependencies _eventConditionDeps = default!; // Floof

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configurationManager, CCVars.EventsEnabled, SetEnabled, true);

        _eventConditionDeps = new(EntityManager, GameTicker, this); // Floof
        _eventConditionDeps.Initialize();
    }

    /// <summary>
    /// Randomly runs a valid event.
    /// </summary>
    public string RunRandomEvent()
    {
        var randomEvent = PickRandomEvent();

        if (randomEvent == null)
        {
            var errStr = Loc.GetString("station-event-system-run-random-event-no-valid-events");
            Log.Error(errStr);
            return errStr;
        }

        var ent = GameTicker.AddGameRule(randomEvent);
        var str = Loc.GetString("station-event-system-run-event",("eventName", ToPrettyString(ent)));
        _chat.SendAdminAlert(str);
        Log.Info(str);
        return str;
    }

    /// <summary>
    /// Randomly picks a valid event.
    /// </summary>
    public string? PickRandomEvent()
    {
        var availableEvents = AvailableEvents();
        Log.Info($"Picking from {availableEvents.Count} total available events");
        return FindEvent(availableEvents);
    }

    /// <summary>
    /// Pick a random event from the available events at this time, also considering their weightings.
    /// </summary>
    /// <returns></returns>
    public string? FindEvent(Dictionary<EntityPrototype, StationEventComponent> availableEvents)
    {
        if (availableEvents.Count == 0)
        {
            Log.Warning("No events were available to run!");
            return null;
        }

        var sumOfWeights = 0;

        foreach (var stationEvent in availableEvents.Values)
        {
            sumOfWeights += (int) stationEvent.Weight;
        }

        sumOfWeights = _random.Next(sumOfWeights);

        foreach (var (proto, stationEvent) in availableEvents)
        {
            sumOfWeights -= (int) stationEvent.Weight;

            if (sumOfWeights <= 0)
            {
                return proto.ID;
            }
        }

        Log.Error("Event was not found after weighted pick process!");
        return null;
    }

    /// <summary>
    /// Gets the events that have met their player count, time-until start, etc.
    /// </summary>
    /// <param name="playerCountOverride">Override for player count, if using this to simulate events rather than in an actual round.</param>
    /// <param name="currentTimeOverride">Override for round time, if using this to simulate events rather than in an actual round.</param>
    /// <returns></returns>
    public Dictionary<EntityPrototype, StationEventComponent> AvailableEvents(
        bool ignoreEarliestStart = false,
        int? playerCountOverride = null,
        TimeSpan? currentTimeOverride = null)
    {
        var playerCount = playerCountOverride ?? _playerManager.PlayerCount;

        // playerCount does a lock so we'll just keep the variable here
        var currentTime = currentTimeOverride ?? (!ignoreEarliestStart
            ? GameTicker.RoundDuration()
            : TimeSpan.Zero);

        var result = new Dictionary<EntityPrototype, StationEventComponent>();

        _eventConditionDeps.Update(); // Floof

        foreach (var (proto, stationEvent) in AllEvents())
        {
            if (CanRun(proto, stationEvent, playerCount, currentTime))
            {
                result.Add(proto, stationEvent);
            }
        }

        return result;
    }

    public Dictionary<EntityPrototype, StationEventComponent> AllEvents()
    {
        var allEvents = new Dictionary<EntityPrototype, StationEventComponent>();
        foreach (var prototype in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract)
                continue;

            if (!prototype.TryGetComponent<StationEventComponent>(out var stationEvent))
                continue;

            allEvents.Add(prototype, stationEvent);
        }

        return allEvents;
    }

    private int GetOccurrences(EntityPrototype stationEvent)
    {
        return GetOccurrences(stationEvent.ID);
    }

    private int GetOccurrences(string stationEvent)
    {
        return GameTicker.AllPreviousGameRules.Count(p => p.Item2 == stationEvent);
    }

    public TimeSpan TimeSinceLastEvent(EntityPrototype stationEvent)
    {
        foreach (var (time, rule) in GameTicker.AllPreviousGameRules.Reverse())
        {
            if (rule == stationEvent.ID)
                return time;
        }

        return TimeSpan.Zero;
    }

    private bool CanRun(EntityPrototype prototype, StationEventComponent stationEvent, int playerCount, TimeSpan currentTime)
    {
        if (GameTicker.IsGameRuleAdded(prototype.ID))
            return false;

        if (stationEvent.MaxOccurrences.HasValue && GetOccurrences(prototype) >= stationEvent.MaxOccurrences.Value)
        {
            return false;
        }

        if (playerCount < stationEvent.MinimumPlayers)
        {
            return false;
        }

        if (currentTime != TimeSpan.Zero && currentTime.TotalMinutes < stationEvent.EarliestStart)
        {
            return false;
        }

        var lastRun = TimeSinceLastEvent(prototype);
        if (lastRun != TimeSpan.Zero && currentTime.TotalMinutes <
            stationEvent.ReoccurrenceDelay + lastRun.TotalMinutes)
        {
            return false;
        }

        // Nyano - Summary: - Begin modified code block: check for glimmer events.
        // This could not be cleanly done anywhere else.
        if (_configurationManager.GetCVar(CCVars.GlimmerEnabled) &&
            prototype.TryGetComponent<GlimmerEventComponent>(out var glimmerEvent) &&
            (_glimmerSystem.Glimmer < glimmerEvent.MinimumGlimmer ||
            _glimmerSystem.Glimmer > glimmerEvent.MaximumGlimmer))
        {
            return false;
        }
        // Nyano - End modified code block.

        // Floof section - custom conditions
        if (stationEvent.Conditions is { } conditions
            && conditions.Any(it => it.Inverted ^ !it.IsMet(prototype, stationEvent, _eventConditionDeps))
        )
            return false;
        // Floof section end

        return true;
    }
}
