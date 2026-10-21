# Third-party components

Flydigi Key Mapper's own code is MIT licensed. Third-party licenses remain separate.

* **USBPcap 1.5.4.0** by Tomasz Mon: driver GPL-2.0-only; command-line component BSD-2-Clause. Setup embeds the unchanged, signed official installer. The matching complete source/PDB archive and license texts are included in the installed `third-party` directory and in every release. See https://github.com/desowin/usbpcap and https://desowin.org/usbpcap/.
* **Microsoft .NET runtime**: the self-contained application includes Microsoft's runtime files and their LICENSE/THIRD-PARTY-NOTICES files. See https://github.com/dotnet/runtime and https://github.com/dotnet/windowsdesktop.
* **Flydigi Space Station for Xbox**: proprietary, separately installed from Microsoft Store. No Flydigi binaries, runtime packages, firmware, or metadata are redistributed. Minimal ABI declarations identify interfaces in the user's installed component. The mapper is independent and not endorsed by Flydigi or Microsoft.

USB discovery uses Windows' published hub information queries and USBPcap's published GET_HUB_SYMLINK query. No undocumented XboxGIP subscription, binary patch, or driver replacement is used.

