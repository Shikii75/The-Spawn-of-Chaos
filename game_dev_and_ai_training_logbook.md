# Student Learning & Implementation Logbook
**Program**: Game Development (Unity 2D & C#) & AI LLM Fine-Tuning Internship  
**Project**: *The Spawn of Chaos* & Nyxaris LLM Pipeline  
**Student / Trainee**: Game Development & AI Trainee  
**Period**: May 25, 2026 – August 21, 2026  
**Schedule**: Monday – Friday (Excluding Weekends & Public Holidays)

---

## 🎓 Learning Objectives & Project Overview

This logbook documents my hands-on learning journey and technical implementation during the game development and AI training program. Over the course of 13 weeks, I learned core 2D game architecture in Unity, object-oriented C# programming, C# dynamic physics, procedural shaders, and Python/Flask AI pipeline integration to build ***The Spawn of Chaos*** alongside fine-tuning the **Nyxaris LLM**.

### Core Technical Skills Mastered & Implemented:
1. **Unity 2D Environment & Component Systems**: `Rigidbody2D`, `Collider2D`, `MeshFilter`/`MeshRenderer`, sorting layers, parent-child hierarchies, and Editor Window script automation (`UnityEditor`).
2. **Player Physics & Game Feel**: Gravity scale dynamics, snappy jump curve tuning, button-release short hops, invulnerable dash windows, and state transformations.
3. **Procedural Math & Graphics Shaders**: Catmull-Rom cubic spline interpolation, Hooke's Law spring node simulation, multi-frequency harmonic wave equations, and HLSL fragment shaders.
4. **AI Combat & State Machines**: Enemy AI behaviors, dynamic attack range calculations, decoupled maneuver timers, and wave spawner loops.
5. **Python Backend & LLM Pipeline**: Flask routing, `rembg` background removal integration, JSONL dataset formatting, persona fine-tuning, and REST API endpoints.

---

## 📅 Calendar & Holiday Exclusions Audit

Per the official internship training calendar, the following non-working dates were excluded from log entries:
- **May 27, 2026 (Wednesday)**: Children's Day / Id el Kabir (Public Holiday)
- **May 28, 2026 (Thursday)**: Id el Kabir (Public Holiday)
- **June 12, 2026 (Friday)**: Democracy Day (Public Holiday)
- **June 16, 2026 (Tuesday)**: Al-Hijra / Islamic New Year (Public Holiday)
- **All Saturdays & Sundays**: Excluded from training logs.

---

## 📝 Student Daily Learning & Implementation Logs

### Module 1: Python Flask Basics, Rembg & Nyxaris LLM Datasets

#### Week 1: May 25, 2026 – May 29, 2026
*Focus: Environment Setup, Flask Routing & Learning Prompt Dataset Curation*

- **Mon May 25, 2026**:
  - *Learning*: Studied local Flask backend structure and learned how game pipeline assets connect to web servers.
  - *Implementation*: Set up project structure for `The Spawn of Chaos` and initialized `app.py` and `routing.py`.
- **Tue May 26, 2026**:
  - *Learning*: Learned image processing concepts with OpenCV and automated background removal using `rembg`.
  - *Implementation*: Created Flask API endpoints (`/remove-bg`, `/bulk-remove`) and formatted JSONL training datasets for Nyxaris lore responses.
- **Wed May 27, 2026**: *PUBLIC HOLIDAY — Children's Day / Id el Kabir (NO WORK)*
- **Thu May 28, 2026**: *PUBLIC HOLIDAY — Id el Kabir (NO WORK)*
- **Fri May 29, 2026**:
  - *Learning*: Explored intent classification and emotion tagging in conversational AI.
  - *Implementation*: Implemented the `/nyxaris` chat endpoint returning dynamic emotion tags (`explaining`, `thinking`, `angry`, `kind`).

---

### Module 2: Unity 2D Environment & 2D Player Controller Physics

#### Week 2: June 1, 2026 – June 5, 2026
*Focus: Unity 2D Physics, Ground Masks, Camera Framing & Jump Feel Tuning*

- **Mon Jun 1, 2026**:
  - *Learning*: Learned `Rigidbody2D` velocity movement, zero-friction physics materials, and ground collision masking.
  - *Implementation*: Built `move.cs` basic ground movement using `Physics2D.OverlapCircle` ground checking.
- **Tue Jun 2, 2026**:
  - *Learning*: Studied invulnerability frames (i-frames) and dash velocity boosts in 2D action games.
  - *Implementation*: Implemented double-tap horizontal sprint and directional invulnerable dash mechanics.
- **Wed Jun 3, 2026**:
  - *Learning*: Learned state-based character transformations and dynamic collider scaling.
  - *Implementation*: Added Blob Form transformation (`KeyCode.M`), shrinking collider height to 1.2 units for narrow tunnel crawling.
- **Thu Jun 4, 2026**:
  - *Learning*: Studied platformer game feel principles: variable jump height, gravity multipliers, and snappy fall curves.
  - *Implementation*: Tuned jump physics in `move.cs`: set `jumpForce = 15.5f`, `gravityScale = 2.8f`, added dynamic fall gravity (`1.6x`), and short-hop release multiplier (`2.0x`).
- **Fri Jun 5, 2026**:
  - *Learning*: Studied camera viewport framing and smooth exponential lerp tracking.
  - *Implementation*: Updated `CameraFollow.cs` target offset to `(0f, 0.5f, -10f)` to keep character vertically centered in viewport.

---

### Module 3: Object Slicing, Interactive Props & Minigame Engines

#### Week 3: June 8, 2026 – June 12, 2026
*Focus: Vector Physics, Interactive Props & Editor Tooling*

- **Mon Jun 8, 2026**:
  - *Learning*: Learned 2D sprite slicing concepts and dynamic force/torque impulse application.
  - *Implementation*: Built `CuttablePlant.cs` slicing plants on weapon impact, applying fragment torque, leaf bursts, and loot orb drops.
- **Tue Jun 9, 2026**:
  - *Learning*: Explored squash-and-stretch animation principles via code and pendulum physics.
  - *Implementation*: Developed `HitReactiveObject.cs` supporting training dummy spring-back, lantern swinging, and 4-quadrant debris shattering.
- **Wed Jun 10, 2026**:
  - *Learning*: Learned how to extend the Unity Editor interface using `EditorWindow` and script generation.
  - *Implementation*: Built `AIAnimationStudio.cs` under `Tools > AI Animation Studio` with preset clip generators and test bench spawner.
- **Thu Jun 11, 2026**:
  - *Learning*: Learned procedural UI liquid rendering and particle splash physics.
  - *Implementation*: Integrated `OrbSpawner.cs` with HUD containers (`ProceduralOrbRenderer.cs`) for dynamic C# liquid physics feedback.
- **Fri Jun 12, 2026**: *PUBLIC HOLIDAY — Democracy Day (NO WORK)*

---

#### Week 4: June 15, 2026 – June 19, 2026
*Focus: Arcade Minigames & 2D Enemy Combat AI Architecture*

- **Mon Jun 15, 2026**:
  - *Learning*: Studied UI view management and modular minigame registration patterns.
  - *Implementation*: Registered arcade minigames inside `MinigameHubUI.cs`.
- **Tue Jun 16, 2026**: *PUBLIC HOLIDAY — Al-Hijra / Islamic New Year (NO WORK)*
- **Wed Jun 17, 2026**:
  - *Learning*: Learned combat AI state machines, dynamic attack range calculation based on combined collider bounds, and decoupled maneuver timers.
  - *Implementation*: Developed `NormalMaleSamuraiAI.cs` for melee combat patterns.
- **Thu Jun 18, 2026**:
  - *Learning*: Studied medium-range whip attack mechanics and spacing logic.
  - *Implementation*: Developed `FemaleSamuraiWhipAI.cs`.
- **Fri Jun 19, 2026**:
  - *Learning*: Learned flying enemy pathfinding using sine-wave floating math and projectile targeting.
  - *Implementation*: Developed `NightmareOrbAI.cs` floating orb AI.

---

### Module 4: Dynamic Platform Systems & Procedural Purple Water Engine

#### Week 5: June 22, 2026 – June 26, 2026
*Focus: Dynamic Proximity Bridges & Procedural Spring Mesh Water Physics*

- **Mon Jun 22, 2026**:
  - *Learning*: Studied proximity-triggered platform assembly, scale interpolation, and `PlatformEffector2D` one-way collision.
  - *Implementation*: Created `DynamicPinkBridgeCluster.cs` with proximity activation (`6.5` units), spring scaling (`1.15x`), and neon pink glow (`#FF40BF`).
- **Tue Jun 23, 2026**:
  - *Learning*: Studied Hooke's Law spring node physics (`F = -k * x - c * v`) and wave propagation passes.
  - *Implementation*: Created initial 2D spring-mesh water surface in `PurpleWater2D.cs`.
- **Wed Jun 24, 2026**:
  - *Learning*: Learned HLSL shader writing in Unity, color gradients, specular highlights, and foam lines.
  - *Implementation*: Created `RealisticPurpleWater.shader` blending surface magenta (`#D8117E`) into deep void purple (`#240438`).
- **Thu Jun 25, 2026**:
  - *Learning*: Learned Catmull-Rom cubic spline interpolation to eliminate jagged polygonal mesh lines.
  - *Implementation*: Upgraded `PurpleWater2D.cs` surface vertex rendering with Catmull-Rom spline filtering for smooth liquid curves.
- **Fri Jun 26, 2026**:
  - *Learning*: Studied wave superposition and multi-frequency harmonic sine waves.
  - *Implementation*: Added 3 multi-frequency ambient swells (`2.2Hz`, `3.6Hz`, `1.4Hz`) to `PurpleWater2D.cs`.

---

#### Week 6: June 29, 2026 – July 3, 2026
*Focus: Water Damping Calibration & GPU Particle Splash VFX*

- **Mon Jun 29, 2026**:
  - *Learning*: Learned liquid damping calibration to prevent unnatural sloshing during player movement.
  - *Implementation*: Calibrated `PurpleWater2D.cs` physics: softened `stiffness` (`0.025f`), increased `damping` (`0.07f`), and reduced wake multipliers.
- **Tue Jun 30, 2026**:
  - *Learning*: Studied numerical stability in physics solvers through velocity clamping.
  - *Implementation*: Added node velocity (`[-0.25, +0.25]`) and displacement (`[-0.4, +0.4]`) clamps in `PurpleWater2D.cs`.
- **Wed Jul 1, 2026**:
  - *Learning*: Learned Unity `ParticleSystem` creation via code and sorting layer rendering.
  - *Implementation*: Upgraded `PurpleWaterSplashFX.cs` to use GPU-accelerated `ParticleSystem` droplet bursts.
- **Thu Jul 2, 2026**:
  - *Learning*: Studied expanding ring particle visuals and material assignments.
  - *Implementation*: Added expanding glowing pink splash ring particle overlays (`#E040FB`).
- **Fri Jul 3, 2026**:
  - *Learning*: Learned velocity vector magnitude extraction for physics trigger callbacks.
  - *Implementation*: Added `Mathf.Max(rb.linearVelocity.magnitude, 3.5f)` impact speed calculations in `PurpleWater2D.cs`.

---

### Module 5: Complete Tutorial Level Architecture & Nyxaris Freedom Arc

#### Week 7: July 6, 2026 – July 10, 2026
*Focus: Level Layout Planning, Elevation Flow & Section Construction*

- **Mon Jul 6, 2026**:
  - *Learning*: Studied level design principles: multi-tier vertical elevation flow and clear tutorial progression.
  - *Implementation*: Planned 4-section layout for `TutorialScene.unity`: Water Region -> Blob Crawlspace -> Cave Arena -> Nyxaris Peak Shrine.
- **Tue Jul 7, 2026**:
  - *Learning*: Learned placing platform geometry and connecting water chasms.
  - *Implementation*: Constructed Section 1 (Water Region) with `PurpleWater2D` and `DynamicPinkBridgeCluster`.
- **Wed Jul 8, 2026**:
  - *Learning*: Learned subterranean elevation drops and tight space layout building.
  - *Implementation*: Constructed Section 2 (Blob Crawlspace) at `Y = -6f` with 1.2-unit low ceiling tunnel.
- **Thu Jul 9, 2026**:
  - *Learning*: Studied in-world tutorial signboards and player guidance indicators.
  - *Implementation*: Placed Blob Form tutorial signboard: *"Hold M while moving to transform into Blob Form & squeeze through tight spaces!"*.
- **Fri Jul 10, 2026**:
  - *Learning*: Learned constructing low rock overhang barriers to enforce mechanics traversal.
  - *Implementation*: Added rock overhang barriers requiring Blob Form (`isBlobForm`) to pass.

---

#### Week 8: July 13, 2026 – July 17, 2026
*Focus: Cave Cavern 3-Stage Wave Arena (`TutorialCaveWaveManager.cs`)*

- **Mon Jul 13, 2026**:
  - *Learning*: Learned wave spawner management and tutorial progression triggers.
  - *Implementation*: Developed `TutorialCaveWaveManager.cs` for Section 3 cavern arena (`X = 60f..95f, Y = 4f`).
- **Tue Jul 14, 2026**:
  - *Learning*: Learned tracking active enemy lists and triggering melee tutorial banners.
  - *Implementation*: Programmed Wave 1 (Melee Trial): spawning 2 Samurai Mobs with `J` key 2-hit combo prompt.
- **Wed Jul 15, 2026**:
  - *Learning*: Learned dynamically unlocking player combat capabilities during gameplay.
  - *Implementation*: Programmed Wave 2 (Magic Unlock): unlocking `MageCombat.Instance.UnlockProjectile()`, spawning magic pedestal, and spawning 3 flying Nightmare Orbs (`K` key).
- **Thu Jul 16, 2026**:
  - *Learning*: Learned hazard barrier placement and dash dodge prompts.
  - *Implementation*: Programmed Wave 3 (Dash Dodge Trial): prompting Shift Dash (`LeftShift`) through hazard barriers.
- **Fri Jul 17, 2026**:
  - *Learning*: Learned cavern exit gate unlocking and loot drop spawning upon wave completion.
  - *Implementation*: Linked Wave 3 completion to cavern exit gate unlock and `OrbSpawner` drops.

---

#### Week 9: July 20, 2026 – July 24, 2026
*Focus: Nyxaris Divine Shrine Cage (`NyxarisShrineCage.cs`) & Narrative Sequence*

- **Mon Jul 20, 2026**:
  - *Learning*: Studied high mountain peak shrine altar design and divine aesthetic lighting.
  - *Implementation*: Designed Section 4 (Nyxaris Shrine Peak) at `X = 100f..135f, Y = 12f`.
- **Tue Jul 21, 2026**:
  - *Learning*: Learned implementing `IDamageable` interface on breakable environment objects with hit flashes.
  - *Implementation*: Created `NyxarisShrineCage.cs` (50 HP) with white/magenta material hit flash routines.
- **Wed Jul 22, 2026**:
  - *Learning*: Learned explosive particle shatter effects on object destruction.
  - *Implementation*: Implemented plasma shatter explosion sequence upon shrine HP reaching 0.
- **Thu Jul 23, 2026**:
  - *Learning*: Learned smooth NPC position lerping, floating text popups, and narrative event triggers.
  - *Implementation*: Programmed Nyxaris smooth descent, floating lore dialogue banner (*"My followers were mass-murdered... Clues point to the Cherry Blossom Forest mountain dōjōs..."*), and portal unlock.
- **Fri Jul 24, 2026**:
  - *Learning*: Learned configuring scene transition triggers (`entersign`) with target spawn point parameters.
  - *Implementation*: Configured portal to spawn player at `MountainPath_Entrance` in `MountainPathScene`.

---

#### Week 10: July 27, 2026 – July 31, 2026
*Focus: Editor Tool Automation (`TutorialLevelBuilder.cs`) & Code Quality*

- **Mon Jul 27, 2026**:
  - *Learning*: Learned writing custom Unity Editor windows using `EditorWindow` and menu items.
  - *Implementation*: Created `TutorialLevelBuilder.cs` under `Tools > Build Complete Tutorial Level`.
- **Tue Jul 28, 2026**:
  - *Learning*: Learned programmatic scene object instantiation, positioning, color tinting, and collider setup.
  - *Implementation*: Automated complete 4-section layout generation in `TutorialScene.unity`.
- **Wed Jul 29, 2026**:
  - *Learning*: Learned exposing Editor commands for AI bridge automation.
  - *Implementation*: Integrated `TutorialLevelBuilder.BuildCompleteTutorialLevel()` into `AIEditorBridge.cs` (`"build_tutorial_level"`).
- **Thu Jul 30, 2026**:
  - *Learning*: Learned diagnosing Editor-time `NullReferenceException` bugs when `Awake()` has not executed.
  - *Implementation*: Fixed `PurpleWater2D.cs` by implementing `EnsureComponents()` lazy-initialization.
- **Fri Jul 31, 2026**:
  - *Learning*: Learned static utility factory method pattern for world-space UI text popups.
  - *Implementation*: Added `FloatingDamageNumber.SpawnText()` to fix tutorial banner text references.

---

### Module 6: System Integration, Playtesting & Final Audit

#### Week 11: August 3, 2026 – August 7, 2026
*Focus: Full Level Playtesting, AI Persona Verification & Internship Certification*

- **Mon Aug 3, 2026**:
  - *Learning*: Learned automated level generation verification and scene dirty state management.
  - *Implementation*: Executed 1-click level builder test in `TutorialScene.unity`, verifying clean creation of all 4 sections.
- **Tue Aug 4, 2026**:
  - *Learning*: Conducted end-to-end playtesting across all gameplay mechanics.
  - *Implementation*: Playtested progression: water region crossing -> subterranean Blob crawl -> 3 cave arena waves -> Nyxaris shrine shatter & freedom portal.
- **Wed Aug 5, 2026**:
  - *Learning*: Tested AI chat persona responses against in-game dialogue state.
  - *Implementation*: Verified `/nyxaris` chat API responses match the investigation narrative arc.
- **Thu Aug 6, 2026**:
  - *Learning*: Learned Unity project build verification and log analysis.
  - *Implementation*: Ran compilation check (`refresh_unity` & `read_console`): confirmed **0 C# compilation errors**.
- **Fri Aug 7, 2026**:
  - *Learning*: Learned documenting technical accomplishments, code walkthroughs, and learning logs.
  - *Implementation*: Finalized logbook, walkthrough documentation, and project deliverables.

---


---

### Module 7: Boss Battle AI, Advanced Combat Systems & Environmental Parallax

#### Week 12: August 10, 2026 – August 14, 2026
*Focus: Tsuchigumo Boss 3-Phase State Machine, Player Attack Combos & Parallax Grouping*

- **Mon Aug 10, 2026**:
  - *Learning*: Studied multi-phase boss state machines and dynamic phase transition thresholds driven by health percentages.
  - *Implementation*: Designed the core architecture for the Tsuchigumo Boss (`CaveScene`), establishing Phase 1 (Melee Slam), Phase 2 (Web Projectile Volleys), and Phase 3 (Rage & Spiderling Swarms).
- **Tue Aug 11, 2026**:
  - *Learning*: Learned projectile pooling and status effect application (`isWebbed`, movement speed reduction, and escape mechanics).
  - *Implementation*: Implemented Tsuchigumo web projectile volleys and sticky ground web hazard pools requiring dash escapes.
- **Wed Aug 12, 2026**:
  - *Learning*: Learned multi-layer background parallax camera tracking with depth sorting and sorting group optimization.
  - *Implementation*: Developed `CherryBlossomParallaxGrouper.cs` to organize forest background layers, configuring differentiated scroll speeds (`0.2x`, `0.5x`, `0.8x`) relative to camera movement.
- **Thu Aug 13, 2026**:
  - *Learning*: Studied responsive combo chaining, attack input buffering, and dynamic weapon collider hitboxes.
  - *Implementation*: Refined the `J` key 2-hit melee slash combo and integrated Lumi Spear weapon mechanics (`LumiSpearWeapon.cs`) with piercing projectile trajectory math.
- **Fri Aug 14, 2026**:
  - *Learning*: Learned physics-based collectible item attraction, exponential lerp interpolation, and HUD currency counter syncing.
  - *Implementation*: Implemented collectible soul orbs (`CollectibleOrb.cs`) with smooth player-seeking velocity and HUD counter integration.

---

### Module 8: High-Aesthetic UI Toolkit, Cinematic Prologues & Standalone Demo Release

#### Week 13: August 17, 2026 – August 21, 2026
*Focus: Physics Seam Smoothing, UI Toolkit Architecture, Optical Bokeh VFX & Demo Finalization*

- **Mon Aug 17, 2026**:
  - *Learning*: Studied 2D tilemap edge-snag physics artifacts and Raycast-based ground seam smoothing techniques.
  - *Implementation*: Resolved player ground collision snags in `move.cs` using composite collider contact normalization and smoothed edge raycasts.
- **Tue Aug 18, 2026**:
  - *Learning*: Learned asynchronous memory management, texture pre-warming, and sprite cache dictionary patterns.
  - *Implementation*: Built asynchronous frame pre-warming for `FoxNyxarisController.cs` (202 animation frames) to eliminate in-game freeze-frames upon mob defeats.
- **Wed Aug 19, 2026**:
  - *Learning*: Learned UI Toolkit (UXML/USS) architecture for Unity, flexbox responsive layouts, and cyber-gothic glassmorphism.
  - *Implementation*: Redesigned the Main Menu with cyber-gothic buttons, 12-slot save/load modal (`MainMenu.uxml`), and unified `PauseMenu.cs` in cohesive shades of purple (Radiant Violet, Amethyst, Celestial Orchid, Dusky Plum).
- **Thu Aug 20, 2026**:
  - *Learning*: Studied optical bokeh simulation via Gaussian falloff mathematics and interactive sprite frame triggering.
  - *Implementation*: Created `ArcaneLoadingScreen.cs` with rotating dual-ring arcane mandala seal, traveling spark progress bar, and floating purple bokeh orbs; implemented tap-to-flap wing animation in `MainMenuUIToolkitController.cs` with fully-extended wing pause.
- **Fri Aug 21, 2026**:
  - *Learning*: Learned cinematic narrative pacing, alpha transition interpolation, native font asset management, and standalone build verification.
  - *Implementation*: Built the opening cinematic lore prologue (`TutorialLorePrologue.cs`) with 7 narrative slides, native ancient TrueType font cycling (`AncientGothic`, `AncientPapyrus`, `AncientSerif`), grand Title Card climax slide, completed final QA audit with 0 errors, and finalized the demo release.

## 📊 Summary of Learning Deliverables & Practical Mastery

| Skill Domain | Key Concept Learned | Practical Implementation Completed | Status |
| :--- | :--- | :--- | :--- |
| **Unity 2D Physics** | Rigidbody2D, Velocity, Ground Masking, i-Frames | Snappy Jump, Variable Gravity, Dash, Blob Form (`move.cs`) | ✅ Mastered |
| **Procedural Math** | Catmull-Rom Splines, Hooke's Law, Wave Fourier | Smooth 2D Purple Water Simulation (`PurpleWater2D.cs`) | ✅ Mastered |
| **Graphics & VFX** | HLSL Shaders, GPU Particle Systems, Sorting Layers | `RealisticPurpleWater.shader`, Splash Rings (`PurpleWaterSplashFX.cs`) | ✅ Mastered |
| **2D Platforming Mechanics** | Proximity Assembly, Effector2D, Scale Curves | Dynamic Pink Bridge Cluster (`DynamicPinkBridgeCluster.cs`) | ✅ Mastered |
| **Interactive Props** | Vector Impulse, Pendulum Motion, Debris Physics | Cuttable Plants (`CuttablePlant.cs`), Hit Props (`HitReactiveObject.cs`) | ✅ Mastered |
| **AI Combat Logic** | State Machines, Dynamic Ranges, Flight Paths | Samurai AI (`NormalMaleSamuraiAI.cs`), Flying Orb (`NightmareOrbAI.cs`) | ✅ Mastered |
| **Level Architecture** | Elevation Progression, Section Layout, Wave Managers | 4-Section Tutorial Level, Cave Manager (`TutorialCaveWaveManager.cs`) | ✅ Mastered |
| **Narrative Integration** | `IDamageable`, Event Coroutines, Portal Teleports | Nyxaris Shrine Shatter & Freedom Arc (`NyxarisShrineCage.cs`) | ✅ Mastered |
| **Editor Automation** | `EditorWindow`, Scene Automation, Null Safety | 1-Click Level Builder Tool (`TutorialLevelBuilder.cs`) | ✅ Mastered |
| **Python & AI Backend** | Flask Server, Rembg, JSONL Curation, REST APIs | `app.py` Backend & `/nyxaris` AI Dialogue Persona Engine | ✅ Mastered |
| **Boss Battle AI** | 3-Phase State Machine, Projectile Pooling, Debuffs | Tsuchigumo Boss (`CaveScene`), Web Projectile Volleys | ✅ Mastered |
| **Parallax & Camera** | Multi-layer Depth Sorting, Scroll Factors | Cherry Blossom Parallax (`CherryBlossomParallaxGrouper.cs`) | ✅ Mastered |
| **Combat & Weapons** | Combo Buffering, Piercing Projectile Trajectories | 2-Hit Melee Combo, Lumi Spear Weapon (`LumiSpearWeapon.cs`) | ✅ Mastered |
| **Performance Optimization** | Async Pre-warming, Sprite Caching Dictionaries | `FoxNyxarisController` 202-frame Background Preload | ✅ Mastered |
| **UI Toolkit & Theming** | UXML/USS Flexbox, Glassmorphism, Color Harmonies | Cyber-Gothic Main Menu, 12-Slot Save Modal, Purple Pause Menu | ✅ Mastered |
| **Procedural Optical VFX** | Gaussian Bokeh Blur, Dual-Ring Arcane Mandalas | Procedural Blurry Orbs, `ArcaneLoadingScreen.cs` | ✅ Mastered |
| **Cinematic Systems** | Alpha Fade Interpolation, Native TrueType Typography | Opening Lore Prologue & Title Card (`TutorialLorePrologue.cs`) | ✅ Mastered |

---
*Logbook certified and completed on Friday, August 21, 2026.*
