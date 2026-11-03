# SpecScope

**A modern system-information viewer and live system monitor for Windows 10 / 11** – a lightweight, privacy-friendly alternative to Speccy, with a clean flat interface, five colour themes, a desktop widget and a real-time tray monitor.

> Developer & publisher: **Tasmon Islam** · [tasmon@outlook.com](mailto:tasmon@outlook.com) · Licensed under the [MIT License](LICENSE)

---

## Features

**System information** – everything in one place:

| Category | Details |
|---|---|
| Summary | One-line overview of your whole PC |
| Operating System | Edition, version, build, install date, uptime, Secure Boot |
| CPU | Model, cores / threads, clocks, caches, virtualization, load |
| RAM | Total / in use, per-module size, type (DDR3/4/5), speed, part number |
| Motherboard | Board, system model, BIOS / UEFI version and date |
| Graphics | GPU, video memory, driver version/date, monitors and refresh rate |
| Storage | Model, capacity, SSD/HDD, NVMe/SATA/USB, health, volumes and free space |
| Audio / Network / Peripherals | Sound devices, adapters (MAC, IP, gateway, DNS), input devices, printers, cameras |
| Sensors & Battery | Thermal zones (when exposed by firmware), battery state |
| Settings | Tray icon, background operation, autostart, refresh rate, units |

**Live, dynamic detail panel** – the right-hand side is a real-time dashboard, not a static report:

- **Summary** – live CPU, memory, disk and network tiles with scrolling graphs.
- **CPU** – utilisation graph, current clock speed and a per-core load bar for every logical processor.
- **RAM** – memory-usage graph with live "in use / available" values.
- **Graphics** – GPU load and dedicated GPU-memory graphs (where Windows reports them).
- **Storage** – disk active-time graph and live read/write throughput.
- **Network** – separate download and upload graphs with peak values.
- **Sensors & Battery** – temperature graph (when exposed) and battery level.
- Rows such as *Uptime*, *Available memory* and *CPU load* tick live; graphs glide smoothly between samples.

**Settings page** (bottom of the sidebar, also in the tray menu → *Settings…*):

- Show / hide the notification-area icon, run in background, start with Windows.
- **Desktop widget** on/off, keep-on-top, opacity.
- **Theme:** Dark, Light, Cyberpunk, Intel or AMD.
- What the tray icon shows, the update interval (1 / 2 / 5 s) and the temperature unit (°C / °F).
- *About*: version, developer and contact – this is the only place they appear in the app.

**Desktop widget** – a small always-visible live monitor (CPU load, CPU temperature, RAM, network ↓/↑). Drag it anywhere (the position is remembered), double-click to open SpecScope, right-click for *Keep on top*, *Opacity* and *Hide widget*. Turn it on from **Settings → Desktop widget**, the tray menu, or the *Show Widget* button.

**Themes** – five flat, solid-colour themes (no gradients) that apply to the window, flyout, widget and menus:

| Theme | Look |
|---|---|
| Dark | Neutral charcoal with a blue accent |
| Light | Neutral light grey / white with a blue accent |
| Cyberpunk | Near-black purple with neon cyan, magenta and yellow, sharper corners |
| Intel | Light blue-white with Intel blue |
| AMD | Black and grey with AMD red |

*Intel and AMD are colour-scheme homages only – SpecScope is not affiliated with or endorsed by Intel or AMD.*

**Live tray monitor** – lives in the notification area:

- A flat, monochrome icon in the style of built-in Windows tray icons: the **value on top and its unit underneath** (`%`, `°C` / `°F`, `B/s`, `KB/s`, `MB/s`, `GB/s`), with a small arrow for download / upload. It follows the taskbar's light/dark theme and turns amber/red at high load.
- **Left-click** opens a flyout with four live gauges and sparklines (CPU load, CPU temperature, RAM, network).
- **Right-click** shows current values plus options: *Desktop widget*, *Tray icon shows*, *Start with Windows*, *Run in background*, *Settings…*, *Exit*.
- Hover the icon for a quick tooltip summary.

**Other**

- Crisp ClearType text, dark/light title bar that matches the theme, rounded corners on Windows 11, per-monitor DPI awareness.
- Copy a section to the clipboard or export a full text report.
- Single instance: launching it again simply brings the existing window forward.
- No telemetry, no network access, no admin rights needed for normal use.

## Installation

