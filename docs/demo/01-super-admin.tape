# SecureShift demo 01 - Super Admin: login, dashboard, users (search, add with temporary password,
# Super Admin rule, reset password, deactivate), audit logs, profile, logout.
# Record from the repository root (Windows):  vhs docs/demo/01-super-admin.tape
# Needs: the portable PostgreSQL (scripts/windows/setup-postgres-portable.ps1) and a Release build.
Output docs/demo/01-super-admin.gif
Output docs/demo/01-super-admin.mp4
Set Shell "powershell"
Set FontSize 14
Set Width 1280
Set Height 760
Set Padding 20
Set TypingSpeed 50ms
Set Framerate 12

# hidden: fresh sample database + connection string for this window
Hide
Type "powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset -SetupOnly *> $null; $env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt; $app = 'src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll'; Clear-Host"
Enter
Wait@180s
Show

Type "dotnet $app"
Enter
Wait+Screen@30s /Choose:/
Sleep 1.5s
Type "1"
Enter
Type "arya.mujumdar@secureshift.io"
Enter
Type "Super@2026"
Enter
Wait+Screen@20s /Logout/
Sleep 2s

# dashboard
Type "1"
Enter
Sleep 4s

# users: list, search, the Super Admin rule
Type "2"
Enter
Sleep 3s
Type "S"
Enter
Type "kumar"
Enter
Sleep 2.5s
Type "C"
Enter
Sleep 1s
Type "2"
Enter
Sleep 2s
Type "4"
Enter
Sleep 3s
Type "0"
Enter
Sleep 1s

# add a user: phone validation, then the "User Created" box with the temporary password
Type "A"
Enter
Type "Riya Sen"
Enter
Type "riya.sen@company.com"
Enter
Type "98765"
Enter
Sleep 1.5s
Type "9123456789"
Enter
Sleep 1s
Type "3"
Enter
Sleep 5s

# open her (page 3, row 5): reset password, deactivate
Type "N"
Enter
Type "N"
Enter
Sleep 2s
Type "5"
Enter
Sleep 2s
Type "3"
Enter
Type "y"
Enter
Sleep 4s
Type "4"
Enter
Type "y"
Enter
Type "Left project"
Enter
Sleep 3s
Type "0"
Enter
Type "0"
Enter
Sleep 1s

# audit logs, filtered by action
Type "3"
Enter
Sleep 3s
Type "A"
Enter
Sleep 1s
Type "Password Reset"
Enter
Sleep 3.5s
Type "0"
Enter

# profile
Type "4"
Enter
Sleep 3s
Type "0"
Enter

# logout, exit
Type "0"
Enter
Type "y"
Enter
Sleep 2s
Type "0"
Enter
Sleep 2s
