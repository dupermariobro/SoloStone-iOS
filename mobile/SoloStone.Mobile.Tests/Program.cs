using System.Text.Json;
using SoloStone.Mobile;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
void Reject(Action action, string message) {
    try { action(); } catch (InvalidOperationException) { assertions++; return; }
    throw new Exception(message);
}

var decks = MatchSession.Decks;
Check(decks.Count > 0, "No admitted decks");
// Upstream includes the admitted 20-card Thief Priest list; preserve it verbatim.
Check(decks.All(d => d.Cards.Length is 20 or 30 or 40), "Unexpected upstream deck size");
var match = new MatchSession();
match.Start(decks[0].Id, decks[1].Id, 42);
var opening = match.View();
Check(opening.Decision?.Mulligan == true, "Mulligan must be offered");
Check(opening.Opponent.Hand.Length == 0 && opening.Opponent.HandCount > 0, "Opponent hand leaked");
var snapshot = match.Save();
Reject(() => match.Answer(opening.Revision - 1, opening.Decision!.Id, []), "Stale revision accepted");
Reject(() => match.Answer(opening.Revision, opening.Decision!.Id, [int.MaxValue]), "Unknown mulligan accepted");
Check(snapshot.SequenceEqual(match.Save()), "Rejected input changed game");
match.Answer(opening.Revision, opening.Decision!.Id, opening.Decision.Options.Select(o => o.Id).ToArray());
match.StepBot();
int moves = 0;
while (!match.View().Complete && moves++ < 1200)
{
    var view = match.View();
    if (view.Decision is { } decision)
    {
        // Prefer a non-end-turn move, but bound actions within a turn for repeatable coverage.
        var action = decision.Options.FirstOrDefault(o => o.Type != "END_TURN") ?? decision.Options.First();
        match.Answer(view.Revision, decision.Id, decision.Mulligan ? [] : [action.Id]);
        Reject(() => match.Answer(view.Revision, decision.Id, [action.Id]), "Duplicate tap accepted");
    }
    else Check(match.StepBot(), "Match stalled with no decision");
    if (moves % 25 == 0)
    {
        var before = match.View();
        var saved = match.Save();
        var restored = new MatchSession(); restored.Restore(saved);
        Check(JsonSerializer.Serialize(before with { Revision = restored.Revision }, MatchSession.Json)
            == JsonSerializer.Serialize(restored.View(), MatchSession.Json), "Snapshot changed visible state or decisions");
    }
}
Check(match.View().Complete, "Autoplay did not finish");
Console.WriteLine($"Completed offline match in {moves} moves.");

foreach (var deck in decks)
{
    var session = new MatchSession(); session.Start(deck.Id, decks[0].Id, 17);
    var view = session.View();
    Check(view.Decision?.Options.Length > 0, $"No opening choices for {deck.Id}");
    session.Answer(view.Revision, view.Decision!.Id, []);
    session.StepBot();
    Check(session.View().Turn > 0, $"Mulligan did not finish for {deck.Id}");
    session.Restore(session.Save());
}

var saveDir = Path.Combine(AppContext.BaseDirectory, "test-save-" + Guid.NewGuid());
var host = new MobileHost(saveDir);
var reply = JsonDocument.Parse(host.Handle(JsonSerializer.Serialize(new { id = 1, command = "start", player = decks[0].Id, opponent = decks[1].Id })));
Check(reply.RootElement.GetProperty("error").ValueKind == JsonValueKind.Null, "Host start failed");
Check(reply.RootElement.GetProperty("warning").ValueKind == JsonValueKind.Null,
    "Host autosave failed: " + reply.RootElement.GetProperty("warning"));
Check(reply.RootElement.GetProperty("canResume").GetBoolean(), "Host did not save");
var freshHost = new MobileHost(saveDir);
var resumed = JsonDocument.Parse(freshHost.Handle("{\"id\":2,\"command\":\"resume\"}"));
Check(resumed.RootElement.GetProperty("error").ValueKind == JsonValueKind.Null, "Cold resume failed");
// Remove only the exact files this test created in its unique temporary folder.
File.Delete(Path.Combine(saveDir, "match.bin")); Directory.Delete(saveDir);
Console.WriteLine($"PASS: {assertions} assertions; {decks.Count} deck openings; save/load; input validation; hidden hand; full match.");
