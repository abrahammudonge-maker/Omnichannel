# Deploying to Windows Server + IIS

The app is hosted path-based under the existing `test.servicesuitecloud.com`
site (IIS site name: `Default Web Site`, physical root `C:\inetpub\wwwroot`):

- `https://test.servicesuitecloud.com/omnichannel-api/` → backend API
- `https://test.servicesuitecloud.com/omnichannel/` → frontend

Both frontend and backend share one origin (same scheme+host+port, just
different paths), so no CORS configuration is needed for real traffic.

---

## 0. Before you start — rotate leaked test credentials

A Gmail app password and a WhatsApp/SMTP test credential were pasted into
chat during development. Rotate them once this app is reachable from the
internet:

- Gmail: revoke the old app password at https://myaccount.google.com/apppasswords
- Any other test credentials you don't recognize as your own real accounts.

---

## 1. Prerequisites on the server (already confirmed present)

- **.NET 8/9/10 shared runtimes + ASP.NET Core Module V2** — confirmed via
  `dotnet --list-runtimes` and `Get-WebGlobalModule`.
- **IIS URL Rewrite module** — confirmed installed (needed for SPA routing).
- **SQL Server Express**, instance `SQLEXPRESS`, running as `MSSQL$SQLEXPRESS`.
  TCP/IP is enabled on port **1433** (static, not dynamic).

---

## 2. Build artifacts (on your local dev machine)

```powershell
# Backend
cd Omni.Api
dotnet publish -c Release -o ..\publish\api
# Edit publish\api\web.config: add <environmentVariables><environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" /></environmentVariables> inside <aspNetCore>
mkdir publish\api\logs -Force

# Frontend
cd Omnichannel-frontend
npm run build
# dist\web.config must contain the SPA fallback rewrite rule pointing to /omnichannel/index.html
```

Zip both:

```powershell
Compress-Archive -Path publish\api\* -DestinationPath deploy-artifacts\omnichannel-api.zip -Force
Compress-Archive -Path Omnichannel-frontend\dist\* -DestinationPath deploy-artifacts\omnichannel.zip -Force
```

Copy `Omni.Infrastructure\Database\InitialSchema.sql` into `deploy-artifacts\`
too — it isn't part of the publish output.

`deploy-artifacts\deploy-on-server.ps1` automates steps 3-6 below. Fill in
its 6 variables at the top (site name/root, SQL instance, a new SQL login +
password, and a freshly generated JWT key) before running it on the server.

---

## 3. Database

The app's connection string uses `Server=localhost,1433` (a **fixed TCP
port**, not the named instance `localhost\SQLEXPRESS`). Testing found that
`Microsoft.Data.SqlClient`'s named-instance resolution (which depends on a
SQL Server Browser / UDP 1434 negotiation) failed with `System.
InvalidOperationException: Instance failure` even with SQL Browser running
and TCP/IP enabled — going straight to the known static port sidesteps that
negotiation entirely and is what actually works here. `sqlcmd` calls (used
for setup) can still use the named instance since those worked fine as-is.

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -C -Q "IF DB_ID('omnichannel') IS NULL CREATE DATABASE omnichannel;"
sqlcmd -S "localhost\SQLEXPRESS" -C -Q "IF SUSER_ID('omnichannel_app') IS NULL CREATE LOGIN [omnichannel_app] WITH PASSWORD = '<password>', CHECK_POLICY = OFF;"
sqlcmd -S "localhost\SQLEXPRESS" -C -d omnichannel -Q "IF USER_ID('omnichannel_app') IS NULL CREATE USER [omnichannel_app] FOR LOGIN [omnichannel_app]; ALTER ROLE db_owner ADD MEMBER [omnichannel_app];"
sqlcmd -S "localhost\SQLEXPRESS" -C -d omnichannel -i ".\InitialSchema.sql"
```

The `-C` flag trusts the server's self-signed certificate — required with
ODBC Driver 18, which defaults to certificate-validated encryption.

`CREATE LOGIN ... WITH PASSWORD` requires SQL Server to be in **Mixed Mode
Authentication** (Server Properties → Security in SSMS). Restart the
`MSSQL$SQLEXPRESS` service after changing that setting.

