### Unity Play Mode & Scene Transitions

1. **Avoid Duplicate Scene Name Reloading**:
   - In scenes that feature main menus or title screens (like `SampleScene`), clicking play should check if the active scene name matches the target scene name.
   - If they match, simply deactivate the menu UI canvas rather than calling `SceneManager.LoadScene()`. This prevents Unity from loading duplicate scene files (e.g. loading `Assets/Scenes/SampleScene.unity` instead of your edited playtest scene `Assets/Scenes/art/SampleScene.unity`) and losing unsaved editor changes.

2. **Prevent Static Variable Caching (Bypassed Domain Reloads)**:
   - When Unity's "Reload Domain on Play" option is bypassed for fast play mode entry, static variables do not reset.
   - Always clear static transition cache variables (e.g. `PlayerSpawnPointManager.targetSpawnPointName = ""`) immediately upon reading them in the spawner's `Start()` or `Awake()` method to ensure future play tests start at your editor-placed character coordinates.

3. **Player & HUD Persistence**:
   - Do not rely on placing the Player GameObject inside every sub-scene or interior level.
   - Attach a persistent component or call `DontDestroyOnLoad(player)` in `move.cs` or the spawner to carry the player character and its child objects across scene boundaries.
   - Ensure the HUD UI canvas is also marked with `DontDestroyOnLoad(canvas.gameObject)` inside `HUDManager.cs` so health/mana/currency meters carry over seamlessly into interiors.

### Narrative Lore: Cherry Blossom Forest & The Tsuchigumo Plot

1. **The Investigation Arc**:
   - **Mission**: Sent by the goddess Nyxaris to find who mass-murdered her followers in this timeline of the Dark Multiverse.
   - **Tip**: Followers tip the player that the killer is a warrior in one of two rivaling clans in the Cherry Blossom Forest: the **Strawhat Clan** (Dojo 1) and the **Samurai Clan** (Dojo 2).
   - **Village Clues**: In the village above, the player learns the clans hate each other, live in the mountains, and that giant spiders have been multiplying around the region.

2. **Dojo 1 (Strawhat Clan)**:
   - Entry requires a fee (a short ad/tribute, to be implemented later).
   - The player enters seeking answers. A confrontation leads to a challenge fight.
   - **Clue on Defeat**: A defeated member hints that the culprit is in the *Samurai Clan* because they have a history of shape-shifting/impersonating them.

3. **Dojo 2 (Samurai Clan)**:
   - Entry requires a fee (a short ad/tribute, to be implemented later).
   - A second challenge fight occurs. On defeat, they tell the player that their Master (who died 2 years ago) was recently sighted alive near the regional cave.

4. **The Cave (The Culprit)**:
   - The player fights spider variants down the cave.
   - **The Reveal**: At the bottom, they find the warrior, who is revealed to be the shape-shifting **Tsuchigumo Giant Spider**. Tsuchigumo has been impersonating members of both clans to provoke a war and cover up their own massacre of Nyxaris's followers.

### Dojo 1 Interior Mechanics Design

1. **Triggering Combat (Dialogue Confrontation)**:
   - The scene starts in a calm state with unlocked gates.
   - Upon approaching the altar, a brief dialogue confrontation starts with a Dojo member.
   - Once the dialogue ends, the left and right gates slide shut (lock), and the waves begin automatically.

2. **Combo/Style Feedback (Combo Meter)**:
   - A style counter (displaying D, C, B, A, S ranks) will track player combat hits and spells to reward fluid movement and spellcasting during the wave battles.

### 2D Combat AI Design & Physics Safety

1. **Dynamic Attack Ranges (Avoid Player Pushing)**:
   - Do not rely solely on hardcoded center-to-center values (e.g. `distance <= attackRange`) to stop moving and start an attack. If the configured range is smaller than the combined widths of the enemy and player colliders, the enemy will collide and physically push the player.
   - Always calculate an effective range dynamically based on both colliders' bounds:
     `touchDistance = enemyColliderHalfWidth + playerColliderHalfWidth`
     `effectiveRange = Mathf.Max(attackRange, touchDistance + buffer)`

2. **Decouple Maneuver Cooldowns**:
   - Do not share a single cooldown timestamp or timer between distinct movement features (like forward engage dashes and defensive backdashes). Doing so causes one action to lock out the other, ruining hit-and-run AI patterns. Use independent timers for each maneuver.

3. **Chaining State Transitions Directly**:
   - For tight, sequential actions (like immediately executing a backdash or a quick combo follow-up after an attack), do not rely on high-level state machine polling inside `Update()` or `FixedUpdate()`. Delayed state timers and frame updates can easily miss the window. Instead, chain them sequentially inside the coroutine using `yield return StartCoroutine()`.

4. **Rigidbody2D vs Transform Manipulation**:
   - Avoid directly modifying `transform.position` on active dynamic Rigidbody2D objects during movement or dashes. This conflicts with Unity's internal physics loop and creates stutter. Use `rb.linearVelocity` (or `rb.velocity`).
   - If performing wall or ledge safety raycasts at the start of a dash, allow a brief grace period (e.g., 0.1s - 0.15s) before checks become active so the entity has time to begin moving away from its starting grid intersection.
