ShutdownSteam 1.0.0 - Windows Portable

Run ShutdownSteam.exe. No installation or administrator access is required.
Requires Windows 10/11 with .NET Framework 4.x.

1. Select the Steam installation folder if it was not detected.
2. Choose a game and the action to run when its download completes.
3. Press Start Monitoring. Keep Steam and ShutdownSteam running.

The app waits to observe an incomplete-to-complete transition. Selecting an
already-installed game does not immediately execute the action.

Game pictures are read from the local Steam cache. Missing pictures use a
generic gamepad icon. No image downloads or telemetry are performed.

Settings > Test notification displays a harmless test message immediately.
Shutdown and restart have a 60-second Windows countdown. Use Settings > Cancel
scheduled shutdown, or run shutdown.exe /a, to cancel a scheduled action.
Stopping monitoring does not cancel an action already scheduled with Windows.

Progress/size are displayed when available in the Steam manifest. Speed and
remaining time are unavailable. Hibernate requires Windows hibernation support.

The executable is unsigned. The ZIP also includes license and artwork notices.
