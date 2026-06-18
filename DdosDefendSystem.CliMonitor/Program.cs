using System.Net.Http.Json;
using DdosDefendSystem.Shared;
using DdosDefendSystem.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Spectre.Console;

// ── Конфигурация ─────────────────────────────────────────────────────────────
var coordinatorUrl = ServiceEndpoints.ResolveCoordinatorUrl(
    Environment.GetEnvironmentVariable("COORDINATOR_URL"));

using var http = new HttpClient
{
    BaseAddress = new Uri(coordinatorUrl),
    Timeout     = TimeSpan.FromSeconds(5)
};

// ── Разделяемое состояние ─────────────────────────────────────────────────────
var state = new MonitorState();

// ── SignalR: живой трафик ─────────────────────────────────────────────────────
var hub = new HubConnectionBuilder()
    .WithUrl($"{coordinatorUrl}/hubs/traffic")
    .WithAutomaticReconnect()
    .Build();

hub.On<List<ActiveIpTraffic>>("ReceiveTraffic", traffic =>
{
    lock (state)
    {
        state.Traffic.Clear();
        state.Traffic.AddRange(traffic);
        state.LastTrafficUpdate = DateTime.Now;
    }
});

hub.Reconnecting  += _ => { state.HubState = "RECONNECTING"; return Task.CompletedTask; };
hub.Reconnected   += _ => { state.HubState = "CONNECTED";    return Task.CompletedTask; };
hub.Closed        += _ => { state.HubState = "DISCONNECTED"; return Task.CompletedTask; };

// ── Фоновый поллер чёрного списка (HTTP GET каждые 2 сек) ────────────────────
var cts = new CancellationTokenSource();

_ = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var bans = await http.GetFromJsonAsync<List<BannedIpInfo>>("/api/blacklist", cts.Token);
            lock (state)
            {
                state.Bans.Clear();
                if (bans != null) state.Bans.AddRange(bans);
                state.ApiReachable  = true;
                state.LastBanUpdate = DateTime.Now;
            }
        }
        catch
        {
            lock (state) { state.ApiReachable = false; }
        }

        await Task.Delay(2000, cts.Token).ConfigureAwait(false);
    }
}, cts.Token);

// ── Подключение к SignalR (не блокируем старт дашборда) ─────────────────────
_ = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            await hub.StartAsync(cts.Token);
            state.HubState = "CONNECTED";
            return;
        }
        catch
        {
            state.HubState = "CONNECTING…";
            await Task.Delay(4000, cts.Token).ConfigureAwait(false);
        }
    }
}, cts.Token);

// ── Выход по Ctrl+C ──────────────────────────────────────────────────────────
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── Live Display ──────────────────────────────────────────────────────────────
await AnsiConsole.Live(BuildLayout(state, coordinatorUrl))
    .AutoClear(false)
    .Overflow(VerticalOverflow.Ellipsis)
    .Cropping(VerticalOverflowCropping.Bottom)
    .StartAsync(async ctx =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            ctx.UpdateTarget(BuildLayout(state, coordinatorUrl));
            await Task.Delay(800).ConfigureAwait(false);
        }
    });

// ── Завершение ────────────────────────────────────────────────────────────────
await hub.DisposeAsync();
AnsiConsole.MarkupLine("\n[grey]Monitor stopped.[/]");

// ═════════════════════════════════════════════════════════════════════════════
//  Построение UI
// ═════════════════════════════════════════════════════════════════════════════

