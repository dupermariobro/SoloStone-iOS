using System.Text.Json;

namespace SoloStone.Mobile;

// Shared by the on-device bridge and the loopback-only development preview.
public sealed class MobileHost(string saveDirectory)
{
    private readonly MatchSession match = new();
    private string SavePath => Path.Combine(saveDirectory, "match.bin");

    public string Handle(string json)
    {
        int id = 0;
        string? error = null, warning = null;
        object? state = null;
        Dictionary<string, string>? legal = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var request = doc.RootElement;
            id = request.GetProperty("id").GetInt32();
            string command = request.GetProperty("command").GetString()!;
            switch (command)
            {
                case "init": break;
                case "legal":
                    var assembly = typeof(MobileHost).Assembly;
                    legal = assembly.GetManifestResourceNames().Where(n => n.StartsWith("Legal."))
                        .ToDictionary(n => n[6..], n => {
                            using var reader = new StreamReader(assembly.GetManifestResourceStream(n)!);
                            return reader.ReadToEnd();
                        });
                    break;
                case "start":
                    match.Start(request.GetProperty("player").GetString()!, request.GetProperty("opponent").GetString()!);
                    break;
                case "resume": match.Restore(File.ReadAllBytes(SavePath)); break;
                case "answer":
                    match.Answer(request.GetProperty("revision").GetInt64(), request.GetProperty("decision").GetInt64(),
                        request.GetProperty("selections").EnumerateArray().Select(s => s.GetInt32()).ToArray());
                    break;
                case "bot": match.StepBot(); break;
                case "concede": match.Concede(); break;
                default: throw new InvalidOperationException("Unknown command.");
            }
            if (match.Game != null)
            {
                state = match.View();
                if (command is not ("init" or "legal"))
                {
                    try
                    {
                        var bytes = match.Save();
                        Directory.CreateDirectory(saveDirectory);
                        File.WriteAllBytes(SavePath + ".tmp", bytes);
                        File.Move(SavePath + ".tmp", SavePath, true);
                    }
                    catch (Exception e) { warning = "Could not save this position: " + e.Message; }
                }
            }
        }
        catch (Exception e) { error = e.Message; }
        return JsonSerializer.Serialize(new { id, error, warning, state, legal,
            decks = MatchSession.Decks.Select(d => new { d.Id, d.Name, d.Class }), canResume = File.Exists(SavePath) }, MatchSession.Json);
    }
}
