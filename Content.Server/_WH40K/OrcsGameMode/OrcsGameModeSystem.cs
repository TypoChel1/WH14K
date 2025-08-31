using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Globalization;
using Content.Shared.WH40K.OrcsGameMode;



namespace Content.Server.GameTicking.Rules;

public sealed class OrcRuleSystem : GameRuleSystem<OrcRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrcRoleComponent, GetBriefingEvent>(OnGetBriefing);

    }

    private bool _messageAboutRoundEnd = false;

    private void OnGetBriefing(Entity<OrcRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("orcs-infection-greeting"));
    }

    protected override void AppendRoundEndText(EntityUid uid,
        OrcRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var fraction = GetOrcsFraction(true, true);

        if (fraction <= 0)
            args.AddLine(Loc.GetString("orcs-round-end-amount-none"));
        else if (fraction <= 0.25)
            args.AddLine(Loc.GetString("orcs-round-end-amount-low"));
        else if (fraction <= 0.5)
            args.AddLine(Loc.GetString("orcs-round-end-amount-medium", ("percent", Math.Round((fraction * 100), 2).ToString(CultureInfo.InvariantCulture))));
        else if (fraction < 1)
            args.AddLine(Loc.GetString("orcs-round-end-amount-high", ("percent", Math.Round((fraction * 100), 2).ToString(CultureInfo.InvariantCulture))));
        else
            args.AddLine(Loc.GetString("orcs-round-end-amount-all"));

        var antags = _antag.GetAntagIdentifiers(uid);
        args.AddLine(Loc.GetString("orcs-round-end-initial-count", ("initialCount", antags.Count)));
        foreach (var (_, data, entName) in antags)
        {
            args.AddLine(Loc.GetString("orcs-round-end-user-was-initial",
                ("name", entName),
                ("username", data.UserName)));
        }

        var humanAlive = GetAliveHumans();

        if (humanAlive.Count <= 0 || humanAlive.Count > 2 * antags.Count)
            return;
        args.AddLine("");
        args.AddLine(Loc.GetString("orcs-round-end-survivor-count", ("count", humanAlive.Count)));
        foreach (var survivor in humanAlive)
        {
            var meta = MetaData(survivor);
            var username = string.Empty;
            if (_mindSystem.TryGetMind(survivor, out _, out var mind) &&
                _player.TryGetSessionById(mind.UserId, out var session))
            {
                username = session.Name;
            }

            args.AddLine(Loc.GetString("orcs-round-end-user-was-survivor",
                ("name", meta.EntityName),
                ("username", username)));
        }
    }

    private void CheckRoundEnd(OrcRuleComponent orcRuleComponent)
    {
        var humanAlive = GetAliveHumans();
        if (humanAlive.Count == 1) // Only one human left. spooky
            _popup.PopupEntity(Loc.GetString("orcs-alone"), humanAlive[0], humanAlive[0]);

        var orcCount = 0;
        var query = EntityQueryEnumerator<OrcComponent, MobStateComponent>();
        while (query.MoveNext(out _, out _, out var mob))
        {
            if (mob.CurrentState == MobState.Dead)
                continue;
            orcCount++;
        }

        var orcsLeft = humanAlive.Count / orcCount;

        if (orcsLeft <= 6)

            if (_messageAboutRoundEnd == false)

        _chat.DispatchGlobalAnnouncement(Loc.GetString("У ВАС ЕСТЬ 2 МИНУТЫ ЧТОБЫ УБИТЬ ДРУГ ДРУГА!"), Loc.GetString("Бог-император"), false, null, colorOverride: Color.Crimson);
        _roundEnd.RequestRoundEnd(TimeSpan.FromMinutes(2), null, true, Loc.GetString("Раунд закончится через 2 минуты"), Loc.GetString("Боги"));
        _messageAboutRoundEnd = true;

        if (GetOrcsFraction() <= 0.1) // Oops, 1 orc
            _roundEnd.EndRound();
    }

    protected override void Started(EntityUid uid, OrcRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.NextRoundEndCheck = _timing.CurTime + component.EndCheckDelay;
    }

    protected override void ActiveTick(EntityUid uid, OrcRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (!component.NextRoundEndCheck.HasValue || component.NextRoundEndCheck > _timing.CurTime)
            return;
        CheckRoundEnd(component);
        component.NextRoundEndCheck = _timing.CurTime + component.EndCheckDelay;
    }

    private float GetOrcsFraction(bool includeOffStation = true, bool includeDead = false)
    {
        var humans = GetAliveHumans(includeOffStation);
        var orcCurrentCount = 0;
        var orcAllCount = 0;

        var query = EntityQueryEnumerator<OrcComponent, MobStateComponent>();
        while (query.MoveNext(out _, out _, out var mob))
        {
            if (!includeDead && mob.CurrentState == MobState.Dead)
                continue;
            orcCurrentCount++;
        }

        var allOrcsQuery = EntityQueryEnumerator<OrcComponent, MobStateComponent>();
        while (allOrcsQuery.MoveNext(out _, out _, out var mob))
        {
            if (includeDead && mob.CurrentState == MobState.Dead)
                continue;
            orcAllCount++;
        }


        return orcCurrentCount / (float)(orcAllCount);
    }

    private List<EntityUid> GetAliveHumans(bool includeOffStation = true)
    {
        var humanAlive = new List<EntityUid>();

        var stationGrids = new HashSet<EntityUid>();
        if (!includeOffStation)
        {
            foreach (var station in _station.GetStationsSet())
            {
                if (TryComp<StationDataComponent>(station, out var data) && _station.GetLargestGrid(data) is { } grid)
                    stationGrids.Add(grid);
            }
        }

        var players = AllEntityQuery<HumanoidAppearanceComponent, ActorComponent, MobStateComponent, TransformComponent>();
        var orcs = GetEntityQuery<OrcComponent>();
        while (players.MoveNext(out var uid, out _, out _, out var mob, out var xform))
        {
            if (!_mobState.IsAlive(uid, mob))
                continue;

            if (TryComp<OrcComponent>(uid, out _))
                continue;

            if (!includeOffStation && !stationGrids.Contains(xform.GridUid ?? EntityUid.Invalid))
                continue;

            humanAlive.Add(uid);

        }

        return humanAlive;
    }
}
