using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorshipProjectionApp.Services;

// Servidor web interno (Kestrel) que serve a página de controlo remoto para o telemóvel, protegido por PIN
public sealed class RemoteControlService
{
    private sealed class RemoteConfig
    {
        public string Pin { get; set; } = string.Empty;
        public int Port { get; set; } = 8765;
    }

    public sealed class SongAddRequest { public int Id { get; set; } public bool Project { get; set; } }
    public sealed class BibleAddRequest { public string Q { get; set; } = ""; public string V { get; set; } = ""; public int From { get; set; } public int To { get; set; } public bool Project { get; set; } }

    private readonly MainWindow _host;
    private WebApplication? _app;
    private RemoteConfig _config = new();

    public bool IsRunning { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public string Pin => _config.Pin;
    public int Port => _config.Port;

    public RemoteControlService(MainWindow host) { _host = host; }

    private static string ConfigPath => AppPaths.File("remote.json");

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                _config = JsonSerializer.Deserialize<RemoteConfig>(File.ReadAllText(ConfigPath)) ?? new RemoteConfig();
        }
        catch { _config = new RemoteConfig(); }

        if (string.IsNullOrWhiteSpace(_config.Pin) || _config.Pin.Length < 6)
        {
            _config.Pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_config)); } catch { }
        }
    }

    public void Start()
    {
        LoadConfig();
        int basePort = _config.Port;

        for (int attempt = 0; attempt < 10 && !IsRunning; attempt++)
        {
            int port = basePort + attempt;
            try
            {
                var app = Build(port);
                Task.Run(() => app.StartAsync()).GetAwaiter().GetResult();
                _app = app;
                _config.Port = port;
                IsRunning = true;
                LastError = string.Empty;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                AppPaths.Log("Remote.Start", ex);
            }
        }
    }

    public void Stop()
    {
        try { var a = _app; _app = null; IsRunning = false; if (a != null) Task.Run(() => a.StopAsync()); } catch { }
    }

    public List<string> GetUrls()
    {
        var list = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue;
                    list.Add($"http://{ip}:{_config.Port}/?k={_config.Pin}");
                }
            }
        }
        catch { }
        return list;
    }

    private WebApplication Build(int port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        var app = builder.Build();

        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                string given = ctx.Request.Headers["X-Pin"].ToString();
                if (!CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(given), System.Text.Encoding.UTF8.GetBytes(_config.Pin)))
                {
                    await Task.Delay(500);
                    ctx.Response.StatusCode = 401;
                    return;
                }
            }
            try { await next(); }
            catch (Exception ex)
            {
                AppPaths.Log("Remote.Request", ex);
                if (!ctx.Response.HasStarted) ctx.Response.StatusCode = 500;
            }
        });

        app.MapGet("/", () => Results.Content(RemotePage.Html, "text/html; charset=utf-8"));
        app.MapGet("/api/state", () => Results.Json(_host.RemoteGetState()));
        app.MapPost("/api/cmd/{name}", (string name) => Results.Json(new { ok = _host.RemoteCommand(name) }));
        app.MapPost("/api/item/{id}", (string id) => Results.Json(new { ok = _host.RemoteSelectItem(id) }));
        app.MapPost("/api/slide/{n:int}", (int n) => Results.Json(new { ok = _host.RemoteSelectSlide(n) }));
        app.MapGet("/api/songs", (string? q, string? lyrics) => Results.Json(_host.RemoteSearchSongs(q ?? "", lyrics == "1")));
        app.MapPost("/api/songs/add", (SongAddRequest r) => Results.Json(new { ok = _host.RemoteAddSong(r.Id, r.Project) }));
        app.MapGet("/api/bible/versions", () => Results.Json(_host.RemoteBibleVersions()));
        app.MapGet("/api/bible", (string? q, string? v) => Results.Json(_host.RemoteSearchBible(q ?? "", v ?? "")));
        app.MapPost("/api/bible/add", (BibleAddRequest r) => Results.Json(new { ok = _host.RemoteAddBible(r.Q, r.V, r.From, r.To, r.Project) }));
        return app;
    }
}
