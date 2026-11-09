using System.Text.Json;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SoloStone.Mobile;

public sealed record DeckInfo(string Id, string Name, string Class, string Hero, string[] Cards,
    IReadOnlyDictionary<string, string[]> Sideboards);
public sealed record EntityView(int Id, string Name, string Text, int Cost, int? Attack, int? Health, int Armor);
public sealed record SeatView(string Name, EntityView Hero, EntityView Power, EntityView[] Board,
    EntityView[] Hand, int HandCount, int DeckCount, int Mana, int MaxMana);
public sealed record ActionView(int Id, int Source, int Target, string Type, string Label);
public sealed record DecisionView(long Id, bool Mulligan, ActionView[] Options);
public sealed record MatchView(long Revision, int Turn, bool Complete, string Result, bool BotTurn,
    SeatView Player, SeatView Opponent, DecisionView? Decision);

// One owner/serial queue must call this class. Never send engine objects or private opponent offers to UI.
public sealed class MatchSession
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static IReadOnlyList<DeckInfo> Decks { get; } = LoadDecks();
    public Game? Game { get; private set; }
    public long Revision { get; private set; }

    private static DeckInfo[] LoadDecks()
    {
        using var stream = typeof(MatchSession).Assembly.GetManifestResourceStream("decks.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("decks").EnumerateArray()
            .Where(d => d.GetProperty("admission").GetString() == "admitted")
            .Select(d => new DeckInfo(d.GetProperty("id").GetString()!, d.GetProperty("name").GetString()!,
                d.GetProperty("class").GetString()!, d.GetProperty("hero").GetString()!,
                d.GetProperty("cards").EnumerateArray().SelectMany(c => Enumerable.Repeat(
                    c.GetProperty("id").GetString()!, c.GetProperty("count").GetInt32())).ToArray(),
                d.TryGetProperty("sideboards", out var sides)
                    ? sides.EnumerateArray().ToDictionary(s => s.GetProperty("owner").GetString()!,
                        s => s.GetProperty("cards").EnumerateArray().SelectMany(c => Enumerable.Repeat(
                            c.GetProperty("id").GetString()!, c.GetProperty("count").GetInt32())).ToArray())
                    : new Dictionary<string, string[]>())).ToArray();
    }

    public void Start(string playerDeck, string opponentDeck, long? seed = null)
    {
        var p = Decks.Single(d => d.Id == playerDeck);
        var o = Decks.Single(d => d.Id == opponentDeck);
        var game = new Game(new GameConfig {
            Player1Name = "You", Player2Name = o.Name,
            Player1HeroClass = Enum.Parse<CardClass>(p.Class), Player2HeroClass = Enum.Parse<CardClass>(o.Class),
            Player1HeroCard = Cards.FromId(p.Hero), Player2HeroCard = Cards.FromId(o.Hero),
            Player1Deck = p.Cards.Select(Cards.FromId).ToList(), Player2Deck = o.Cards.Select(Cards.FromId).ToList(),
            Player1Sideboards = p.Sideboards, Player2Sideboards = o.Sideboards,
            FormatType = FormatType.FT_WILD, RandomSeed = seed, Shuffle = true,
            SkipMulligan = false, History = true, Logging = false, EnableCorpses = true
        });
        game.StartGame();
        Game = game;
        Revision++;
    }

    public void Answer(long revision, long decisionId, int[] selections)
    {
        var game = RequireGame();
        if (revision != Revision) throw new InvalidOperationException("The board changed. Select your move again.");
        var decision = game.GetPendingDecisions().SingleOrDefault(d => d.ControllerId == game.Player1.Id && d.Id == decisionId)
            ?? throw new InvalidOperationException("That choice is no longer available.");
        Apply(decision, selections);
    }

    private void Apply(PendingDecision decision, int[] selections)
    {
        var game = RequireGame();
        bool mulligan = decision.Kind == DecisionKind.Mulligan;
        if (!game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, selections), out var error))
            throw new InvalidOperationException(error);
        if (mulligan && game.Player1.MulliganState == Mulligan.DONE && game.Player2.MulliganState == Mulligan.DONE)
            game.MainBegin();
        Revision++;
    }

    // One public-state heuristic action per call so the host can yield, save and update the screen.
    public bool StepBot()
    {
        var game = RequireGame();
        var decision = game.GetPendingDecisions().FirstOrDefault(d => d.ControllerId == game.Player2.Id);
        if (decision == null) return false;
        int[] selected = decision.Kind == DecisionKind.Mulligan
            ? decision.Options.Where(o => game.IdEntityDic[o.EntityId].Card.Cost <= 3).Select(o => o.OptionId).ToArray()
            : new[] { decision.Options.OrderByDescending(Score).First().OptionId };
        Apply(decision, selected);
        return true;
    }

    private double Score(DecisionOption option)
    {
        var game = RequireGame();
        if (option.TaskType == PlayerTaskType.END_TURN) return -10000;
        game.IdEntityDic.TryGetValue(option.EntityId, out var source);
        game.IdEntityDic.TryGetValue(option.TargetId, out var target);
        if (option.TaskType is PlayerTaskType.MINION_ATTACK or PlayerTaskType.HERO_ATTACK)
        {
            var attacker = source as Character;
            var victim = target as Character;
            if (attacker == null || victim == null) return -100;
            if (victim is Hero) return attacker.AttackDamage >= victim.Health + victim.Armor ? 100000 : 10 + attacker.AttackDamage;
            return (attacker.AttackDamage >= victim.Health ? 35 : 5) + victim.AttackDamage
                - (victim.AttackDamage >= attacker.Health ? attacker.Card.Cost * 3 : 0);
        }
        // This intentionally uses only offered moves and visible targets, never the human hand/deck.
        if (target != null && target.Controller == game.Player2 && source?.Card.Type == CardType.SPELL) return -20;
        return 20 + (source?.Card.Cost ?? 0) * 2;
    }

    public MatchView View()
    {
        var g = RequireGame();
        var decisions = g.GetPendingDecisions();
        var human = decisions.FirstOrDefault(d => d.ControllerId == g.Player1.Id);
        return new(Revision, g.Turn, g.State == State.COMPLETE, g.Player1.PlayState.ToString(),
            decisions.Any(d => d.ControllerId == g.Player2.Id) && human == null,
            Seat(g.Player1, true), Seat(g.Player2, false), human == null ? null : new(human.Id,
                human.Kind == DecisionKind.Mulligan, human.Options.Select(o => Project(human, o)).ToArray()));
    }

    private ActionView Project(PendingDecision decision, DecisionOption option)
    {
        var game = RequireGame();
        string Name(int id) => game.IdEntityDic.TryGetValue(id, out var e) ? e.Card.Name : "";
        var label = decision.Kind != DecisionKind.Options ? Name(option.EntityId) :
            option.TaskType == PlayerTaskType.END_TURN ? "End turn" :
            $"{option.TaskType.ToString().Replace('_', ' ')} · {Name(option.EntityId)}";
        if (option.TargetId != 0) label += $" → {Name(option.TargetId)}";
        if (option.ZonePosition >= 0) label += $" · position {option.ZonePosition + 1}";
        if (option.BranchIndex > 0) label += $" · option {option.BranchIndex}";
        return new(option.OptionId, option.EntityId, option.TargetId, option.TaskType.ToString(), label);
    }

    private static EntityView Entity(IEntity e) => new(e.Id, e.Card.Name, e.Card.Text,
        e is IPlayable p ? p.Cost : e.Card.Cost, e is Character c ? c.AttackDamage : null,
        e is Character h ? h.Health : null, e is Character a ? a.Armor : 0);
    private static SeatView Seat(Controller p, bool human) => new(p.Name, Entity(p.Hero), Entity(p.Hero.HeroPower),
        p.BoardZone.Select(Entity).ToArray(), human ? p.HandZone.Select(Entity).ToArray() : [],
        p.HandZone.Count, p.DeckZone.Count, p.RemainingMana, p.BaseMana);
    public byte[] Save() => RequireGame().SaveSnapshot();
    public void Restore(byte[] bytes) { Game = Game.LoadSnapshot(bytes); Revision++; }
    public void Concede() { RequireGame().Process(ConcedeTask.Any(RequireGame().Player1)); Revision++; }
    private Game RequireGame() => Game ?? throw new InvalidOperationException("Start a match first.");
}