1. Download **`SpecScope-Setup-1.2.0.exe`** from the [Releases](https://github.com/tasmon/SpecScope/releases) page.
2. *(Recommended)* verify the download – see [Verify your download](#verify-your-download).
3. Run the installer. It installs to `C:\Program Files\SpecScope`, adds a **SpecScope** shortcut directly in the Start menu list (no subfolder) and an optional desktop shortcut, and registers a proper uninstaller under **Settings → Apps** with *Tasmon Islam* as the publisher.

**Requirements:** Windows 10 (1607+) or Windows 11, .NET Framework 4.6.2 or newer (built into current Windows 10/11 – nothing extra to install).

To uninstall: **Settings → Apps → SpecScope → Uninstall**.

## ⚠️ Windows SmartScreen / "Unknown publisher" notice

SpecScope is an independent, open-source project and the installer is **not yet code-signed** with a paid Authenticode certificate. Because of that, Windows may show:

- **Microsoft Defender SmartScreen – "Windows protected your PC"** when you run the installer, and/or
- a UAC prompt reading **"Verified publisher: Unknown"**.

This is expected for any new, unsigned application and is **not** a sign that the file is malicious – SmartScreen warns about files it has not seen many times before. To continue:

1. Click **More info**.
2. Click **Run anyway**.

If you would rather not take our word for it:

- **Verify the checksum** (below) so you know the file is exactly what was published.
- **Read the source** – it is a small, readable C# codebase in `src/`.
- **Build it yourself** – see [Building from source](#building-from-source).

Once the installer is signed with a trusted certificate (see [Code signing](#code-signing)) the "Unknown publisher" wording goes away; SmartScreen reputation then builds up as more people install the signed release.

### Verify your download

Each release lists a SHA-256 checksum. In PowerShell:

```powershell
Get-FileHash .\SpecScope-Setup-1.2.0.exe -Algorithm SHA256
```

Compare the result with the value on the release page.

## About the CPU temperature

Windows has no universal temperature API. SpecScope reads the ACPI/WMI thermal zones (no drivers installed). Many laptops expose one; many desktops do **not**, in which case the temperature shows as **N/A**. Running as administrator can help on some systems (menu button **Run as admin**). True per-core readings require a kernel driver and are intentionally out of scope.

## Building from source

Requires only Windows' built-in .NET Framework compiler – no Visual Studio needed.

```bash
git clone https://github.com/tasmon/SpecScope.git
cd SpecScope
```

```bat
build.bat              :: -> dist\SpecScope.exe
build-installer.bat    :: -> dist\SpecScope-Setup-<version>.exe  (needs NSIS)
```

NSIS: `winget install NSIS.NSIS` (or <https://nsis.sourceforge.io>).

The code is plain **C# 5** so it compiles with the compiler that ships with Windows:

```
src/
  AssemblyInfo.cs   version + publisher metadata embedded in the exe
  Program.cs        entry point, single-instance handling
  TrayApp.cs        tray icon, menu, widget / flyout management
  TrayIcon.cs       renders the value + unit tray icon
  Widget.cs         desktop widget and tray flyout (shared live gauges)
  MainForm.cs       main window, live gauges, settings page
  Settings.cs       cached per-user settings (HKCU)
  LiveMonitor.cs    CPU / cores / RAM / disk / GPU / network / temperature sampling, settings
  Collector.cs      WMI hardware inventory
  Theme.cs          flat colour themes and drawing helpers
  Native.cs         P/Invoke helpers
  app.manifest      DPI awareness + Windows 10/11 compatibility
  app.ico           application icon
installer/SpecScope.nsi   NSIS installer script
```

## Versioning

Version metadata (File version, Product version, Company, Copyright) is embedded in the executable from `src/AssemblyInfo.cs` and is visible in **Right-click → Properties → Details**. To release a new version, update:

1. `src/AssemblyInfo.cs` (`AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion`)
2. `VERSION` in `build-installer.bat`
3. `CHANGELOG.md`

## Code signing

Setting the publisher name in metadata (done here) is what Windows shows in **Settings → Apps**. The *"Verified publisher"* line in UAC/SmartScreen, however, only appears when the file carries an **Authenticode signature** from a certificate authority. Options:

- An OV/EV code-signing certificate, or Azure Trusted Signing.
- [SignPath Foundation](https://signpath.org/) offers free signing for qualifying open-source projects.

`build-installer.bat` signs both the exe and the installer automatically when the `CERT_THUMBPRINT` environment variable is set and `signtool.exe` is available.

## Privacy

SpecScope runs entirely locally. It reads hardware and OS information through standard Windows APIs, stores its few settings under `HKCU\Software\SpecScope`, and never sends data anywhere.

## Contributing & contact

Issues and pull requests are welcome. Questions: **Tasmon Islam** – [tasmon@outlook.com](mailto:tasmon@outlook.com).

## License

[MIT](LICENSE) © 2026 Tasmon Islam
