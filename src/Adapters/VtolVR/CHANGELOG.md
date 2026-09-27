# Changelog

## 0.1.0

- Add an initial VTOL VR adapter schema and Mod Loader deployment description for six flight axes.
- Add the separate game-side Mod Loader payload project; local game/loader references are required to compile it.
- Add Kontrol deployment to the VTOL VR Mod Loader Mods folder and launch the Mod Loader through Steam.
- Detect the Steam-installed Mod Loader beside VTOL VR when checking deployment and launch prerequisites.
- Report the mod as loaded only after both Kontrol input hooks are registered.
- Map Kontrol flight axes to VTOL vehicle, throttle, tilt, and brake routes.
- Add an optional incremental throttle mode with an adjustable change rate; position-based throttle remains the default.
