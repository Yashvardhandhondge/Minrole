# SecureShift backend — how to run it

This guide is for Windows and PowerShell. Copy the grey boxes one by one.
Run every command from the **repo folder** (the folder that has `SecureShift.sln`).

> **New laptop with only .NET (no Docker, no PostgreSQL)?** Follow [TCS_LAPTOP_SETUP.md](TCS_LAPTOP_SETUP.md):
> one script starts a portable PostgreSQL 16 without admin rights. **What every menu does:** [USER_GUIDE.md](USER_GUIDE.md).
> Live status of the sprint: [PROGRESS.md](PROGRESS.md). Demo videos and the live-demo keystrokes: [../demo/](../demo/).

```powershell
cd C:\path\to\secure-shift      # change this to where your copy of the repo is
```

---

## 1. Install the .NET 8 SDK (one time)

```powershell
winget install Microsoft.DotNet.SDK.8
```

Close PowerShell and open it again, then check:

```powershell
dotnet --list-sdks
```

You should see a line that starts with `8.0.` (for example `8.0.428`).
Every project uses `net8.0`, because the demo machine (Accops) only has .NET 8.

---

## 2. Tell the app which database to use (the connection string)

`src/SecureShift.Console/appsettings.json` only has **placeholders** (`YOUR_HOST` ...).
Never put a real password in that file — it is committed to Git.

Pick **one** of these two ways. Ask your team lead for the real host, database, user and password.

### Way A — only for this PowerShell window (simplest)

```powershell
$env:ConnectionStrings__SecureShiftDb = "Host=YOUR_HOST;Port=5432;Database=YOUR_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
```

When you close the window, it is gone. Run it again in a new window.

### Way B — a local file that Git ignores

```powershell
Copy-Item src\SecureShift.Console\appsettings.Local.example.json src\SecureShift.Console\appsettings.Local.json
notepad src\SecureShift.Console\appsettings.Local.json
```

Replace the `YOUR_...` parts and save. `appsettings.Local.json` is in `.gitignore`, so it is never committed.

If both are set, the environment variable (Way A) wins.

> If the server needs SSL, add `;SSL Mode=Require;Trust Server Certificate=true` to the end of the connection string.

---

## 3. Build and check the connection

```powershell
dotnet build
dotnet run --project src/SecureShift.Console -- db status
```

`db status` prints the server, database and user, checks the **28 tables and 4 views**,
shows the rows per table and the total, and lists the applied migrations.

---

## 4. The database commands

Run them like this: `dotnet run --project src/SecureShift.Console -- <command>`

| Command | What it does | When to use it | Changes data? |
|---|---|---|---|
| `db status` | Shows server, database, user, the 28 tables / 4 views / 46 foreign keys, rows per table, migrations | Any time | No |
| `db setup` | Runs `01` (create) + `02` (seed) + `04` (migration history) | Only on a **brand-new, empty** `dev` schema. It refuses if any SecureShift table exists | Yes |
| `db seed` | Runs `02` (loads the 569 sample rows) | Only when all 28 tables exist and are **empty**. It refuses otherwise | Yes |
| `db migrate` | Applies new files from `database/postgres/migrations` (`V002__...sql`, `V003__...`) in order, each in its own transaction, and records them in `dev."__SchemaMigrations"` | After someone adds a migration file | Yes |
| `db baseline` | Only creates `dev."__SchemaMigrations"` and records "version 1 = baseline". Does not touch any SecureShift table | Once, on the **faculty DB** that was set up by hand | Only the history table |
| `smoke` | Counts Users / Roles / Companies / Enterprises through EF Core (expected 25 / 5 / 10 / 3) | To prove the app can read the DB | No |
| `help` | Lists the commands | Any time | No |

`setup`, `seed`, `migrate` and `baseline` ask **Type YES to continue**. Type `YES` (capital letters) and press Enter.
Anything else cancels. To skip the question (for scripts), add `--yes`:

```powershell
dotnet run --project src/SecureShift.Console -- db migrate --yes
```

---

## 5. First-time setup

### 5a. The faculty database (already created and seeded by Vedant) — the normal case

**Never run `db setup` or `db seed` here.** (They would refuse anyway, but do not try.)

```powershell
dotnet run --project src/SecureShift.Console -- db status      # should say 28 of 28 tables, 4 of 4 views
dotnet run --project src/SecureShift.Console -- db baseline    # type YES — only creates the migration history
dotnet run --project src/SecureShift.Console -- db status      # now shows "V001 baseline"
dotnet run --project src/SecureShift.Console -- smoke
```

`db baseline` only needs to be done **once** for the faculty DB (by one person). Running it again just says "Already baselined".

### 5b. A brand-new empty database (your own test database)

The schema `dev` must exist first. The app never creates schemas. Ask the DB admin, or in SQLTools / psql run:

```sql
CREATE SCHEMA dev;
```

Then:

