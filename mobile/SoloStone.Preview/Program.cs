using SoloStone.Mobile;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5187");
var app = builder.Build();
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Web"));
var files = new PhysicalFileProvider(root);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
var host = new MobileHost(Path.Combine(AppContext.BaseDirectory, "preview-save"));
var gate = new SemaphoreSlim(1);
app.MapPost("/api", async (HttpRequest request) => {
    // Development host is loopback-only; reject requests initiated by other origins.
    if (request.Headers.Origin.Count > 0 && request.Headers.Origin != "http://127.0.0.1:5187")
        return Results.StatusCode(403);
    using var reader = new StreamReader(request.Body);
    var text = await reader.ReadToEndAsync();
    await gate.WaitAsync();
    try { return Results.Content(host.Handle(text), "application/json"); }
    finally { gate.Release(); }
});
app.Run();
