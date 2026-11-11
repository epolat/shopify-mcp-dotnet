@echo off
REM Copy this file outside the repo, fill in your values and schedule it:
REM   schtasks /create /tn "Shopify stock alerts" /sc hourly /tr "C:\path\to\run-alerts.cmd"

set SHOPIFY_STORE_DOMAIN=my-store.myshopify.com
set SHOPIFY_CLIENT_ID=your-client-id
set SHOPIFY_CLIENT_SECRET=your-client-secret
set TELEGRAM_BOT_TOKEN=
set TELEGRAM_CHAT_ID=
set ALERT_WEBHOOK_URL=

"C:\path\to\publish\shopify-mcp.exe" alerts check >> "%USERPROFILE%\.shopify-mcp\alerts.log" 2>&1
