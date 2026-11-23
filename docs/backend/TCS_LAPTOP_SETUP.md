# SecureShift on a TCS laptop – step by step (from zero)

This guide is for a laptop that has **only .NET 8**. You do **not** need Docker, a PostgreSQL installer or admin
rights. A "portable" PostgreSQL 16 runs from a folder (`C:\SecureShiftTools`, port 5434, this computer only).
Every command is for **Windows PowerShell**: copy one block at a time, paste it, press Enter.
Video: [00-setup-tcs-laptop.mp4](https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/00-setup-tcs-laptop.mp4).

## Choose your path

You need two files in every path except A:

| File | Where to get it | Size |
|---|---|---|
| `SecureShift-offline.zip` (the app, ready to run - no Git, no build) | https://github.com/Yashvardhandhondge/Minrole/releases/download/sprint4-demo/SecureShift-offline.zip | ~3.5 MB |
| `postgresql-16.11-1-windows-x64-binaries.zip` (PostgreSQL) | https://get.enterprisedb.com/postgresql/postgresql-16.11-1-windows-x64-binaries.zip | ~309 MB |

> **On the owner's laptop the PostgreSQL zip is already here:**
> `C:\SecureShiftTools\postgresql-16.11-1-windows-x64-binaries.zip` (324,093,756 bytes).
> Copy it (and `SecureShift-offline.zip`) to a pen drive for paths C and D.

### A) The laptop has Git and internet

```powershell
git clone https://github.com/Yashvardhandhondge/Minrole.git C:\Temp\SecureShift
cd C:\Temp\SecureShift
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1
```

`run.ps1` downloads PostgreSQL the first time, builds the app, creates the tables + sample data and opens the login
screen. The detailed steps are in sections 0-7 below.

### B) No Git (internet works)

1. Download `SecureShift-offline.zip` (link above), right-click it → **Extract All** → `C:\SecureShift`.
2. Open PowerShell and type:

```powershell
cd C:\SecureShift
powershell -ExecutionPolicy Bypass -File run.ps1
```

PostgreSQL is downloaded the first time (about 310 MB). Nothing is built: the app is already in `C:\SecureShift\app`.

### C) No internet (files from a pen drive)

1. Copy `SecureShift-offline.zip` and `postgresql-16.11-1-windows-x64-binaries.zip` from the pen drive.
2. Right-click `SecureShift-offline.zip` → **Extract All** → `C:\SecureShift`.
3. Put `postgresql-16.11-1-windows-x64-binaries.zip` **next to `run.ps1`** (in `C:\SecureShift`).
4. Open PowerShell and type:

```powershell
cd C:\SecureShift
powershell -ExecutionPolicy Bypass -File run.ps1
```

You see `Using the zip C:\SecureShift\postgresql-16.11-1-windows-x64-binaries.zip (no download needed)` and
`Offline package: using the published app ... (nothing to build)`. Nothing is downloaded and nothing is built.

### D) PowerShell scripts are blocked by policy (no .ps1 files at all)

Do steps 1-3 of path C, then type these commands **by hand, one by one** (typed commands are allowed even when
scripts are blocked):

```powershell
cd C:\SecureShift
mkdir C:\SecureShiftTools
tar -xf C:\SecureShift\postgresql-16.11-1-windows-x64-binaries.zip -C C:\SecureShiftTools
```

Create the database folder. It asks for a password twice: type one and remember it.

```powershell
C:\SecureShiftTools\pgsql\bin\initdb.exe -D C:\SecureShiftTools\pgdata -U postgres -A scram-sha-256 -E UTF8 --no-locale --pwprompt
```

Start PostgreSQL in the background (port 5434, this computer only), then wait 5 seconds:

```powershell
Start-Process C:\SecureShiftTools\pgsql\bin\pg_ctl.exe -WindowStyle Hidden -ArgumentList '-D','C:\SecureShiftTools\pgdata','-l','C:\SecureShiftTools\postgres.log','-o','"-p 5434 -c listen_addresses=localhost"','start'
Start-Sleep 5
```

Create the database and the schema (put YOUR password between the quotes):

```powershell
$env:PGPASSWORD = "the-password-you-typed"
C:\SecureShiftTools\pgsql\bin\psql.exe -h 127.0.0.1 -p 5434 -U postgres -c "CREATE DATABASE secureshift"
C:\SecureShiftTools\pgsql\bin\psql.exe -h 127.0.0.1 -p 5434 -U postgres -d secureshift -c "CREATE SCHEMA dev"
```

