# Flydigi Key Mapper

Map the **Flydigi Vader 5S** extra buttons **C, Z, LM, RM, M1 and M2** to keyboard or numpad keys, including while a game is focused. Normal controller mappings are left alone.

[Download the Windows installer](https://github.com/notjacobwashere/FlydigiKeyMapper/releases/latest) · [Report a problem](https://github.com/notjacobwashere/FlydigiKeyMapper/issues)

## Install

1. Install [Flydigi Space Station for Xbox from Microsoft Store](https://apps.microsoft.com/detail/9NB9DQG1BN6P). Its component is required; Flydigi's proprietary files are not included in our downloads.
2. Run `FlydigiKeyMapper-Setup-<version>-x64.exe`. Setup installs the app and offers the official signed **USBPcap 1.5.4.0** USB filter if it is missing. A separate .NET installation is unnecessary.
3. If setup installed USBPcap, save your work and restart Windows. Setup never restarts Windows automatically.
4. Close Flydigi's Store app and any other controller capture tools. Connect **one Vader 5S by USB** and open Flydigi Key Mapper from Start. Administrator approval is required for USB capture.

The app and installer are currently unsigned; their publisher is not verified by Windows. Release checksums are provided in `SHA256SUMS.txt`. USBPcap's original installer and driver retain their own signatures. Never disable Windows security or driver-signature checks to use this app.

## Map buttons

* Click **Listen** beside a row and press the keyboard/numpad key to assign, or use its dropdown. Listening captures one key; Cancel, switching away, or waiting 15 seconds stops listening.
* Buttons light green while detected. Select **Unassigned** to disable a mapping. New installations start with every key unassigned.
* Bind the selected keyboard keys in GTA or another game. **Only while GTA / FiveM is focused** is optional. Leave it unchecked for a Notepad test.
* Enable **Start with Windows (in tray)** if wanted. It creates an elevated scheduled task for your account at sign-in. Closing the window leaves it running; use the tray menu's **Exit** to stop it.

Mappings are stored in `%LOCALAPPDATA%\FlydigiKeyMapper\settings.json`, separately from installation files, so upgrades preserve them. A portable build can migrate an adjacent `settings.json` on first launch. Uninstall keeps per-user mappings and the shared USBPcap driver; remove USBPcap separately through Windows Installed Apps if it is no longer needed.

## USB auto-detection

No channel or USB address is hardcoded. On connection and after a stream timeout, the app enumerates USBPcap channels and queries their USB hub trees for **VID `37D7`, PID `2801`**. It uses the device's actual USB address, including through external hubs, without relying on translated device names or confusing another Xbox controller with the Flydigi.

Only the matched physical controller is captured. Capture bytes are decoded in memory; the app does not save USB captures or capture keyboard USB devices. A stale stream releases mapped keys and triggers discovery again.

The app loads Flydigi's command component from the user's installed Store app, importing its required files into a local installation cache because WindowsApps may prevent direct loading. No Flydigi binaries are redistributed. The component sends the observed stream-start/heartbeat commands. USBPcap receives the button reports in the background. The bridge does **not** use undocumented XboxGIP subscriptions or patch any driver.

## Updates

Open the **Updates** tab and select **Check for updates**. It checks the latest stable public release in this repository, displays its version and notes, and enables **Download installer** for a newer version with a matching installer asset. Download opens the official GitHub asset; the app does not silently execute an update. Exit the mapper before running a newer installer. Future releases use `v<major>.<minor>.<patch>` tags.

## Compatibility and limits

* Windows x64, Vader 5S USB mode, one matching controller at a time. Other Flydigi models, wireless/Bluetooth connections and multiple Vader 5S controllers are not supported by this release.
* The observed firmware stream reports one active button identity. Simultaneous extra-button combinations are not guaranteed.
* USBPcap adds a system USB filter and needs a restart. It has been exercised on the developer's Windows Insider PC; broad compatibility across PCs is not established. This is an early community release.
* The original experimental private-driver reader was withdrawn after a watchdog crash. It is not included in this repository or installer.
* A game's support for mixed controller and simulated keyboard input can vary. Anti-cheat compatibility is not established.

If discovery fails, close other mapper/capture processes, confirm the official Store app is installed, check that the controller is connected by USB, and restart after installing USBPcap. The **Device report** button creates a local report; review it before sharing because it can contain device identifiers.

## Build and release

Requires Windows x64, **.NET 10 SDK**, Visual Studio C++ tools with a Windows SDK, **NSIS 3.x** and **7-Zip**. Proprietary Flydigi files are not needed to compile.

```powershell
./scripts/build.ps1 -Makensis 'C:\Program Files (x86)\NSIS\makensis.exe'
```

The script runs offline parser/updater tests, publishes a self-contained runtime, compiles the minimal native bridge, validates pinned dependency hashes and the USBPcap installer signature, includes matching USBPcap source/licenses, and creates the installer and checksums in `artifacts/`. It generates an exact uninstall file manifest instead of recursively deleting the install directory.

The GitHub workflow builds pull requests and `main`, and publishes an installer when a matching `v*` tag is pushed. Change the project version before tagging. A signing certificate is not configured; do not claim a signed app release until one is configured and verified.

## License

Our code is [MIT licensed](LICENSE). [Third-party notices](THIRD-PARTY-NOTICES.md) cover USBPcap, Microsoft runtime/interoperability files and the separate Flydigi dependency. Each installer includes USBPcap's complete matching source archive and license texts; they are also release assets.
