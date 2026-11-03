# Changelog

## 1.2.0
- **Desktop widget:** an always-visible live monitor you can drag anywhere, with keep-on-top and opacity options.
- **Themes:** replaced the gradient look with flat Dark, Light, Cyberpunk, Intel and AMD themes.
- **Tray icons:** redrawn with hinted text and now show units under the value (%, °C/°F, B/s, KB/s, MB/s, GB/s).
- Developer credits and version info now appear only in Settings → About.
- New flat application icon.
- Installer: the Start menu shortcut is now directly in the list (no "SpecScope" subfolder); older shortcuts are cleaned up on upgrade.

## 1.1.0
- Crisper text: the UI now uses the Windows ClearType text renderer with pixel-snapped layout instead of soft anti-aliased GDI+ text.
- New Settings page: notification-area icon on/off, run in background, start with Windows, tray metric, update interval, temperature unit.
- Dynamic detail panel: live graphs for CPU (with per-core load), memory, disk, GPU, network, temperature and battery; live-updating rows (uptime, available memory, CPU load/clock).
- Tray icon redesigned to match native Windows icons (flat, monochrome, follows light/dark taskbar).
- Flyout graphs now scroll smoothly between samples.

## 1.0.0
- First installable release (NSIS installer, Add/Remove Programs entry, publisher "Tasmon Islam").
- Frosted-glass "iOS-style" interface.
- Live tray monitor: CPU load, CPU temperature, RAM usage, network upload/download; live flyout with sparklines.
- Embedded version info, DPI-aware manifest, application icon.