Tables + sample data, then the app:

```powershell
$env:ConnectionStrings__SecureShiftDb = "Host=127.0.0.1;Port=5434;Database=secureshift;Username=postgres;Password=$env:PGPASSWORD"
dotnet app\SecureShift.Console.dll db setup --yes
dotnet app\SecureShift.Console.dll
```

Next day in path D: the `Start-Process ... pg_ctl` line, then the two `$env:` lines, then
`dotnet app\SecureShift.Console.dll`. Stop PostgreSQL:
`C:\SecureShiftTools\pgsql\bin\pg_ctl.exe -D C:\SecureShiftTools\pgdata stop`

---

## 0. Check that .NET 8 is there

```powershell
dotnet --list-sdks
```

You must see a line that starts with `8.0.` (for example `8.0.428`). For the offline package (paths B, C, D)
the runtime is enough: `dotnet --list-runtimes` must show `Microsoft.NETCore.App 8.0.x`.
If you see "dotnet is not recognized", see **Troubleshooting → 'dotnet' is not recognized** at the end.

---

## 1. Get the code

**With Git:**

```powershell
cd C:\Temp
git clone https://github.com/Yashvardhandhondge/Minrole.git SecureShift
cd C:\Temp\SecureShift
```

**Without Git:** use path B or C above (the offline zip). Or on GitHub click **Code → Download ZIP**,
right-click the zip → **Extract All** to `C:\Temp\SecureShift`, then:

```powershell
cd C:\Temp\SecureShift
```

> Keep the folder in a short path like `C:\Temp\SecureShift`. Every command below is run **inside this folder**.

---

## 2. Start the portable PostgreSQL (first time: ~5 minutes)

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\setup-postgres-portable.ps1
```

What it does (it never needs admin):

1. uses `postgresql-16*-windows-x64-binaries.zip` if it is already next to the script, in the current folder or in
   `C:\SecureShiftTools` (no internet needed); otherwise downloads it (about 310 MB) to `C:\SecureShiftTools`
2. unzips it to `C:\SecureShiftTools\pgsql`
3. creates the data folder `C:\SecureShiftTools\pgdata` and a random password for the local `postgres` user
4. starts the server on **port 5434**, only for this computer (localhost)
5. creates the database `secureshift` and the empty schema `dev`
6. saves the connection string in `C:\SecureShiftTools\connection-string.txt`

At the end you see:

```
== Done. Connection string (saved in C:\SecureShiftTools\connection-string.txt; the password is not shown here):
Host=127.0.0.1;Port=5434;Database=secureshift;Username=postgres;Password=********
```

**If the download is blocked** (proxy / firewall): download
`https://get.enterprisedb.com/postgresql/postgresql-16.11-1-windows-x64-binaries.zip` in a browser, then:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\setup-postgres-portable.ps1 -ZipPath "$HOME\Downloads\postgresql-16.11-1-windows-x64-binaries.zip"
```

Running the script again is always safe: it skips what is already done and just starts the server.

---

## 3. Tell the app which database to use

**Easiest (for this PowerShell window only):**

```powershell
$env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt
```

You must run this again in every new PowerShell window. To keep it for good instead, create the file
`src\SecureShift.Console\appsettings.Local.json` (Git ignores it, so the password is never committed):

```powershell
$conn = Get-Content C:\SecureShiftTools\connection-string.txt
@{ ConnectionStrings = @{ SecureShiftDb = $conn } } | ConvertTo-Json | Set-Content src\SecureShift.Console\appsettings.Local.json
```

---

## 4. Build, create the tables, check

```powershell
dotnet build SecureShift.sln -c Release
$app = "src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll"
dotnet $app db setup --yes
dotnet $app db status
dotnet $app smoke
```

You should see:

```
Now: 28 tables, 4 views, 46 foreign keys, 569 rows.
[OK] All 28 tables, 4 views and 46 foreign keys are present.
[OK] EF Core can read the database and the counts match the fresh seed (25 / 5 / 10 / 3).
```

---

## 5. Run the app

```powershell
dotnet $app
```

or, from now on, one command that does steps 2–5 for you (starts PostgreSQL, sets the connection string,
builds the first time, creates the tables the first time, starts the app):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1
```

Login accounts (type `1` on the login screen, then the email and the password; the password shows as `*****`):

