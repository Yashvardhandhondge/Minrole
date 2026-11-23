#!/usr/bin/env bash
# Helpers for the end-to-end tests: they pipe keystrokes into the REAL built console app
# and then check what it printed and what is in the database.
#
#   APP_CMD   how to start the app      (CI: "dotnet src/SecureShift.Console/bin/Release/net8.0/SecureShift.Console.dll")
#   PSQL      how to run psql           (CI: "psql"; locally e.g. "docker exec -i secureshift-pg psql -U postgres")
#   E2E_CONN  connection string WITHOUT the database name, e.g. "Host=localhost;Port=5432;Username=postgres;Password=..."
set -euo pipefail

: "${APP_CMD:?set APP_CMD}"
: "${E2E_CONN:?set E2E_CONN}"
PSQL=${PSQL:-psql}
OUT_DIR=${OUT_DIR:-e2e-output}
mkdir -p "$OUT_DIR"

# fresh_db NAME : drop + create a database with the empty schema dev, then "db setup --yes" (01 + 02 + 04)
fresh_db() {
  E2E_DB=$1
  $PSQL -v ON_ERROR_STOP=1 -q -d postgres -c "DROP DATABASE IF EXISTS $E2E_DB WITH (FORCE);" -c "CREATE DATABASE $E2E_DB;"
  $PSQL -v ON_ERROR_STOP=1 -q -d "$E2E_DB" -c 'CREATE SCHEMA dev;'
  export ConnectionStrings__SecureShiftDb="$E2E_CONN;Database=$E2E_DB"
  $APP_CMD db setup --yes > "$OUT_DIR/$E2E_DB-setup.txt"
}

# run_app NAME LINE... : types the lines into the app (one per Enter), output -> $OUT_DIR/NAME.txt
run_app() {
  local name=$1; shift
  printf '%s\n' "$@" | $APP_CMD > "$OUT_DIR/$name.txt" 2>&1 || { echo "app exited with an error in $name"; cat "$OUT_DIR/$name.txt"; exit 1; }
  if grep -qE "Unhandled exception|   at [A-Za-z]+\." "$OUT_DIR/$name.txt"; then echo "stack trace in $name"; cat "$OUT_DIR/$name.txt"; exit 1; fi
  LAST="$OUT_DIR/$name.txt"
}

# expect TEXT : the last run printed TEXT
expect() {
  grep -qF -- "$1" "$LAST" || { echo "FAIL: expected '$1' in $LAST"; echo "----- output -----"; cat "$LAST"; exit 1; }
  echo "  ok: '$1'"
}

# sql QUERY : one value from the e2e database
sql() { $PSQL -tA -d "$E2E_DB" -c "$1" | tr -d '\r'; }

# expect_sql QUERY VALUE
expect_sql() {
  local got; got=$(sql "$1")
  [ "$got" = "$2" ] || { echo "FAIL: $1 -> '$got' (expected '$2')"; exit 1; }
  echo "  ok: db $2 <- $1"
}