```powershell
dotnet run --project src/SecureShift.Console -- db setup       # type YES
dotnet run --project src/SecureShift.Console -- db status
dotnet run --project src/SecureShift.Console -- smoke          # 25 / 5 / 10 / 3
```

You can also run the scripts by hand with psql, in this order:

```powershell
psql "host=YOUR_HOST port=5432 dbname=YOUR_DATABASE user=YOUR_USERNAME" -v ON_ERROR_STOP=1 -f database/postgres/01_create_secureshift_in_dev.sql
psql "host=YOUR_HOST port=5432 dbname=YOUR_DATABASE user=YOUR_USERNAME" -v ON_ERROR_STOP=1 -f database/postgres/02_seed_data_postgresql_dev.sql
psql "host=YOUR_HOST port=5432 dbname=YOUR_DATABASE user=YOUR_USERNAME" -v ON_ERROR_STOP=1 -f database/postgres/03_verify_dev.sql
```

The last line of `03` must say `VERIFY RESULT: PASS (tables=28, views=4, fks=46, rows=569)`.

---

## 6. Run the console app

```powershell
dotnet run --project src/SecureShift.Console
```

You get the login screen. After login you only see the menu of your role (Super Admin, Cloud Admin, Admin,
Architect, Executive). Every menu, with the exact keys to type, is in [USER_GUIDE.md](USER_GUIDE.md).
Faster to start (no build check each time): `dotnet build -c Release` once, then
`dotnet src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll`.

Leave with `0` (Logout) → `y` → `0` (Exit). `Ctrl+C` or closing the input also exits cleanly.

### Login accounts (seeded)

| Role | Email | Password |
|---|---|---|
| Cloud Admin | aarav.mehta@secureshift.io | Cloud@2026 |
| Super Admin | arya.mujumdar@secureshift.io | Super@2026 |
| Admin | sachin.kumar@secureshift.io | Admin@2026 |
| Architect | amanjeet.sharma@secureshift.io | Architect@2026 |
| Executive | shivanand.kumatekar@secureshift.io | Exec@2026 |

All other seeded users have `Welcome@2026` and must change it at the first login.

---

## 7. On the Accops demo machine

First check what .NET it has:

```powershell
dotnet --list-sdks
dotnet --list-runtimes      # you need a line "Microsoft.NETCore.App 8.0.x"
```

### If Accops has the .NET 8 SDK (`--list-sdks` shows 8.0.x)

```powershell
git clone https://github.com/Yashvardhandhondge/Minrole.git
cd Minrole
git checkout sprint4-console        # or main, after the PR is merged
$env:ConnectionStrings__SecureShiftDb = "Host=YOUR_HOST;Port=5432;Database=YOUR_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
dotnet run --project src/SecureShift.Console -- db status
dotnet run --project src/SecureShift.Console
```

### If Accops only has the .NET 8 runtime (no SDK)

On **your laptop**, make a ready-to-run folder:

```powershell
dotnet publish src/SecureShift.Console -c Release -o publish\SecureShift
```

Copy the whole `publish\SecureShift` folder to Accops (zip / USB / shared drive).
The SQL scripts are inside it (`database\postgres\...`), so the db commands work there too.
If you have an `appsettings.Local.json`, it is copied into that folder as well — do not share the folder with other people then.

On **Accops**:

```powershell
cd C:\path\to\SecureShift
$env:ConnectionStrings__SecureShiftDb = "Host=YOUR_HOST;Port=5432;Database=YOUR_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
dotnet SecureShift.Console.dll db status
dotnet SecureShift.Console.dll smoke
dotnet SecureShift.Console.dll
```

---

## 8. How to change the database schema

Never change the tables by hand in SQLTools and never run `dotnet ef migrations`.
Every change is a small SQL file. Full rules: `database/postgres/migrations/README.md`.

1. Make a branch and a new file with the next number, for example `V002__add_user_nickname.sql`:

   ```powershell
   git checkout -b add-user-nickname
   notepad database\postgres\migrations\V002__add_user_nickname.sql
   ```

   Example content (no `BEGIN` / `COMMIT` — the app adds the transaction):

   ```sql
   ALTER TABLE dev."Users" ADD COLUMN "Nickname" VARCHAR(50) NULL;
   ```

2. Apply it to **your test database** and check:

   ```powershell
   dotnet run --project src/SecureShift.Console -- db migrate
   dotnet run --project src/SecureShift.Console -- db status
   ```

3. Push the file and run the **scaffold** workflow on GitHub (it re-creates the EF Core classes from 01 + all V files):

   ```powershell
   git add database/postgres/migrations/V002__add_user_nickname.sql
   git commit -m "V002: add user nickname"
   git push -u origin add-user-nickname
   gh workflow run scaffold.yml --ref add-user-nickname
   ```

