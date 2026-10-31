# deSwitcher

A mod loader and patcher for People Playground by [Switch Studio](https://deswitchstudio.com).

People Playground's safe mode disables C# mods. deSwitcher patches the game to load them again, and adds a protection layer: every mod is compiled and its IL is scanned for malware before it runs. Dangerous mods are held back behind an in-game warning; known-bad mods are blocked from a signed blocklist. It works on both the stable (Legacy) and beta (Registry) game builds and survives game updates (re-run the patch after an update).

## Requirements

- Windows 10/11 with .NET Framework 4.8 (preinstalled) to run.
- .NET SDK 8+ to build.
- The game's reference assemblies to build the runtime (see Build).

## Build

The runtime projects compile against the game's own assemblies, which are not in this repo. Copy the referenced DLLs from your People Playground install (`People Playground_Data\Managed\`) into `third_party\refs\beta\`. The exact files are the `HintPath` references in `src\*\*.csproj` (Assembly-CSharp, UnityEngine*, Newtonsoft.Json and a few others).

Then build and assemble the distributable:

```powershell
powershell -ExecutionPolicy Bypass -File build\pack.ps1
```

This builds the projects in Release and writes the launcher to `out\`: `deSwitcher.exe` next to its `payload\` folder. Ship the whole `out\` folder together.

## Run

Run `out\deSwitcher.exe` and press "Enable mods". The exe needs the adjacent `payload\` folder. If the game is in Program Files, Windows prompts for admin to patch it.

On the beta (Registry) build, mods compile in the main menu (progress in the bottom-left corner). If you enter a map before it finishes, re-enter the map to apply the late mods. Re-press "Enable mods" after every deSwitcher or game update.

## Layout

- `src\Patcher`, `src\Patcher.Logic` - WPF launcher and its logic.
- `src\Runtime`, `src\Modes`, `src\Legacy` - injected runtime: mod loading, scanning, the in-game warning. `Modes` is beta (Registry), `Legacy` is stable.
- `src\Core` - shared scanner, guard lists and signatures.
- `src\Compiler` - Roslyn mod compiler.
- `src\Boot`, `src\Shim` - hook bootstrap and compile shim.
- `build\pack.ps1` - builds and assembles the distributable.
