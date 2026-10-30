# Changelog

## 1.0.3 — 2026-10-30

- Added **Toggle relative dampening** to enable dampeners and set the ship's current heading as its relative reference.
- Added **Cycle signal display** to change signal marker display modes.
- Added **Reload** to switch the selected ship weapon's ammo magazine.
- Clarified the secondary control label as **Secondary mode / Target**.
- Manually tested with Space Engineers `1.210.014` (Steam build `24675677`).

## 1.0.2 — 2026-10-23

- Compatible with Space Engineers `1.210.014` (Steam build `24675677`).

## 1.0.1 — 2026-09-27

- Declares Steam app `244850`, build `24675677` as the package's game build identity.
- Records user-confirmed manual validation for this exact game build.

## 1.0.0 - 2026-09-13

- Added the initial stable Space Engineers adapter for Steam build `24675677` (Steam app `244850`).
- Added Pulsar Legacy deployment using the adapter-owned `.NET Framework 4.8` plugin payload, Harmony runtime, descriptor metadata, launch chain, and ownership-safe uninstall.
- Added the `.NET 9` discovery adapter and SDK 1.4.0 contract metadata with canonical Steam build identity and assembly evidence.
- Added native flight input merging for pitch, roll, yaw, forward/reverse, strafe, and lift while preserving keyboard and mouse coexistence.
- Added weapons, tools, secondary mode, vehicle actions, dampeners, broadcasting, lights, parking, handbrake, power, toolbar slots, HUD, terminal/inventory, chat, and voice-chat controls.
- Added held and toggled look-around with configurable camera axes and realtime camera sensitivity.
- Added smooth analog third-person zoom with dedicated look-around zoom input and native zoom gating to prevent duplicate application.
- Added release-safe input clearing for disabled Kontrol input, focus/player changes, unavailable local control, and Pulsar unload.
- Added adapter diagnostics, connection/heartbeat reporting, game-update checks, and compatibility tests for the validated Steam build.
