# ⚔️ THE SPAWN OF CHAOS — DEMO PRODUCTION ROADMAP & SPRINT SCHEDULE

> **Target Release Window:** Friday, August 21 – Sunday, August 23, 2026  
> **Project Workspace:** `The Spawn of Chaos`  
> **Engine Version:** Unity 6 LTS (URP)

---

## 🎯 High-Level Milestone Overview

```
Weekend (Aug 15–16)     : Asset Gathering & Preparation
Day 1 (Monday, Aug 17)   : Physics Snag Fix + Tsuchigumo Boss + Tutorial/Void Level
Day 2 (Tuesday, Aug 18)  : Nyxaris Chatbot Companion + Cutscenes + Save/Load System
Day 3 (Wednesday, Aug 19): Dedicated Minigame Scenes + NPC Challenges + Ads + Shop
Day 4 (Thursday, Aug 20) : UI & VFX Overhaul + Sound Effects Foley Upgrade
Day 5 (Friday, Aug 21)   : End-of-Demo Outro + Final QA + Export Standalone Demo
```

---

## 📅 Daily Sprint Breakdown

### 🛠️ Weekend (Aug 15 – 16): Asset Preparation (User)
- [ ] Gather art assets for the **Tutorial / Void Nyxaris Seal Level**.
- [ ] Prepare sprite frames / sheets for the **Tsuchigumo Giant Spider Boss**.
- [ ] Collect or design button / icon assets for the UI overhaul.
- [ ] *(Optional)* Source organic Foley SFX (steel slashes, spell casts, monster roars).

---

### 🚀 Day 1 (Monday, Aug 17): Core Mechanics & Boss
- [ ] **Player Ground Physics Seam Fix**:
  - Implement edge-smoothing raycasts / composite collider snapping so the player never snags or gets stuck on small uneven tile/box colliders.
- [ ] **Tsuchigumo Boss Full Implementation (`CaveScene`)**:
  - Wire up the 3-Phase State Machine (Melee Slam -> Web Shots -> Spiderling Swarm).
  - Connect dynamic phase transitions, boss health bar, and rage visual effects.
  - Implement the boss death sequence.
- [ ] **Tutorial / Void Nyxaris Seal Level**:
  - Build the onboarding flow (Movement, Dash Invulnerability, Spellcasting, Breaking the Nyxaris Seal).

---

### 📜 Day 2 (Tuesday, Aug 18): Narrative, Nyxaris & Cutscenes
- [ ] **Nyxaris Functional Character & AI Chatbot**:
  - Finalize the expressive avatar portrait system and dynamic reaction dialogue.
  - Integrate lore advice and companion dialogue triggers across all scenes.
- [ ] **Level Cutscenes**:
  - Level 1: Strawhat Dojo entrance confrontation & gate lock.
  - Level 2: Samurai Dojo interrogation & deceased master sighting clue.
  - Boss Reveal: Tsuchigumo unmasking from clan member into giant spider.
- [ ] **Unique Save & Load System**:
  - Implement persistent storage for level progression, Nyxaris affinity, unlocks, currency, and high scores.

---

### 🕹️ Day 3 (Wednesday, Aug 19): Minigames, Economy & Ads
- [ ] **Modular Minigame Scenes**:
  - Extract minigames (*Shadow Runner, Void Surge, Skybound Box, Orb Splash*) into dedicated standalone scenes with clean return navigation.
- [ ] **NPC Challenge & Bounty System**:
  - NPCs in the village and dojos challenge the player to reach specific high scores for gold/currency bounties.
- [ ] **Unity Ads Integration**:
  - Add Rewarded Ads for **Minigame Fuel / Energy**, extra lives, and currency multipliers.
- [ ] **Currency Economy & Shop System**:
  - Convert minigame points to in-game currency.
  - Build a sleek shop to buy spell upgrades, robes, and companion buffs.



### 🎨 Day 4 (Thursday, Aug 20): UI, VFX & Audio Overhaul
- [ ] **UI Style Synchronization**:
  - Redesign the **Pause Menu**, **Shop Panel**, and **Minigame Hub** to match the new UI Toolkit aesthetic (dark void obsidian glass, neon glowing borders, clean typography).
- [ ] **Dojo Mob Visuals & Custom VFX**:
  - Upgrade sprites/animations for Strawhat & Samurai clan warriors.
  - Add impactful 2D particle VFX (sword slash arcs, spell bursts, amethyst hit sparks).
- [ ] **Sound Effects Overhaul**:
  - Replace synthetic Game Dev OS sounds with rich, organic Foley (steel slashes, spell wooshes, footsteps, cloth rustling).

---

### 🏆 Day 5 (Friday, Aug 21): End-of-Demo Outro & Export
- [ ] **Demo Ending Sequence**:
  - Create the *"Demo Complete — The Spawn of Chaos Saga Continues"* victory screen with social links and Wishlist prompt.
- [ ] **Build Settings & Scene Flow Audit**:
  - Ensure all scene transitions, spawn points, and return-to-menu triggers work flawlessly.
- [ ] **Demo Export**:
  - Build and package the Standalone Windows / WebGL Demo build.

---

### 🛡️ Weekend Buffer (Aug 22 – 23): Final QA & Launch
- [ ] Full end-to-end demo playthrough.
- [ ] Balance boss health/damage and minigame reward rates.
- [ ] Public Demo Release! 🎮✨
