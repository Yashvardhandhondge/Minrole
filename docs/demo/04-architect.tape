# SecureShift demo 04 - Architect: dashboard, assessment (Technical Debt + Governance Risk factors),
# simulation (Replatform), create the plan (15/20/45/rest phases) and submit it, My Plans.
# Record from the repository root (Windows):  vhs docs/demo/04-architect.tape
Output docs/demo/04-architect.gif
Output docs/demo/04-architect.mp4
Set Shell "powershell"
Set FontSize 14
Set Width 1280
Set Height 760
Set Padding 20
Set TypingSpeed 50ms
Set Framerate 12

Hide
Type "powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset -SetupOnly *> $null; $env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt; $app = 'src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll'; Clear-Host"
Enter
Wait@180s
Show

Type "dotnet $app"
Enter
Wait+Screen@30s /Choose:/
Sleep 1s
Type "1"
Enter
Type "amanjeet.sharma@secureshift.io"
Enter
Type "Architect@2026"
Enter
Wait+Screen@20s /Logout/
Sleep 2s

# dashboard
Type "1"
Enter
Sleep 4s

# assess Customer360
Type "4"
Enter
Sleep 2s
Type "2"
Enter
Sleep 2s
Type "1"
Enter
Sleep 5s

# continue to simulation: Replatform
Type "2"
Enter
Sleep 2s
Type "2"
Enter
Sleep 5s

# create the plan from this simulation and submit it
Type "1"
Enter
Sleep 5s
Enter
Type "Lift to a managed .NET platform first"
Enter
Sleep 1s
Type "2"
Enter
Sleep 2s
Type "y"
Enter
Sleep 3s

# My Plans
Type "7"
Enter
Sleep 3s
Type "1"
Enter
Sleep 5s
Type "0"
Enter

Type "0"
Enter
Type "y"
Enter
Sleep 1.5s
Type "0"
Enter
Sleep 2s
