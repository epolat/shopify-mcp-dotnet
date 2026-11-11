using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopifyMcp.Core.Alerts;
using ShopifyMcp.Infrastructure;
using ShopifyMcp.Infrastructure.Alerts;
using ShopifyMcp.Server.Tools;

// Two modes:
//   shopify-mcp                → MCP server over stdio (what Claude Code / Claude Desktop start)
//   shopify-mcp alerts check   → evaluates alert rules once, sends notifications, exits (for cron)
var alertsMode = args.Length >= 2 && args[0] == "alerts" && args[1] == "check";

var builder = Host.CreateApplicationBuilder(alertsMode ? args[2..] : args);

// In server mode stdout carries the MCP protocol; anything else written there breaks the
// connection. Logs always go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

ShopifyOptions shop;
AlertOptions alertOptions;
try
{
    shop = ShopifyOptions.FromConfiguration(builder.Configuration);
    alertOptions = AlertOptions.FromConfiguration(builder.Configuration);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"[shopify-mcp] Configuration error: {ex.Message}");
    return 1;
}

builder.Services.AddShopify(shop).AddShopifyAlerts(shop, alertOptions);

if (alertsMode)
    return await RunAlertCheckAsync(builder.Build().Services);

var mcp = builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "shopify-mcp-dotnet", Version = "0.1.0" })
    .WithStdioServerTransport()
    .WithTools<ShopTools>()
    .WithTools<OrderTools>()
    .WithTools<ProductTools>()
    .WithTools<AnalyticsTools>()
    .WithTools<AlertTools>();

// Tools that change store data are opt-in.
if (shop.AllowWrites)
    mcp.WithTools<OrderWriteTools>();

await builder.Build().RunAsync();
return 0;

static async Task<int> RunAlertCheckAsync(IServiceProvider services)
{
    var alerts = services.GetRequiredService<AlertService>();
    if (alerts.Channels.Count == 0)
        Console.WriteLine("No notification channel configured (TELEGRAM_BOT_TOKEN/TELEGRAM_CHAT_ID or ALERT_WEBHOOK_URL). Results are only printed.");

    try
    {
        var results = await alerts.CheckAsync(notify: true);
        if (results.Count == 0)
            Console.WriteLine("No alert rules. Add one from your assistant (add_stock_alert).");

        var failed = false;
        foreach (var r in results)
        {
            Console.WriteLine($"[{r.Rule.Id}] {r.Rule.Name}: {r.Triggered.Count} triggered, " +
                              $"{r.NewSinceLastNotification.Count} new, sent via: {(r.NotifiedChannels.Count == 0 ? "-" : string.Join(", ", r.NotifiedChannels))}");
            foreach (var e in r.Errors)
            {
                Console.Error.WriteLine($"  error {e}");
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }
    catch (Exception ex) when (ex is ShopifyMcp.Core.ShopifyException or ArgumentException)
    {
        Console.Error.WriteLine($"[shopify-mcp] {ex.Message}");
        return 1;
    }
}
