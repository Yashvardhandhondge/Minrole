# SecureShift demo 05 - Executive: plans awaiting review, review a plan, approve it, reject another one
# (a reason of at least 5 characters is required), decision history.
# Record from the repository root (Windows):  vhs docs/demo/05-executive.tape
Output docs/demo/05-executive.gif
Output docs/demo/05-executive.mp4
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
Type "shivanand.kumatekar@secureshift.io"
Enter
Type "Exec@2026"
Enter
Wait+Screen@20s /Logout/
Sleep 2s

# plans awaiting review -> the first one -> approve
Type "1"
Enter
Sleep 3s
Type "1"
Enter
Sleep 6s
Type "1"
Enter
Type "Go ahead in Q1"
Enter
Sleep 1.5s
Type "y"
Enter
Sleep 3s

# the next one -> reject: a reason is required
Type "1"
Enter
Sleep 3s
Type "2"
Enter
Type "no"
Enter
Sleep 2s
Type "Too costly this year"
Enter
Sleep 1s
Type "y"
Enter
Sleep 3s
Type "0"
Enter

# decision history
Type "3"
Enter
Sleep 4s

Type "0"
Enter
Type "y"
Enter
Sleep 1.5s
Type "0"
Enter
Sleep 2s