| Role | Email | Password |
|---|---|---|
| Cloud Admin | aarav.mehta@secureshift.io | Cloud@2026 |
| Super Admin | arya.mujumdar@secureshift.io | Super@2026 |
| Admin | sachin.kumar@secureshift.io | Admin@2026 |
| Architect | amanjeet.sharma@secureshift.io | Architect@2026 |
| Executive | shivanand.kumatekar@secureshift.io | Exec@2026 |

All other sample users have the password `Welcome@2026` and must change it at the first login.
What every menu does, with the exact keys to type: [USER_GUIDE.md](USER_GUIDE.md).

To leave the app: `0` (Logout) → `y` → `0` (Exit). `Ctrl+C` also stops it safely (nothing half-saved).

---

## 6. Stop, and start again tomorrow

Stop the database (your data is kept):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\stop-postgres.ps1
```

Next day, in the project folder:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1
```

---

## 7. Reset to a fresh database (sample data again)

This deletes ONLY the local portable database `secureshift` and creates it again with the 569 sample rows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset
```

To remove everything (PostgreSQL and its data): stop it (step 6), then delete the folders
`C:\SecureShiftTools\pgsql` and `C:\SecureShiftTools\pgdata`.

---

## 8. Using the faculty database instead

The faculty PostgreSQL database was created and seeded by hand. **Never run `db setup` or `db seed` on it.**
Ask for the host, database, user and password, then (fill in the placeholders):

```powershell
$env:ConnectionStrings__SecureShiftDb = "Host=FACULTY_HOST;Port=5432;Database=FACULTY_DB;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Prefer"
dotnet $app db status
```

Only ONE person runs this ONE time on the faculty database (it only adds the migration history table):

```powershell
dotnet $app db baseline
```

Then `dotnet $app` runs the app on the faculty database. Never put the real password in a file that Git tracks.

---

## 9. A machine that has only the .NET 8 runtime (no SDK)

On a machine WITH the SDK, make a "publish" folder (framework-dependent, net8.0):

```powershell
dotnet publish src\SecureShift.Console -c Release -o publish\SecureShift
```

Copy the folder `publish\SecureShift` (it contains the app, the SQL scripts and the Governance sample files)
to the other machine. There:

```powershell
cd C:\path\to\SecureShift
$env:ConnectionStrings__SecureShiftDb = "Host=...;Port=...;Database=...;Username=...;Password=..."
dotnet SecureShift.Console.dll db status
dotnet SecureShift.Console.dll
```

Check the runtime with `dotnet --list-runtimes` (you need `Microsoft.NETCore.App 8.0.x`).

---

## Troubleshooting

**"running scripts is disabled on this system"** – always start the scripts with
`powershell -ExecutionPolicy Bypass -File ...` exactly as shown (no admin needed; it only affects that one command).

**Port 5434 is already used** – the setup script says so. Use another port everywhere:
`powershell -ExecutionPolicy Bypass -File scripts\windows\setup-postgres-portable.ps1 -Port 5435`
(and `run.ps1 -Port 5435`). The connection-string file is updated automatically.

**Windows Firewall pops up** – click Cancel. The server only listens on this computer (localhost), so it works without
any firewall rule.

**"password authentication failed"** – the connection string does not match the local password. Run step 3 again
(`Get-Content C:\SecureShiftTools\connection-string.txt`). If you deleted `postgres-password.txt`, delete
`C:\SecureShiftTools\pgdata` and run the setup script again (this deletes the local data).

**"Could not connect to the database" / timeout** – the server is not running: run the setup script (step 2) again.
Use `Host=127.0.0.1`, not `localhost` (on some laptops `localhost` goes to IPv6 and times out).
The server log is `C:\SecureShiftTools\postgres.log`.

**The connection string is not set** – the app shows how to set it. Run step 3 in THIS window.

**"relation dev.Users does not exist" in psql** – Postgres names are case-sensitive; always quote them:
`SELECT * FROM dev."Users";`

**Build error CS8805** – `src\SecureShift.Console\SecureShift.Console.csproj` must keep `<OutputType>Exe</OutputType>`.

**'dotnet' is not recognized** – install the .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8` if you are allowed),
then close and reopen PowerShell. Without admin rights you can use the official `dotnet-install.ps1`
(`-InstallDir $HOME\.dotnet -Channel 8.0`) and add `$HOME\.dotnet` to PATH for your user.

**"db setup" refuses ("already has SecureShift tables")** – the database is already set up. Just run the app, or use
`run.ps1 -Reset` for a fresh local copy. On the faculty database use `db baseline`, never setup.

**The app looks frozen** – it waits for your answer after `Choose:`. Type a number and press Enter.