4. Wait for it, download the result and copy it over the old generated files:

   ```powershell
   Start-Sleep -Seconds 10
   $run = gh run list --workflow scaffold.yml --branch add-user-nickname --limit 1 --json databaseId --jq '.[0].databaseId'
   gh run watch $run
   gh run download $run -n scaffold-output -D scaffold-output
   Remove-Item -Recurse -Force src\SecureShift.Data\Models\Generated, src\SecureShift.Data\Context\Generated
   Copy-Item -Recurse scaffold-output\src\SecureShift.Data\Models\Generated src\SecureShift.Data\Models\
   Copy-Item -Recurse scaffold-output\src\SecureShift.Data\Context\Generated src\SecureShift.Data\Context\
   Remove-Item -Recurse -Force scaffold-output
   dotnet build
   ```

5. Commit the new generated files and open a pull request:

   ```powershell
   git add src/SecureShift.Data
   git commit -m "Re-scaffold EF models after V002"
   git push
   ```

6. After the PR is merged, one person runs `db migrate` on the faculty DB.

Never edit the files in `Models/Generated` or `Context/Generated` by hand — the next scaffold would overwrite them.
Extra code for a generated class goes in a partial class in `src/SecureShift.Data/Partials/`.

---

## 9. Run the tests

The unit tests need nothing. The database tests need a **freshly seeded** database
(use your own test DB, not the faculty DB — on a DB that people use, the row counts are different).

```powershell
$env:ConnectionStrings__SecureShiftDb = "Host=YOUR_HOST;Port=5432;Database=YOUR_TEST_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
dotnet test
```

Only the unit tests (no database):

```powershell
dotnet test --filter "FullyQualifiedName~MigrationServiceTests|FullyQualifiedName~FormulaTests|FullyQualifiedName~GovernanceInputTests"
```

The database user must be allowed to `CREATE DATABASE`: tests that write data make their own fresh database
(CREATE DATABASE + db setup) and drop it at the end, so the shared seeded database is never changed.

What the tests cover:

- **Formulas = prototype:** `tests/prototype/prototype-expected.js` runs the prototype JavaScript (`js/data/*.js`) in Node
  and writes the numbers to `tests/SecureShift.Tests/Data/prototype-expected.json`; the C# tests compare against it
  (EMSI, assessment factors, simulations, plan phases, governance ratios). CI checks the file is up to date:
  `node tests/prototype/prototype-expected.js --check`.
- **Services** against real PostgreSQL, **menus** with scripted keyboard input (IConsoleIO),
- **End-to-end:** `tests/e2e/phase*.sh` pipe keystrokes into the real built app and check the database with psql
  (`phase6-full-workflow.sh` = Super Admin → Admin → Architect → Executive → Cloud Admin in one run).

GitHub Actions (`.github/workflows/ci.yml`) runs everything on every push against a real PostgreSQL 16.

---

## 10. Troubleshooting

| You see | What it means / what to do |
|---|---|
| `relation "users" does not exist` or `relation "dev.users" does not exist` | Postgres names are **case-sensitive**. The tables are PascalCase, so you must use double quotes: `SELECT * FROM dev."Users";` (not `dev.users`, not `Users`). |
| `error CS8805: Program using top-level statements must be an executable` | `src/SecureShift.Console/SecureShift.Console.csproj` must keep `<OutputType>Exe</OutputType>`. Put it back. |
| Scaffold says `Expression index 'UX_Deps_Pair' on table dev.Dependencies cannot be scaffolded` | Expected. The index is still in the database and still blocks duplicate dependency pairs; EF Core just cannot describe an expression index. |
| Scaffold warnings about **collations** | Expected on some servers (for example the faculty DB). They do not change the generated classes. (The CI database has the default collation, so CI shows none.) |
| `The database connection string is not set` | Do step 2 (env var or `appsettings.Local.json`). |
| `Could not connect to the database: Failed to connect to ...` | Wrong host or port, VPN not connected, or a firewall blocks port 5432. Try `Test-NetConnection YOUR_HOST -Port 5432`. |
| `password authentication failed for user ...` | Wrong user name or password in the connection string. |
| `permission denied for schema dev` | Your DB user has no rights on `dev`. Ask the DB admin. |
| `'dotnet' is not recognized` | Close PowerShell and open it again after installing .NET (step 1). |
| `db setup` says `Refusing: schema dev already has ... table(s)` | That is the safety check. On the faculty DB use `db baseline`, never setup. |
| `db seed` says `Refusing: ... table(s) already have rows` | The seed only runs on empty tables, so it can never create duplicates. |
| `db migrate` says `There is no migration history` | Run `db baseline` (faculty DB) or `db setup` (new DB) first. |
| `... was changed after it was applied` | Someone edited a migration file that already ran. Put the file back and add a **new** V file instead. |
| `SQL script not found` | Build again: `dotnet build` (the scripts are copied next to the app at build time). |
| `Cannot write DateTime with Kind=Local to PostgreSQL type 'timestamp with time zone'` (later phases) | Use `DateTime.UtcNow`, not `DateTime.Now`, for `timestamptz` columns. |
| Tests fail with `The database tests need a freshly seeded SecureShift database` | Set `ConnectionStrings__SecureShiftDb` to a seeded test DB (step 9), or run only the unit tests. |