Any password used here must avoid `'`, `"`, `;`, `{`, `}`, and `` ` `` —
those break either the T-SQL string literal or the ADO.NET connection
string parser.

---

## 4. `appsettings.Production.json`

Created directly on the server (never committed to git):

```json
{
  "PathBase": "/omnichannel-api",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=omnichannel;User Id=omnichannel_app;Password=<password>;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "<64-byte base64 key — generate with [Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }))>",
    "Issuer": "https://test.servicesuitecloud.com",
    "Audience": "omnichannel",
    "ExpiryMinutes": 480
  },
  "Cors": {
    "AllowedOrigins": []
  }
}
```

Write it with a **single-quoted PowerShell here-string** (`@'...'@`) if any
value contains a backslash — double-quoted here-strings interpolate and
mis-escape backslashes into invalid JSON (this bit us once with the SQL
instance name before the connection string was switched to `host,port`).

---

## 5. IIS setup

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "omnichannel-api"
Set-ItemProperty "IIS:\AppPools\omnichannel-api" -Name managedRuntimeVersion -Value ""

New-WebApplication -Site "Default Web Site" -Name "omnichannel-api" -PhysicalPath "C:\inetpub\wwwroot\omnichannel-api" -ApplicationPool "omnichannel-api"
New-WebApplication -Site "Default Web Site" -Name "omnichannel" -PhysicalPath "C:\inetpub\wwwroot\omnichannel" -ApplicationPool "DefaultAppPool"

icacls "C:\inetpub\wwwroot\omnichannel-api" /grant "IIS AppPool\omnichannel-api:(OI)(CI)RX" /T
icacls "C:\inetpub\wwwroot\omnichannel-api\logs" /grant "IIS AppPool\omnichannel-api:(OI)(CI)M" /T
```

The `logs` folder needs **Modify** (write), not just Read+Execute — stdout
logging silently produces nothing without it.

---

## 6. Verify

```powershell
Invoke-WebRequest -Uri "https://test.servicesuitecloud.com/omnichannel-api/" -UseBasicParsing
Invoke-WebRequest -Uri "https://test.servicesuitecloud.com/omnichannel-api/health" -UseBasicParsing
Invoke-WebRequest -Uri "https://test.servicesuitecloud.com/omnichannel/" -UseBasicParsing
```

Then in an actual browser: register a new org at
`https://test.servicesuitecloud.com/omnichannel/register`, confirm it logs
in and lands on `/conversations`.

If the API 500s, enable stdout logging (`stdoutLogEnabled="true"` in
`web.config`), recycle the app pool, retrigger the request, then read the
newest file under `omnichannel-api\logs\`. If the log is empty, check the
Windows Application Event Log — ANCM writes startup crashes there even
before stdout logging can capture them:

```powershell
Get-EventLog -LogName Application -Source "IIS AspNetCore Module V2" -Newest 5 | Format-List TimeGenerated, Message
```

---

## 7. After deployment — connect real channels

1. Settings → Channels → reconnect email with the rotated Gmail app password.
2. For WhatsApp/Messenger/Instagram: in the Meta App Dashboard, set the
   webhook URL to `https://test.servicesuitecloud.com/omnichannel-api/api/webhooks/meta`.
3. Before deploying the WhatsApp hardening changes to an existing database,
  run `deploy-artifacts\WhatsAppHardeningMigration.sql` against the
  `omnichannel` database. A fresh database receives these columns from
  `InitialSchema.sql`.
4. Run `deploy-artifacts\AuthAndWorkflowMigration.sql` against the same
   database to enable revocable refresh tokens. A fresh database already
   receives the table from `InitialSchema.sql`.

---

## Redeploying after future changes

Backend:
```powershell
dotnet publish Omni.Api -c Release -o publish\api
# copy publish\api\* over C:\inetpub\wwwroot\omnichannel-api\ (excluding appsettings.Production.json)
Restart-WebAppPool -Name "omnichannel-api"
```

Frontend:
```powershell
npm run build
# copy dist\* over C:\inetpub\wwwroot\omnichannel\
```
No app pool restart needed for the frontend — IIS serves the new static
files immediately.
