# CineKit

CineKit is a detached cinematic free-camera mod for SPT 4.0.

## Features

- Moves EFT's active raid camera while leaving it in its original hierarchy
- FieldKit-style dark GUI with a configurable accent color
- Simple Freecam checkbox
- Configurable speed, boost multiplier, mouse sensitivity, and menu hotkey
- Drives EFT's existing raid camera so lighting, weather, post-processing,
  thermal state, and culling remain owned by the game
- Restores the original camera and view state when disabled
- Uses EFT's third-person player rendering so the full local body is visible
  while world culling follows the detached camera
- Redirects EFT's Perfect Culling and indoor/environment observers to the
  free-camera position
- Leaves AA, textures, LODs, shadows, render distance, post-processing, and
  GPU-instancer settings entirely under EFT's control

## Installation

Extract `dist/CineKit-1.1.0.zip` into the SPT installation directory. It installs:

```text
BepInEx/plugins/Hysocs-CineKit/CineKit.dll
```

## Usage

1. Enter a raid and press `HOME`.
2. Check `Freecam`.
3. Press `HOME` again to close the menu and control the camera.

Controls: `WASD` moves, the mouse looks, `Space`/`Left Ctrl` moves up/down, and
`Left Shift` boosts movement speed. Open the menu and uncheck `Freecam` to
return to the player camera.

When FieldKit is installed with its default `HOME` ESP hotkey, CineKit moves
that ESP hotkey to `END` so opening CineKit does not hide FieldKit ESP.

## Identity

- Client GUID: `com.hysocs.cinekit`
- Client plugin name: `Hysocs-CineKit`
- Version: `1.1.0`

## Building

```powershell
dotnet build CineKit.sln -c Release -p:SkipDeploy=true
```

This creates `dist/CineKit-1.1.0.zip`. Omit `SkipDeploy=true` to also copy the
DLLs into the active SPT installation.

## License

CineKit is licensed under the [Apache License 2.0](LICENSE).
