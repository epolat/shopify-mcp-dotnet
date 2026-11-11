# shopify-mcp-dotnet

**Talk to your Shopify store from Claude.** An [MCP](https://modelcontextprotocol.io) server written in .NET that lets AI assistants (Claude Code, Claude Desktop, Cursor and any other MCP client) read orders, products and inventory, run sales reports, and send stock alerts to Telegram or any webhook.

```text
You:    How were sales in the last 15 days, by week?
Claude: → sales_report(days: 15, groupBy: "week")
        Net sales were ₺48,210 over 132 orders (AOV ₺365). Week 46 was the strongest...

You:    How much stock do I have, and what will run out in the next 10 days?
Claude: → inventory_report()  → stock_runway(maxDays: 10)
        1,840 units on hand, worth ₺212,400 at cost (₺495,000 retail).
        3 variants will run out within 10 days: Ceramic Mug / Blue (4 days left)...

You:    Let me know on Telegram whenever something has less than 10 days of stock.
Claude: → add_stock_alert(maxDays: 10)
        Done. You'll get a Telegram message when `shopify-mcp alerts check` runs.
```

## Features

| Tool | What it does |
|---|---|
| `get_shop_info` | Store name, domain, currency, timezone |
| `list_orders` | Orders filtered by status, payment, fulfillment, date range or free text |
| `get_order` | One order with line items, address and note (accepts `1042`, `#1042` or a GID) |
| `search_products` | Products by title or Shopify search syntax |
| `list_low_stock` | Variants at or below an inventory threshold |
| `sales_report` | Total/gross/net sales, orders, AOV, items sold for the last N days, by day/week/month/product/variant/channel |
| `inventory_report` | Units on hand, cost value and retail value, optionally by product/variant/vendor/type |
| `stock_runway` | Variants that will run out within N days at their recent sales pace |
| `run_shopifyql` | Any [ShopifyQL](https://shopify.dev/docs/api/shopifyql) analytics query, for everything else |
| `add_stock_alert` / `list_alerts` / `remove_alert` / `check_alerts` | Stock alerts with Telegram and webhook notifications |
| `update_order_note` | Append to or replace an order note. **Only when `SHOPIFY_ALLOW_WRITES=true`** |

Design choices:

- **Read-only by default.** Tools that change store data are not even registered unless you opt in.
- **Runs locally over stdio.** Your credentials stay on your machine; there is no hosted service.
- **Both Shopify auth models.** Dev Dashboard apps (client credentials, tokens refreshed automatically every 24h) and legacy custom-app tokens (`shpat_...`).
- **Handles throttling.** Throttled requests are retried with backoff, and Shopify errors come back as readable messages the assistant can act on.
- **Clean architecture.** `Core` (services, no dependencies) → `Infrastructure` (HTTP, auth, alert storage) → `Server` (MCP host). The same services can back a web API or CLI.

## Quick start

### 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Shopify store. A free development store from a [Shopify Partner](https://www.shopify.com/partners) account works well for testing.

### 2. Create a Shopify app and get credentials

**Option A: Dev Dashboard app (recommended)**

1. Open the [Dev Dashboard](https://dev.shopify.com/dashboard) and create an app.
2. In a new app version, select these Admin API scopes:
   - `read_orders`, `read_products`, `read_inventory`, `read_reports`
   - `write_orders`, only if you plan to enable write tools
3. Release the version and install the app on your store. The store must belong to the same organization as the app.
4. Copy the **Client ID** and **Client secret** from the app's settings.

The server exchanges these for an access token using the [client credentials grant](https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens/client-credentials-grant) and refreshes it before it expires.

**Option B: legacy custom app token.** If you already have an Admin API token (`shpat_...`) from a custom app created in the Shopify admin, set `SHOPIFY_ACCESS_TOKEN` instead of the client ID and secret.

> **Customer data:** customer names, and some ShopifyQL reports, need [protected customer data](https://shopify.dev/docs/apps/launch/protected-customer-data) access. If customer names come back empty, or a report says access is denied, request that access in the app's configuration.

### 3. Build

```bash
git clone https://github.com/epolat/shopify-mcp-dotnet.git
cd shopify-mcp-dotnet
dotnet publish src/ShopifyMcp.Server -c Release -o publish
```

This produces `publish/shopify-mcp.exe` on Windows or `publish/shopify-mcp` on macOS/Linux. The configs below need its **absolute path**.

<details>
<summary>Alternative: install as a global .NET tool</summary>

```bash
dotnet pack src/ShopifyMcp.Server -c Release -o nupkg
dotnet tool install -g ShopifyMcp.Server --add-source ./nupkg
# now available as the `shopify-mcp` command
```
</details>

### 4a. Connect to Claude Code

```bash
claude mcp add shopify --scope user \
  -e SHOPIFY_STORE_DOMAIN=my-store.myshopify.com \
  -e SHOPIFY_CLIENT_ID=your-client-id \
  -e SHOPIFY_CLIENT_SECRET=your-client-secret \
  -- /absolute/path/to/publish/shopify-mcp
```

Check it with `claude mcp list` or `/mcp` inside Claude Code, then ask: *"What are my unfulfilled orders from the last 3 days?"*

To share the setup with a team, commit a `.mcp.json` like [examples/mcp.json](examples/mcp.json). It reads secrets from environment variables, so none end up in the repo.

### 4b. Connect to Claude Desktop

Open **Settings → Developer → Edit Config**, or edit the file directly:

- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "shopify": {
      "command": "C:\\path\\to\\publish\\shopify-mcp.exe",
      "env": {
        "SHOPIFY_STORE_DOMAIN": "my-store.myshopify.com",
        "SHOPIFY_CLIENT_ID": "your-client-id",
        "SHOPIFY_CLIENT_SECRET": "your-client-secret"
      }
    }
  }
}
```

Restart Claude Desktop completely (quit it from the tray or menu bar). The tools show up under the 🔧 icon.

Any other MCP client (Cursor, Windsurf, VS Code, etc.) works the same way: a `command` plus `env`.

## Configuration

All settings are environment variables.

| Variable | Required | Description |
|---|---|---|
| `SHOPIFY_STORE_DOMAIN` | ✅ | `my-store.myshopify.com`. `my-store` or a full URL also work. |
| `SHOPIFY_CLIENT_ID` + `SHOPIFY_CLIENT_SECRET` | one of the two | Dev Dashboard app credentials |
| `SHOPIFY_ACCESS_TOKEN` | one of the two | Legacy custom app token (`shpat_...`) |
| `SHOPIFY_API_VERSION` | | Admin API version. Default `2026-07`. |
| `SHOPIFY_ALLOW_WRITES` | | `true` registers tools that modify the store. Default `false`. |
| `SHOPIFY_MCP_DATA_DIR` | | Where alert rules are stored. Default `~/.shopify-mcp`. |
| `TELEGRAM_BOT_TOKEN` + `TELEGRAM_CHAT_ID` | | Send alerts to Telegram |
| `ALERT_WEBHOOK_URL` | | POST alerts as JSON to Slack, Discord, n8n, Zapier, Make, etc. |
| `ALERT_COOLDOWN_HOURS` | | Don't re-send an alert for the same variant within this window. Default `24`. |

## Stock alerts

An MCP server only answers when the assistant asks it something, so it can't wake up on its own to warn you. Alerts therefore have two parts:

1. **Rules** are created from the assistant (`add_stock_alert`) and saved to `~/.shopify-mcp/<store>.alerts.json`.
2. **Checks** run on a schedule with `shopify-mcp alerts check`. This command evaluates every rule, notifies the configured channels and exits.

```text
 Claude ── add_stock_alert ──▶ alerts.json ◀── shopify-mcp alerts check ◀── cron / Task Scheduler
                                                         │
                                                         ├──▶ Telegram
                                                         └──▶ Webhook ──▶ Slack / Discord / n8n → e-mail, WhatsApp...
```

The rule *"10 days of stock left"* uses Shopify's own `days_of_inventory_remaining` metric: units on hand ÷ units sold per day over the lookback window (30 days by default). Variants with no sales in that window never trigger an alert. Each variant is notified at most once per cooldown window.

### Telegram setup

1. Message [@BotFather](https://t.me/BotFather), send `/newbot` and copy the token.
2. Send any message to your new bot, then open `https://api.telegram.org/bot<TOKEN>/getUpdates` and copy `message.chat.id`.
3. Set `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` both in the MCP config and in the scheduled job.

### Scheduling the check

**Linux / macOS (cron)**, hourly:

```cron
0 * * * * SHOPIFY_STORE_DOMAIN=my-store.myshopify.com SHOPIFY_CLIENT_ID=... SHOPIFY_CLIENT_SECRET=... TELEGRAM_BOT_TOKEN=... TELEGRAM_CHAT_ID=... /path/to/shopify-mcp alerts check >> ~/.shopify-mcp/alerts.log 2>&1
```

**Windows (Task Scheduler)**: copy [examples/run-alerts.cmd](examples/run-alerts.cmd), fill in your values, then register it:

```powershell
schtasks /create /tn "Shopify stock alerts" /sc hourly /tr "C:\path\to\run-alerts.cmd"
```

Exit codes: `0` ok, `1` configuration or Shopify error, `2` at least one channel failed.

### Adding a channel

Implement `IAlertNotifier` (a name plus one `SendAsync` method) and register it in `AddShopifyAlerts` in [DependencyInjection.cs](src/ShopifyMcp.Infrastructure/DependencyInjection.cs). See `TelegramNotifier` in [Notifiers.cs](src/ShopifyMcp.Infrastructure/Alerts/Notifiers.cs) for a 20-line example. Without writing code, `ALERT_WEBHOOK_URL` pointed at n8n, Zapier or Make can forward alerts to email, WhatsApp, SMS and more.

## Example questions

- "Show me today's orders that haven't been shipped yet."
- "What's in order #1042, and where is it going?"
- "Sales for the last 15 days, by day."
- "Top 10 products by net sales this month."
- "Which sales channel brought the most revenue in the last 90 days?"
- "How much is my inventory worth at cost, by vendor?"
- "What will run out in the next 2 weeks?"
- "Compare this month's returns to last month." (uses `run_shopifyql`)
- "Add a note to #1042: customer called, address changes." (needs `SHOPIFY_ALLOW_WRITES=true`)

## Development

```bash
dotnet build
dotnet test
```

```text
src/
  ShopifyMcp.Core/            Services, models, GraphQL & ShopifyQL queries, alert logic. No external dependencies.
  ShopifyMcp.Infrastructure/  Shopify HTTP client, auth (client credentials / static token), alert store, notifiers.
  ShopifyMcp.Server/          MCP host (stdio), tool definitions, `alerts check` command.
tests/
  ShopifyMcp.Tests/           Unit tests with fake HTTP handlers and GraphQL clients.
```

**Adding a tool:** add a method to a service in `Core`, then expose it from a class in `Server/Tools` with `[McpServerTool]` and a clear `[Description]`. The description is what the model reads when it decides which tool to call. Register the class in `Program.cs`.

**Debugging:** run the server under the [MCP Inspector](https://github.com/modelcontextprotocol/inspector):

```bash
npx @modelcontextprotocol/inspector ./publish/shopify-mcp
```

The server logs to stderr only, because stdout is reserved for the protocol.

All Admin GraphQL operations are validated against the `2026-07` schema.

## Security

- Credentials are read from environment variables and are never logged.
- Keep the MCP config file private. It contains your client secret.
- Grant the app only the scopes you need. With `SHOPIFY_ALLOW_WRITES` unset, the assistant has no tool that can change store data.
- `run_shopifyql` is read-only by design: ShopifyQL can't modify data.

## Roadmap

- [ ] Draft orders and abandoned checkouts
- [ ] Fulfillment tools (create fulfillment, add tracking number)
- [ ] Inventory adjustments
- [ ] Email notifier (SMTP)
- [ ] Publish to NuGet so it runs with `dnx ShopifyMcp.Server`
- [ ] Streamable HTTP transport for remote hosting

Contributions are welcome. Open an issue first for larger changes.

## License

[MIT](LICENSE)
