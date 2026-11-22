# SecureShift demo 03 - Admin (Modernization): dashboard, register an enterprise, EMSI classification,
# register an application, add a component and a dependency, dependency impact, digital twin, enterprise details.
# Record from the repository root (Windows):  vhs docs/demo/03-admin-modernization.tape
Output docs/demo/03-admin-modernization.gif
Output docs/demo/03-admin-modernization.mp4
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
Type "sachin.kumar@secureshift.io"
Enter
Type "Admin@2026"
Enter
Wait+Screen@20s /Logout/
Sleep 2s

# dashboard
Type "1"
Enter
Sleep 4s
Type "0"
Enter

# register a new enterprise (it becomes the current enterprise)
Type "2"
Enter
Sleep 1s
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
Sleep 2.5s

# EMSI classification: 12 parameters
Type "5"
Enter
Sleep 1s
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
Sleep 6s

# register an application
Type "6"
Enter
Sleep 2s
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
Sleep 3s
Type "0"
Enter

# a component and a dependency, then the dependency impact
Type "7"
Enter
Sleep 2s
Type "A"
Enter
Type "2"
Enter
Type "1"
Enter
Type "Booking Database"
Enter
Sleep 2s
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
Sleep 2.5s
Type "I"
Enter
Sleep 3.5s
Type "0"
Enter

# digital twin of the new enterprise, then Northwind Bank
Type "8"
Enter
Sleep 3s
Type "E"
Enter
Type "1"
Enter
Sleep 5s
Type "0"
Enter

# enterprise details (Northwind Bank is now the current enterprise)
Type "4"
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
