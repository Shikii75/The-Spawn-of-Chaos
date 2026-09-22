# Project: The Spawn of Chaos Gameplay Feel, HUD Overhaul, Platform Fall Recovery & Combat Expansion

## Architecture
- **Player Locomotion & Physics**: `move.cs`, `PlayerShadowDashTrail.cs`, `PlayerPlatformFallManager.cs`
- **Combat & Weapons**: `MageCombat.cs`, `LumiSpearWeapon.cs`, `WeaponManager.cs`, `WeaponData.cs`, `SpearSlashVFX.cs`, `PlayerCombatJuice.cs`
- **Health & Economy**: `Health.cs`, `ProceduralOrbRenderer.cs`, `ProceduralOrbUI.cs`, `HUDOrbPanel.cs`, `HUDManager.cs`, `OrbSpawner.cs`
- **Asset Pipeline & Audit**: `Assets/Scenes/animations/frames` catalog

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Pure Shadow Trail | Eliminate blue trail emissions from LumiSpearWeapon, move.cs, juice systems; retain pure black void trail, afterimages, and LowResBlackOrb | M1 | ORIGINAL_REQUEST §R1 |
| 2 | Double Dash Distance | Double instantaneous shadow teleport dash to 11.2 units with multi-hit BoxCastAll geometry safety check | M1 | ORIGINAL_REQUEST §R2 |
| 3 | HUD Multi-Scene Persistence | Ensure HUDCanvas, HUDOrbPanel, and health units display reliably across all scene transitions | M2 | ORIGINAL_REQUEST §R4 |
| 4 | Dark Liquid Theming | Recolor Mana liquid to pure pitch-black (#000000), Health to deep dark red (#7A0909), and secondary meters to deep/dark tones | M2 | ORIGINAL_REQUEST §R4 |
| 5 | Scarce Mana Economy | Remove passive/continuous mana regen (0 MP/s); mana only restored at Shrines/SavePoints or rare items; remove mana orb mob loot leaks | M2 | ORIGINAL_REQUEST §R6 |
| 6 | Platform Fall Respawn & Penalty | Continuous safe grounded platform tracking; universal fall/hazard respawn with 25% max HP penalty | M3 | ORIGINAL_REQUEST §R5 |
| 7 | Post-Dash Heavy Single Strike | Explosive heavy single strike immediately after dash during grace window | M4 | ORIGINAL_REQUEST §R7 |
| 8 | Aerial 2-Hit Combo | Distinct mid-air strike 1 flowing into rising strike 2 with fall-damping air stall | M4 | ORIGINAL_REQUEST §R7 |
| 9 | Blob Form Combat Lockout | Strict attack lockout while in Blob Form across melee, ranged, and spear inputs | M4 | ORIGINAL_REQUEST §R7 |
| 10 | End-of-Combo Recovery Cooldown | Recovery cooldown after combo finisher preventing attack button spam | M4 | ORIGINAL_REQUEST §R7 |
| 11 | Unused NPC Frames Audit Report | Formal asset catalog report of unused NPC sprite animation frames in Assets/Scenes/animations/frames | M5 | ORIGINAL_REQUEST §R3 |
| 12 | Verification & Compilation | Clean Unity C# compilation, null safety, review, adversarial challenge, and forensic integrity audit | M6 | ORIGINAL_REQUEST Verification |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Visuals & Dash Locomotion | R1 (Pure Shadow Trail) & R2 (Double Dash with BoxCastAll) | none | IN_PROGRESS (67fe2c92) |
| M2 | HUD & Mana Economy | R4 (HUD Persistence & Liquid Theming) & R6 (Scarce Mana) | none | DONE (HUDManager, ProceduralOrbRenderer, MageCombat, OrbSpawner) |
| M3 | Platform Fall Respawn | R5 (Safe Platform Tracking & 25% HP Penalty) | none | PLANNED |
| M4 | Expanded Combat Combos | R7 (Post-Dash Strike, Aerial Combo, Blob Lockout, Recovery CD) | M1 | PLANNED |
| M5 | Unused NPC Frames Audit Report | R3 (Deliver comprehensive audit catalog) | none | DONE (Assets/Scenes/animations/frames/UNUSED_NPC_FRAMES_AUDIT.md) |
| M6 | Clean Compilation & Forensic Audit | Full project build verification, review, challenge, and audit | M1, M2, M3, M4, M5 | PLANNED |

## Interface Contracts
### `move.cs` ↔ `MageCombat.cs`
- `move.Instance.IsInPostDashWindow`: `bool` indicating player finished dash within 0.35s grace window.
- `move.Instance.TryConsumePostDashStrike()`: returns `bool`, clears the post-dash timer so only one heavy strike is fired per dash.
- `move.Instance.IsBlobForm`: `bool` indicating whether player is currently morphed in blob form. When true, all combat inputs in `MageCombat` and `LumiSpearWeapon` must be blocked.
- `move.Instance.dashDistance`: `11.2f`, guarded in `Awake()` to overwrite any stale serialized values < 10f.

### `Health.cs` ↔ `PlayerPlatformFallManager.cs`
- `Health.TakeFallPenalty(float percentOfMax = 25f)`: Deducts exactly `maxHealth * (percentOfMax / 100f)` bypassing dash invulnerability, triggers hurt audio and HUD splash, resets physics velocity, and teleports player to safe coordinates.

### `MageCombat.cs` ↔ Mana Economy
- `manaRegenRate`: `0f` (no continuous passive regeneration).
- `DrifterAltar.cs` & `DrifterSaveManager.cs`: Full refill upon save point interaction.

### `ProceduralOrbRenderer.cs` ↔ `HUDOrbPanel.cs`
- `OrbType.Mana`: Primary `#000000` (RGBA: 0, 0, 0, 255), Secondary (RGBA: 22, 22, 26, 255).
- `OrbType.Health`: Primary `#7A0909` (RGBA: 122, 9, 9, 255), Secondary (RGBA: 65, 4, 4, 255).

## Code Layout
- Player Locomotion: `Assets/Scenes/scripts/Player/move.cs`
- Platform Fall Recovery: `Assets/Scenes/scripts/Player/PlayerPlatformFallManager.cs`
- Player Health & Stats: `Assets/Scenes/scripts/Player/Health.cs`
- Player Combat: `Assets/Scenes/scripts/Player/MageCombat.cs`
- Weapons: `Assets/Scenes/scripts/Player/LumiSpearWeapon.cs`, `Assets/Scenes/scripts/Player/Weapons/`
- FX & Juice: `Assets/Scenes/scripts/Player/PlayerShadowDashTrail.cs`, `Assets/Scenes/scripts/Player/PlayerCombatJuice.cs`, `Assets/Scenes/scripts/Player/SpearSlashVFX.cs`
- HUD: `Assets/Scenes/scripts/HUDManager.cs`, `Assets/Scenes/scripts/Minigames/OrbSplash/ProceduralOrbRenderer.cs`, `Assets/Scenes/scripts/Minigames/OrbSplash/HUDOrbPanel.cs`
- Drops: `Assets/Scenes/scripts/Minigames/OrbSplash/OrbSpawner.cs`
- Audit Deliverable: `Assets/Scenes/animations/frames/UNUSED_NPC_FRAMES_AUDIT.md`
