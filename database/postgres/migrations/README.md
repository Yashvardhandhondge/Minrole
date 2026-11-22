# Schema migrations

Every change to the SecureShift tables **after** the baseline (01 + 02) goes in this folder.

## Rules

1. **One change per file.** Example: add one column, or add one table, or add one index.
2. **File name:** `V<3-digit number>__<short_name>.sql`
   - `V002__add_user_last_password_change.sql`
   - `V003__add_plan_priority.sql`
   - The number is the version. Version 1 is the baseline, so the first file is **V002**.
   - Two underscores between the number and the name. Name: letters, digits and `_` only.
   - Never reuse a number, never rename or edit a file that was already applied
     (`db status` / `db migrate` warn when an applied file was changed).
3. **No `BEGIN` / `COMMIT` inside the file.** The console runs each file in its own
   transaction and records it in `dev."__SchemaMigrations"` in the same transaction.
   If any statement fails, the whole file is rolled back and nothing is recorded.
4. Always use the schema and quoted names: `ALTER TABLE dev."Users" ADD COLUMN "Nickname" VARCHAR(50) NULL;`
5. Do not drop or rename columns that the generated EF models use without updating the code in the same PR.

## Example file: `V002__add_user_nickname.sql`

```sql
-- V002: users can have an optional nickname (shown on the profile screen)
ALTER TABLE dev."Users" ADD COLUMN "Nickname" VARCHAR(50) NULL;
```

## How to apply and update the models

```powershell
dotnet run --project src/SecureShift.Console -- db status    # shows the pending files
dotnet run --project src/SecureShift.Console -- db migrate   # type YES to apply them
gh workflow run scaffold.yml --ref <your-branch>             # re-generate the EF models
```

Then download the scaffold artifact, replace `src/SecureShift.Data/Models/Generated` and
`src/SecureShift.Data/Context/Generated`, build, and commit. See `docs/backend/RUNNING.md`.
