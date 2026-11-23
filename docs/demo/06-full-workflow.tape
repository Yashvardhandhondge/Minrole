# SecureShift demo 06 - the full workflow in one session, then a live database change:
#   1. Super Admin adds an Architect   2. Admin registers + classifies an enterprise, adds an application + a dependency
#   3. Architect assesses, simulates, creates and submits a plan   4. Executive approves   5. Cloud Admin runs an analysis
#   6. while the app is open, psql changes a row: the next screen shows it (every screen reads fresh from the DB)
# Record from the repository root (Windows):  vhs docs/demo/06-full-workflow.tape
Output docs/demo/06-full-workflow.gif
Output docs/demo/06-full-workflow.mp4
Set Shell "powershell"
Set FontSize 14
Set Width 1280
Set Height 760
Set Padding 20
Set TypingSpeed 40ms
Set Framerate 10

Hide
Type "powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset -SetupOnly *> $null; $env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt; $env:PGPASSWORD = Get-Content C:\SecureShiftTools\postgres-password.txt; $app = 'src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll'; Clear-Host"
Enter
Wait@180s
Show

Type "dotnet $app"
Enter
Wait+Screen@30s /Choose:/

# ---- 1. Super Admin adds an Architect
Type "1"
Enter
Type "arya.mujumdar@secureshift.io"
Enter
Type "Super@2026"
Enter
Wait+Screen@20s /Logout/
Type "2"
Enter
Sleep 1s
Type "A"
Enter
Type "Nisha Verma"
Enter
Type "nisha.verma@company.com"
Enter
Type "9811223344"
Enter
Type "3"
Enter
Sleep 4s
Type "0"
Enter
Type "0"
Enter
Type "y"
Enter
Sleep 1s

# ---- 2. Admin: enterprise + EMSI + application + dependency
Type "1"
Enter
Type "sachin.kumar@secureshift.io"
Enter
Type "Admin@2026"
Enter
Wait+Screen@20s /Logout/
Type "2"
Enter
Type "Tailspin Airlines"
Enter
Type "8"
Enter
Type "Pune"
Enter
Type "Kavita Desai"
Enter
Type "kavita.desai@tailspin.com"
Enter
Type "9812300011"
Enter
Sleep 1.5s
Type "5"
Enter
Type "12"
Enter
Type "20"
Enter
Type "8"
Enter
Type "250000"
Enter
Type "3"
Enter
Type "400"
Enter
Type "2"
Enter
Type "1"
Enter
Type "2"
Enter
Type "30"
Enter
Type "1,3"
Enter
Type "2,4"
Enter
Type "3"
Enter
Type "4"
Enter
Type "3"
Enter
Type "2"
Enter
Sleep 4s
Type "6"
Enter
Type "R"
Enter
Type "Flight Booking"
Enter
Type "Sales"
Enter
Type "Web Team"
Enter
Type "1"
Enter
Type "3"
Enter
Type "1"
Enter
Type "Java 17"
Enter
Type "1"
Enter
Type "6"
Enter
Type "1"
Enter
Type "2"
Enter
Type "300000"
Enter
Type "4"
Enter
Type "1"
Enter
Type "1"
Enter
Type "1"
Enter
Type "1"
Enter
Sleep 2s
Type "0"
Enter
Type "7"
Enter
Type "A"
Enter
Type "2"
Enter
Type "1"
Enter
Type "Booking Database"
Enter
Type "L"
Enter
Type "1"
Enter
Type "2"
Enter
Type "4"
Enter
Type "Stores bookings"
Enter
Sleep 3s
Type "0"
Enter
Type "0"
Enter
Type "y"
Enter
Sleep 1s

# ---- 3. Architect: assess Flight Booking, simulate Refactor, create + submit the plan
Type "1"
Enter
Type "amanjeet.sharma@secureshift.io"
Enter
Type "Architect@2026"
Enter
Wait+Screen@20s /Logout/
Type "2"
Enter
Type "4"
Enter
Type "4"
Enter
Type "11"
Enter
Type "1"
Enter
Sleep 3s
Type "2"
Enter
Type "3"
Enter
Sleep 4s
Type "1"
Enter
Sleep 3s
Enter
Type "First Tailspin plan"
Enter
Type "2"
Enter
Type "y"
Enter
Sleep 2.5s
Type "0"
Enter
Type "y"
Enter
Sleep 1s

# ---- 4. Executive approves
Type "1"
Enter
Type "shivanand.kumatekar@secureshift.io"
Enter
Type "Exec@2026"
Enter
Wait+Screen@20s /Logout/
Type "1"
Enter
Sleep 2s
Type "1"
Enter
Sleep 3s
Type "1"
Enter
Type "Approved for Tailspin"
Enter
Type "y"
Enter
Sleep 2.5s
Type "0"
Enter
Type "0"
Enter
Type "y"
Enter
Sleep 1s

# ---- 5. Cloud Admin runs an analysis from the sample JSON files
Type "1"
Enter
Type "aarav.mehta@secureshift.io"
Enter
Type "Cloud@2026"
Enter
Wait+Screen@20s /Logout/
Type "3"
Enter
Type "1"
Enter
Type "4"
Enter
Type "08/12/2026"
Enter
Type "11/09/2026"
Enter
Type "samples\governance\role-assignments.json"
Enter
Type "samples\governance\activity-log-90d.json"
Enter
Type "samples\governance\roles-catalog.json"
Enter
Sleep 2s
Type "y"
Enter
Sleep 4s
Type "0"
Enter
Type "y"
Enter
Sleep 1s
Type "0"
Enter
Sleep 1.5s

# ---- 6. live change: psql updates a row while the app is open
Type "# Live change: in 12 seconds a teammate renames Tejas Pathak with psql, while the app is open"
Enter
Type `Set-Content $env:TEMP\live-change.sql 'UPDATE dev."Users" SET "FullName" = ''Tejas Pathak Live'' WHERE "UserId" = 3;'`
Enter
Type `$job = Start-Job { Start-Sleep 12; & C:\SecureShiftTools\pgsql\bin\psql.exe -h 127.0.0.1 -p 5434 -U postgres -d secureshift -f $env:TEMP\live-change.sql }`
Enter
Sleep 1s
Type "dotnet $app"
Enter
Wait+Screen@30s /Choose:/
Type "1"
Enter
Type "arya.mujumdar@secureshift.io"
Enter
Type "Super@2026"
Enter
Wait+Screen@20s /Logout/
Type "2"
Enter
Sleep 18s
# redraw the list: the change made in psql shows at once
Type "C"
Enter
Sleep 4s
Type "0"
Enter
Type "0"
Enter
Type "y"
Enter
Sleep 1s
Type "0"
Enter
Sleep 1s
Type "Receive-Job $job -Wait"
Enter
Sleep 3s
