# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

HW-Heroes is a networked multiplayer team-based arena shooter built with **Unity 2022.3.62f3** and **Photon Fusion** for networking. Teams compete to capture and hold objective zones. The project uses URP (14.0.12), New Input System (1.14.0), and Cinemachine (2.10.3).

## Unity MCP Integration

This project uses `com.coplaydev.unity-mcp` for Claude integration. Key MCP workflows:

- **Read state with resources** (editor_state, project_info, scene hierarchy)
- **Mutate with tools** (manage_gameobject, manage_script, manage_components, manage_scene)
- After creating/modifying scripts, use `read_console` to check for compilation errors before proceeding
- Poll `editor_state` resource's `isCompiling` field to verify domain reload completion
- Always include Camera and Directional Light when setting up new scenes

## Architecture

### Networking Model

Photon Fusion tick-based simulation with server-authoritative state:
- **State Authority** (server): runs physics, spawning, damage resolution
- **Input Authority** (client): each client controls their own agent via `INetworkInput`
- `[Networked]` properties sync automatically; RPCs go client→server via `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]`
- Input is accumulated every frame in `BeforeUpdate()` and transmitted once per tick in `OnInput()`

### Core Object Hierarchy

```
GameManager (INetworkRunnerCallbacks) → spawns Player on join
  └─ Gameplay → manages game loop, scoring, respawns, team assignment
       └─ Player (networked) → team, character selection, RPC_SelectCharacter
            └─ PlayerAgent → SimpleKCC movement, Weapons, Health, PlayerInput, abilities
```

### Context Pattern

`SceneContext` is the central hub referenced by all networked components via `ContextBehaviour` base class. It holds references to ObjectCache, GeneralInput, Camera, Gameplay, NetworkRunner, and the local player's agent.

### Character System (3 Heroes)

Characters are swapped mid-game via `Player.RPC_SelectCharacter(index)` → `Gameplay.RequestCharacterSwitch()`. Each character is a separate `PlayerAgent` prefab with unique weapons and abilities:

- **Cohen** (DPS): Shrink ability (55% size, 3s), ricochet projectile, explosive ultimate. Primary: kinematic explosion projectile.
- **Goedde** (Mobility): Phase-dash (4m, invulnerable, 2s) with wall-slide and ground snap, flamethrower spray, hitscan rifle primary. Ultimate: enraged form with 3 book traps (damage + stun) and damage boost. 
- **Theiss** (Tank): Shield wall, buff ability (1.5x speed, 1.4x jump, +50 HP for 5s), damage debuff field.

Character switching is only allowed in spawn areas (gated by `DisappearWhenPlayerNotInArea.IsLocalPlayerInside`), triggered by Tab key. Switching has a 1-second cooldown and is blocked while dead.

### Ability System

All abilities extend `AbilityBase` (which extends `ContextBehaviour` and implements `IAbility`). This provides shared cooldown management, `ReduceCooldownSeconds()`, `DespawnActiveObjectIfAny()`, `GetHorizontalAimForward()`, and `ValidateCanAct()`.

**Interfaces:**
- `IAbility` — read-only interface with `Slot`, `IsActive`, `IsOnCooldown`, `IsReady`, `HasDuration`, cooldown/duration times. Used by UI widgets to display any ability without knowing its concrete type.
- `IUltimateAbility` extends `IAbility` — adds `AccelerateCooldownFromDamage()`. Used by `HitUtility` to accelerate ultimate cooldowns on damage dealt.
- `EAbilitySlot` — `Movement`, `RightClick`, `Ultimate`. Each ability declares its slot.

**Adding a new character:** Create 3 new ability classes extending `AbilityBase` (or `AbilityBase` + `IUltimateAbility` for the ultimate). Set the `Slot` property. No UI code changes needed — `UIGameplayView` discovers abilities via `GetComponents<IAbility>()` and buckets by slot.

### Goedde Phase-Dash (GoeddeMovementAbility)

Goedde's movement ability is a phase-dash that uses a two-phase collision sweep:

1. **Back-stepped CapsuleCast**: The cast origin is offset *backward* by `capsuleRadius + 0.05f` to detect walls the player is pressed against. Unity's `CapsuleCast` ignores colliders the capsule starts inside, so this back-step prevents the wall-phasing exploit.
2. **Wall slide**: When hitting a wall at an angle, remaining distance is projected onto the wall tangent plane with a second CapsuleCast, letting the player slide along surfaces.
3. **Minimum travel gate**: If the resolved distance is below `_minTravelDistance` (0.3m), activation silently fails — no cooldown consumed, no phase triggered.
4. **AnimationCurve slide**: Dash motion uses a serialized `AnimationCurve` for snappy acceleration/deceleration.
5. **Ground snap**: A downward raycast each tick keeps the character on terrain during the dash.
6. **VFX hooks**: Optional serialized `ParticleSystem` fields for entry burst, exit burst, and trail particles (in addition to the existing `_visual` and `_phaseEffect` toggles).