static IRenderable BuildLayout(MonitorState s, string url)
{
    MonitorState snap;
    lock (s)
    {
        snap = new MonitorState
        {
            ApiReachable    = s.ApiReachable,
            HubState        = s.HubState,
            LastBanUpdate   = s.LastBanUpdate,
            LastTrafficUpdate = s.LastTrafficUpdate,
            Bans            = [.. s.Bans],
            Traffic         = [.. s.Traffic],
        };
    }

    // ── Заголовок ────────────────────────────────────────────────────────────
    var header = new Panel(
        Align.Center(
            new Markup($"[bold white]DDoS COORDINATOR MONITOR[/]  [grey]|[/]  [dim]{url}[/]  [grey]|[/]  [dim]{DateTime.Now:HH:mm:ss}[/]"),
            VerticalAlignment.Middle))
    {
        Border      = BoxBorder.Heavy,
        BorderStyle = Style.Parse("grey"),
        Padding     = new Padding(1, 0),
    };

    // ── Строка статуса ────────────────────────────────────────────────────────
    var apiStatus  = snap.ApiReachable
        ? "[green]● ONLINE [/]"
        : "[red]● OFFLINE[/]";

    var hubStatus  = snap.HubState switch
    {
        "CONNECTED"    => "[green]● CONNECTED   [/]",
        "RECONNECTING" => "[yellow]● RECONNECTING[/]",
        _              => "[red]● DISCONNECTED[/]",
    };

    var statusGrid = new Grid();
    statusGrid.AddColumn(new GridColumn().PadRight(4));
    statusGrid.AddColumn(new GridColumn().PadRight(4));
    statusGrid.AddColumn(new GridColumn());
    statusGrid.AddRow(
        $"[grey]HTTP API :[/] {apiStatus}",
        $"[grey]SignalR  :[/] {hubStatus}",
        $"[grey]Updated  :[/] [dim]{snap.LastBanUpdate:HH:mm:ss}[/]");

    var statusPanel = new Panel(statusGrid)
    {
        Border      = BoxBorder.Rounded,
        BorderStyle = Style.Parse("grey"),
        Header      = new PanelHeader("[grey] STATUS [/]"),
        Padding     = new Padding(1, 0),
    };

    // ── Таблица трафика (L4 / SignalR) ─────────────────────────────────────
    var trafficTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderStyle(Style.Parse("grey"))
        .Title("[grey] LIVE TRAFFIC  (SignalR /hubs/traffic) [/]")
        .AddColumn(new TableColumn("[bold]IP Address[/]").LeftAligned())
        .AddColumn(new TableColumn("[bold]Connections[/]").RightAligned());

    if (snap.Traffic.Count == 0)
    {
        trafficTable.AddRow("[dim]—[/]", "[dim]no data[/]");
    }
    else
    {
        foreach (var t in snap.Traffic.OrderByDescending(x => x.ConnectionCount).Take(10))
        {
            var bar   = BuildBar(t.ConnectionCount, snap.Traffic.Max(x => x.ConnectionCount));
            var color = t.ConnectionCount > 50 ? "red" : t.ConnectionCount > 20 ? "yellow" : "green";
            trafficTable.AddRow(
                $"[white]{t.IpAddress}[/]",
                $"[{color}]{t.ConnectionCount,4}[/] [grey]{bar}[/]");
        }
    }

    // ── Таблица банов ─────────────────────────────────────────────────────────
    var banTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderStyle(Style.Parse("grey"))
        .Title($"[grey] BLACKLIST  ({snap.Bans.Count} active bans) [/]")
        .AddColumn(new TableColumn("[bold]IP / Subnet[/]").LeftAligned())
        .AddColumn(new TableColumn("[bold]Banned At (UTC)[/]").LeftAligned())
        .AddColumn(new TableColumn("[bold]Expires (UTC)[/]").LeftAligned())
        .AddColumn(new TableColumn("[bold]Reason[/]").LeftAligned());

    if (snap.Bans.Count == 0)
    {
        banTable.AddRow("[dim]—[/]", "[dim]—[/]", "[dim]—[/]", "[dim]No active bans[/]");
    }
    else
    {
        foreach (var ban in snap.Bans.OrderByDescending(b => b.BlockedAt).Take(30))
        {
            var remaining = ban.ExpiresAt - DateTime.UtcNow;
            var expiresStr = remaining > TimeSpan.Zero
                ? $"{ban.ExpiresAt:yyyy-MM-dd HH:mm:ss} [dim](-{remaining:mm\\:ss})[/]"
                : $"[dim]{ban.ExpiresAt:yyyy-MM-dd HH:mm:ss}[/]";

            var reasonShort = ban.Reason.Length > 55
                ? ban.Reason[..52] + "…"
                : ban.Reason;

            banTable.AddRow(
                $"[white]{ban.IpAddress}[/]",
                $"[dim]{ban.BlockedAt:yyyy-MM-dd HH:mm:ss}[/]",
                expiresStr,
                $"[yellow]{Markup.Escape(reasonShort)}[/]");
        }
    }

    // ── Сборка Layout ─────────────────────────────────────────────────────────
    return new Rows(header, statusPanel, trafficTable, banTable);
}

static string BuildBar(int value, int max)
{
    if (max <= 0) return string.Empty;
    var filled = (int)Math.Round(value / (double)max * 12);
    return new string('█', filled) + new string('░', 12 - filled);
}

// ═════════════════════════════════════════════════════════════════════════════
//  Модель состояния
// ═════════════════════════════════════════════════════════════════════════════
internal sealed class MonitorState
{
    public bool              ApiReachable      { get; set; }
    public string            HubState          { get; set; } = "CONNECTING…";
    public DateTime          LastBanUpdate     { get; set; }
    public DateTime          LastTrafficUpdate { get; set; }
    public List<BannedIpInfo>    Bans          { get; set; } = [];
    public List<ActiveIpTraffic> Traffic       { get; set; } = [];
}
