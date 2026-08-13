# Adapter debugging

Prove each boundary in order:

1. The host selected the intended adapter and created its IPC context.
2. The adapter heartbeat reached the host and belongs to the current adapter generation.
3. The host produced the intended effective frame after mappings, curves, deadzone, and Input Control state.
4. The adapter received the frame and detected the intended changed axis, held state, or trigger edge.
5. The adapter invoked the expected game hook on the correct game thread and object instance.
6. The game accepted the state and native keyboard/mouse input still coexists as required.
7. Neutral input actively clears or submits neutral game state when the game retains prior control values.

Prefer session logs and rate-limited, opt-in debug diagnostics. Resolve log locations through platform APIs or application configuration; never add a fixed user path. Avoid repeated logging at the runtime polling rate.

When a game update breaks an adapter, compare product version plus relevant assembly SHA-256 and MVID evidence. A similar version number alone is not compatibility proof.
