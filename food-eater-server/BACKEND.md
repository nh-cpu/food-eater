# Sandwich Delivery Backend

This SpacetimeDB module is the authoritative backend for a shared co-op
sandwich delivery run. Clients submit player intent; the server owns movement,
carrying, topping state, recovery checks, and completion.

## Public State

- `config`: Tunable world and gameplay constants.
- `player`: Connected players, their fixed sandwich attachment offset, derived
  3D position, and steering input.
- `sandwich`: The shared sandwich position, velocity, tilt, attached player
  count, and completion state.
- `topping`: Each sandwich layer and whether it is attached, dropped, waiting
  at the summit, or placed.
- `game_event`: Transient events for UI/audio feedback.

The current prototype has one global run. A later lobby system should add a
`run_id` to gameplay tables and scope subscriptions per run.

## Client Reducers

- `enter_game(name)`: Set a validated display name.
- `update_player_input(direction)`: Submit a normalized 3D steering direction.
- `try_recover_topping(topping_id)`: Reattach a dropped topping when both the
  player and sandwich are close enough.
- `reset_run()`: Reset the shared prototype run.

`simulate` is scheduled server-side at 20 Hz. Do not call it from clients.

## Rules

- Every connected player is permanently attached to the sandwich at a fixed
  offset. Player positions are derived from the authoritative sandwich position.
- Player inputs collectively steer the sandwich.
- Disagreement between player inputs tilts the sandwich.
- Excessive tilt or a hard ground impact drops the highest attached topping.
- Dropped toppings fall and land under server-authoritative lightweight gravity.
- Dropped toppings must be brought near the sandwich and recovered.
- Reaching the summit completes the run only when all delivery toppings remain
  attached. The server then places the waiting top bread.

## Local Workflow

From `food-eater-server`:

```powershell
spacetime build
spacetime publish food-eater --server local --delete-data always --yes
spacetime generate --lang csharp --out-dir ../module_bindings
```

Regenerate bindings after every schema or reducer signature change. The
existing Godot controllers still target the old agar-style bindings and must be
updated before regenerating them in this branch.