### Goedde Book-Trap Ultimate (GoeddeUltimateAbility)

Goedde's ultimate enters an enraged state and summons 3 book traps from the floor:

1. **Enraged state**: Duration-based (8s). While active, all outgoing damage is multiplied (`_enragedDamageMultiplier`). Tracked via static `_enragedMultipliers` dictionary queried by `HitUtility.ProcessHit`.
2. **Book traps**: 3 rectangular zones placed in a fan pattern (center, left, right at `_trapSpreadAngle`). After `_snapDelay` (0.8s), books "snap shut" dealing `_trapDamage` (80) and applying stun (`_stunDuration`, 2s) to all enemies inside via `Physics.OverlapBox`.
3. **Stun system**: `PlayerAgent.ApplyStun(float)` sets a `[Networked] TickTimer`. While stunned, `InputBlocked` is true (client stops sending input) and `ProcessMovementInput` zeroes velocity (server enforcement).
4. **Visuals**: Procedural cube GameObjects animate rising from the floor (SmoothStep ease), flash on snap. Character gets emissive glow via `MaterialPropertyBlock`. Optional `_enragedEffect` GameObject toggle (e.g. particle for glowing eyes).
5. **No prefab required**: Book trap visuals are created client-side in `Render()`. All damage logic runs server-side in `FixedUpdateNetwork()`.

### Weapon System

`Weapons` manages an array of `Weapon` slots per agent. Each `Weapon` contains `WeaponAction` children with composable `WeaponComponent` pieces (barrel, trigger, magazine, spray, beam, effects).

Projectile types: **Hitscan** (instant trace), **Kinematic** (physics-based with acceleration), **Homing** (target-tracking), **Spray** (spread pattern). Each type has a networked buffer for tick-aligned processing.

### Damage & Health

`HitData` struct carries damage info (action, amount, direction, instigator, target, type). `Health` component manages HP with networked state, immortality timer (3s on respawn), and max health bonuses from buffs. Friendly fire is disabled (`FRIENDLY_FIRE_ENABLED = false`). `HitUtility.ProcessHit` applies Theiss debuff and Goedde enraged multipliers before dealing damage. `PlayerAgent.ApplyStun()` provides a server-authoritative stun that blocks movement and input.

### Game Mode: Capture Point

`CapturePoint` zones detect team majority inside, awarding points every 0.5s tick to the controlling team. First team to 100 points wins. Zone color reflects controller (blue/red/gray).

### Spawning & Respawn

Team-filtered `SpawnPoint` components. On death: 3-second delay → new agent spawns with 3-second immortality. Character switch: despawn old agent → 0.15s delay → respawn new character at same position.

## Key Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Game/GameManager.cs` | Fusion callbacks, player spawning on join |
| `Assets/Scripts/Game/Gameplay.cs` | Game loop, scoring, respawns, character switching |
| `Assets/Scripts/Player/Player.cs` | Networked player identity, team, character selection RPC |
| `Assets/Scripts/Player/PlayerAgent.cs` | Character controller, movement, camera, abilities |
| `Assets/Scripts/Player/PlayerInput.cs` | Input accumulation and network transmission |
| `Assets/Scripts/Weapons/Weapons.cs` | Weapon slot management and switching |
| `Assets/Scripts/Health/Health.cs` | Health, damage, immortality, death |
| `Assets/Scripts/Game/CapturePoint.cs` | Objective zone scoring |
| `Assets/Scripts/Utility/SceneContext.cs` | Central scene data hub |
| `Assets/Scripts/Player/GoeddeMovementAbility.cs` | Goedde's phase-dash with wall detection/sliding |
| `Assets/Scripts/Player/GoeddeUltimateAbility.cs` | Goedde's book-trap ultimate with enraged state + stun |
| `Assets/Scripts/Health/HitUtility.cs` | Central hit pipeline, damage multipliers, ultimate charge |
| `Assets/Scripts/Player/GoeddeRifleEquip.cs` | Ctrl+Shift+G runtime rifle equip for Goedde |

## Testing

Unity Test Framework is available. Run tests via Unity Editor (Window > General > Test Runner) or through MCP:
```
mcp: run_tests with mode="EditMode" or mode="PlayMode"
mcp: get_test_job to poll results
```

## Multiplayer Testing

One player enters a room name and presses "Start Host". Others enter the same room name and press "Start Client".

## External References

- [Character abilities spreadsheet](https://docs.google.com/spreadsheets/d/1JAX8BJoPRpNAQsH4tiXJRVe0GxBT1-DG-yVf_mY8Rkw/edit?usp=sharing)
- [Design spec (Figma)](https://www.figma.com/board/89CE58n7rvGjobqVV8o2S5/HW-Heroes?node-id=1-212)
- [Character tutorial video](https://drive.google.com/file/d/1CE_AyMLigCBnkJ65bBsSOVyhrFXjHEUB/view?usp=sharing)
