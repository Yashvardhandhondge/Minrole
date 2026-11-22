# SecureShift – live demo for the faculty (about 10 minutes, all roles)

**Before the demo (2 minutes, not shown):** open PowerShell in the project folder and load a fresh sample database:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset -SetupOnly
$env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt
$app = "src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll"
dotnet $app smoke
```

Make the window big (about 140 x 40). Then start the app: `dotnet $app`.
Each line below is what you type, then **Enter**. Text after `#` is what you say (do not type it).
The same flow is recorded in `docs/demo/06-full-workflow.mp4`.

---

## 1. Super Admin – users without email (1.5 min)

```
1                                   # Log in
arya.mujumdar@secureshift.io
Super@2026                          # shows as **********
1                                   # Dashboard: 24 users, 20 active, 4 inactive - same as the prototype
2                                   # User Management
A                                   # Add user
Nisha Verma
nisha.verma@company.com
98765                               # wrong phone -> red error, asked again
9811223344
3                                   # Architect -> "User Created" box with the temporary password, no email
0                                   # back
3                                   # Audit Logs: "User Created", newest first
0
0                                   # Logout
y
```

## 2. Admin – enterprise, EMSI, application, dependency (2.5 min)

```
1
sachin.kumar@secureshift.io
Admin@2026
2                                   # Register New Enterprise
Tailspin Airlines
8
Pune
Kavita Desai
kavita.desai@tailspin.com
9812300011
5                                   # EMSI Classification: 12 parameters
12
20
8
250000
3
400
2
1
2
30
1,3
2,4
3
4
3
2                                   # -> EMSI 32.33, Growth, Rs 10,000 / month (formula = prototype emdt-calc.js)
6                                   # Application Inventory
R
Flight Booking
Sales
Web Team
1
3
1
Java 17
1
6
1
2
300000
4
1
1
1
1
0
7                                   # Components & Dependencies
A
2
1
Booking Database
L
1
2
4
Stores bookings
I                                   # Dependency Impact: 45 for Flight Booking
0
8                                   # Digital Twin as a text tree
0
0                                   # Logout
y
```

## 3. Architect – assess, simulate, plan (2 min)

```
1
amanjeet.sharma@secureshift.io
Architect@2026
2                                   # Choose current enterprise
4                                   # Tailspin Airlines
4                                   # Assessments
11                                  # Flight Booking (11th application)
1                                   # Run Assessment: Technical Debt + Governance Risk with every factor
2                                   # Continue to Simulation
3                                   # Refactor -> risk, effort, cost in Rs lakh, suggested order
1                                   # Create Plan: 15 / 20 / 45 / rest phases = the effort
                                    # (Enter keeps "Flight Booking Refactor Plan")
First Tailspin plan
2                                   # Submit
y                                   # one confirmation -> locked
0                                   # Logout
y
```

## 4. Executive – approve (1 min)

```
1
shivanand.kumatekar@secureshift.io
Exec@2026
1                                   # Plans awaiting review
1                                   # the new plan (newest first)
1                                   # Approve
Approved for Tailspin
y                                   # "This decision cannot be changed later."
0
3                                   # Decision history
0                                   # Logout
y
```

## 5. Cloud Admin – analysis from 3 JSON files (2 min)

```
1
aarav.mehta@secureshift.io
Cloud@2026
3                                                     # Analysis Wizard
1                                                     # Northwind Traders
4                                                     # Custom range
08/12/2026
11/09/2026
samples\governance\bad\role-assignments-missing-fields.json   # bad file first
samples\governance\activity-log-90d.json
samples\governance\roles-catalog.json                 # -> "Missing fields check failed ... record 2", nothing saved
3
1
4
08/12/2026
11/09/2026
samples\governance\role-assignments.json
samples\governance\activity-log-90d.json
samples\governance\roles-catalog.json                 # 4 checks OK
y                                                     # saved in ONE transaction
5                                                     # Identities
1                                                     # svc-webapp-deploy: score 91 and why
0
0
7                                                     # Generated Roles
1                                                     # least-privilege role JSON + rare actions
0
0
0                                                     # Logout
y
0                                                     # Exit
```

## 6. (Optional, 1 min) Live change from SQL

In a **second** PowerShell window:

```powershell
$env:PGPASSWORD = Get-Content C:\SecureShiftTools\postgres-password.txt
C:\SecureShiftTools\pgsql\bin\psql.exe -h 127.0.0.1 -p 5434 -U postgres -d secureshift -c "UPDATE dev.""Users"" SET ""FullName"" = 'Tejas Pathak Live' WHERE ""UserId"" = 3"
```

In the app (logged in as Super Admin, User Management open) type `C`: the new name shows at once, because every
screen reads fresh from the database. Put it back the same way with `'Tejas Pathak'`.

**Questions you may get:** formulas = the prototype JavaScript (tests compare the console with the numbers the
prototype JS calculates); every write runs in a transaction with an audit row; no email anywhere; the faculty DB uses
`db baseline` (never setup / seed).
