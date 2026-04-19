# Kaya for Windows (WPF)

A bookmarking and notes app with server sync. WPF port of the GTK version, built with .NET 9.

## TODO

* [ ] Consider applying to https://signpath.org/apply

## Prerequisites

1. **Install .NET 9 SDK** from https://dotnet.microsoft.com/download/dotnet/9.0

   Verify installation:
   ```powershell
   dotnet --version
   ```

2. **Enable PowerShell script execution** (run PowerShell as Administrator):
   ```powershell
   Set-ExecutionPolicy RemoteSigned
   ```

## Build and Run

```powershell
.\build.ps1 build     # Build the solution
.\build.ps1 test      # Run all tests
.\build.ps1 run       # Launch the app
.\build.ps1 clean     # Clean build artifacts
.\build.ps1 publish   # Publish a release build
```

Or use `dotnet` commands directly:

```powershell
dotnet build
dotnet test
dotnet run --project src/Kaya.Wpf
```

## Data Storage

All data is stored in `%USERPROFILE%\.kaya\`:

| Directory | Contents |
|-----------|----------|
| `.kaya/anga/` | Bookmarks (`.url`) and notes (`.md`) |
| `.kaya/meta/` | Metadata files (`.toml`) |
| `.kaya/words/` | Vocabulary/word sync data |
| `.kaya/settings.json` | App settings (server URL, email, sync state) |

Refresh tokens are stored in Windows Credential Manager (target: `Save Button Refresh Token`), not on disk. Access tokens live in memory only.

## URL scheme registration

The `savebutton://` URL scheme (used for OAuth sign-in callbacks) is registered automatically on every primary-instance startup — no action required. The registration is written to `HKCU\Software\Classes\savebutton` and points at the currently running executable, so dev builds (`dotnet run`) and installed builds each register themselves.

To remove the entry manually (e.g. after uninstall or to re-point):

```powershell
reg delete HKCU\Software\Classes\savebutton /f
```

## Project Structure

```
├── src/
│   ├── Kaya.Core/        # Models and services (no UI dependency)
│   └── Kaya.Wpf/         # WPF application
├── tests/
│   └── Kaya.Tests/       # xUnit tests
├── bin/
│   └── SvgToIco/         # SVG-to-ICO conversion utility
└── build.ps1
```

## Theming

The app uses the .NET 9 Fluent theme with `ThemeMode="System"`, which automatically follows the Windows dark/light mode setting.
