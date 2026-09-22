using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnOfChaos.Weapons
{
    /// <summary>
    /// Master manager for the interchangeable weapon arsenal, 3-tier masteries, and active weapon abilities.
    /// Preserves persistence through PlayerPrefs.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        public static WeaponManager Instance { get; private set; }

        public const string PREF_EQUIPPED = "Weapon_Equipped";
        public const string PREF_UNLOCKED_PREFIX = "Weapon_Unlocked_";
        public const string PREF_TIER_PREFIX = "Weapon_Tier_";

        public WeaponID activeWeapon = WeaponID.DarkSpear;
        public WeaponID ActiveWeapon => activeWeapon;

        public event Action<WeaponID> OnWeaponEquipped;
        public event Action<WeaponID, int> OnWeaponUpgraded;

        private readonly Dictionary<WeaponID, WeaponInfo> weaponDatabase = new Dictionary<WeaponID, WeaponInfo>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDatabase();
            LoadState();
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("WeaponManager");
                Instance = go.AddComponent<WeaponManager>();
                Instance.InitializeDatabase();
                Instance.LoadState();
            }
        }

        private void InitializeDatabase()
        {
            weaponDatabase.Clear();

            // 0: Lumi's Light Spear (Starter Weapon)
            weaponDatabase[WeaponID.LumiSpear] = new WeaponInfo
            {
                id = WeaponID.LumiSpear,
                displayName = "Lumi's Light Spear",
                archetype = "Ethereal Traversal",
                abilityType = WeaponAbilityType.StandardLaunch,
                abilityName = "Super Launch Blast",
                abilityDescription = "Embeds into walls/floors to create footholds. Detonate on 'X' to launch the player upward.",
                baseDamage = 50,
                explosionDamage = 90,
                throwSpeed = 34f,
                recallSpeed = 38f,
                auraColor = new Color(0.04f, 0.02f, 0.08f, 0.95f),
                trailColor = new Color(0.02f, 0.01f, 0.04f, 0.95f),
                spriteResourcePath = "LumiSpear",
                unlockCost = 0,
                tier2Cost = 25,
                tier3Cost = 50
            };

            // 1: Dark Spear (Ground Anchor & Emergency Dodge)
            weaponDatabase[WeaponID.DarkSpear] = new WeaponInfo
            {
                id = WeaponID.DarkSpear,
                displayName = "Dark Void Spear",
                archetype = "Anchor & Emergency Dodge",
                abilityType = WeaponAbilityType.GroundAnchorDodge,
                abilityName = "Ground Anchor Blink",
                abilityDescription = "Embeds into surfaces. When danger strikes, press X while off the spear to emergency-teleport directly back to it with invulnerability frames!",
                baseDamage = 65,
                explosionDamage = 110,
                throwSpeed = 36f,
                recallSpeed = 42f,
                auraColor = new Color(0.6f, 0.1f, 0.95f, 1.0f),
                trailColor = new Color(0.75f, 0.2f, 1.0f, 0.95f),
                spriteResourcePath = "Weapons/DarkSpear",
                unlockCost = 0,
                tier2Cost = 45,
                tier3Cost = 90
            };

            // 2: Blood Blade (Autonomous Familiar Combat)
            weaponDatabase[WeaponID.BloodBlade] = new WeaponInfo
            {
                id = WeaponID.BloodBlade,
                displayName = "Blood Blade",
                archetype = "Autonomous Familiar",
                abilityType = WeaponAbilityType.AutonomousCombat,
                abilityName = "Autonomous Blood Slash",
                abilityDescription = "Commands a hovering crimson blade to strike without locking player animations, allowing seamless running and jumping during battle.",
                baseDamage = 55,
                explosionDamage = 85,
                throwSpeed = 32f,
                recallSpeed = 36f,
                auraColor = new Color(0.95f, 0.1f, 0.2f, 1.0f),
                trailColor = new Color(1.0f, 0.15f, 0.3f, 0.95f),
                spriteResourcePath = "Weapons/BloodBlade",
                unlockCost = 30,
                tier2Cost = 60,
                tier3Cost = 120
            };
            // 3: Dark Dagger (Removed / Disabled for now)

            // 4: Lucky Dagger (1-in-3 Cheat-Death)
            weaponDatabase[WeaponID.DarkBladeSmall] = new WeaponInfo
            {
                id = WeaponID.DarkBladeSmall,
                displayName = "Lucky Shadow Blade",
                archetype = "Fate & Survival",
                abilityType = WeaponAbilityType.LuckCheatDeath,
                abilityName = "1-in-3 Cheat Death",
                abilityDescription = "Has a 33% chance on fatal damage to cheat death, restoring 30% HP with a golden shadow shield and granting 1.5s of invulnerability.",
                baseDamage = 50,
                explosionDamage = 90,
                throwSpeed = 38f,
                recallSpeed = 40f,
                auraColor = new Color(1.0f, 0.85f, 0.2f, 1.0f),
                trailColor = new Color(1.0f, 0.9f, 0.4f, 0.95f),
                spriteResourcePath = "Weapons/DarkBladeSmall",
                unlockCost = 35,
                tier2Cost = 70,
                tier3Cost = 140
            };

            // 5: Obsidian Axe (Heavy Cleave & Seismic Slam)
            weaponDatabase[WeaponID.DarkAxe] = new WeaponInfo
            {
                id = WeaponID.DarkAxe,
                displayName = "Obsidian Battle Axe",
                archetype = "Heavy Cleave & Seismic Titan",
                abilityType = WeaponAbilityType.SeismicSlam,
                abilityName = "Seismic Shockwave",
                abilityDescription = "Massive physical damage. Slams down with an expanding earth-cracking shockwave that damages and staggers all nearby foes.",
                baseDamage = 80,
                explosionDamage = 135,
                throwSpeed = 28f,
                recallSpeed = 34f,
                auraColor = new Color(0.85f, 0.35f, 0.1f, 1.0f),
                trailColor = new Color(0.95f, 0.45f, 0.15f, 0.95f),
                spriteResourcePath = "Weapons/DarkAxe",
                unlockCost = 40,
                tier2Cost = 80,
                tier3Cost = 160
            };
        }

        private void LoadState()
        {
            int savedEquipped = PlayerPrefs.GetInt(PREF_EQUIPPED, (int)WeaponID.DarkSpear);
            // If saved weapon was DarkDag (3) or not in database, reset to base spear (DarkSpear)
            if (savedEquipped == (int)WeaponID.DarkDag || !weaponDatabase.ContainsKey((WeaponID)savedEquipped))
            {
                savedEquipped = (int)WeaponID.DarkSpear;
                PlayerPrefs.SetInt(PREF_EQUIPPED, savedEquipped);
                PlayerPrefs.Save();
            }
            activeWeapon = (WeaponID)savedEquipped;

            // Ensure base spear (DarkSpear) is unlocked
            UnlockWeapon(WeaponID.DarkSpear);
        }

        public bool IsUnlocked(WeaponID id)
        {
            if (id == WeaponID.DarkSpear) return true;
            if (id == WeaponID.DarkDag) return false;
            return PlayerPrefs.GetInt(PREF_UNLOCKED_PREFIX + (int)id, 0) == 1;
        }

        public void UnlockWeapon(WeaponID id)
        {
            if (id == WeaponID.DarkDag) return;
            PlayerPrefs.SetInt(PREF_UNLOCKED_PREFIX + (int)id, 1);
            if (GetTier(id) < 1) SetTier(id, 1);
            PlayerPrefs.Save();
        }

        public int GetTier(WeaponID id)
        {
            return PlayerPrefs.GetInt(PREF_TIER_PREFIX + (int)id, 1);
        }

        public void SetTier(WeaponID id, int tier)
        {
            PlayerPrefs.SetInt(PREF_TIER_PREFIX + (int)id, Mathf.Clamp(tier, 1, 3));
            PlayerPrefs.Save();
        }

        public void UpgradeWeapon(WeaponID id)
        {
            int currentTier = GetTier(id);
            if (currentTier < 3)
            {
                SetTier(id, currentTier + 1);
                OnWeaponUpgraded?.Invoke(id, currentTier + 1);
            }
        }

        public void EquipWeapon(WeaponID id)
        {
            if (id == WeaponID.DarkDag) return;
            if (!IsUnlocked(id)) return;
            activeWeapon = id;
            PlayerPrefs.SetInt(PREF_EQUIPPED, (int)id);
            PlayerPrefs.Save();

            OnWeaponEquipped?.Invoke(id);
            Debug.Log($"[WeaponManager] Equipped weapon: {GetWeaponInfo(id)?.displayName ?? id.ToString()}");
        }

        public WeaponInfo GetWeaponInfo(WeaponID id)
        {
            if (weaponDatabase.Count == 0) InitializeDatabase();

            if (weaponDatabase.TryGetValue(id, out WeaponInfo info))
            {
                return info;
            }
            if (weaponDatabase.TryGetValue(WeaponID.DarkSpear, out WeaponInfo defaultInfo))
            {
                return defaultInfo;
            }
            if (weaponDatabase.TryGetValue(WeaponID.LumiSpear, out WeaponInfo fallbackInfo))
            {
                return fallbackInfo;
            }
            return null;
        }

        public WeaponInfo ActiveWeaponInfo => GetWeaponInfo(activeWeapon);
        public IEnumerable<WeaponInfo> AllWeapons
        {
            get
            {
                if (weaponDatabase.Count == 0) InitializeDatabase();
                return weaponDatabase.Values;
            }
        }

        /// <summary>
        /// Evaluates 1-in-3 Lucky Cheat-Death for DarkBladeSmall.
        /// </summary>
        public bool TryTriggerLuckCheatDeath(Health playerHealth)
        {
            if (activeWeapon != WeaponID.DarkBladeSmall) return false;

            int tier = GetTier(WeaponID.DarkBladeSmall);
            // Tier 1 & 2: 1 in 3 (33%). Tier 3: 1 in 2 (50%).
            int roll = UnityEngine.Random.Range(0, tier >= 3 ? 2 : 3);
            if (roll == 0)
            {
                int healPercent = tier >= 3 ? 40 : 30;
                int healAmount = Mathf.Max(1, Mathf.RoundToInt(playerHealth.MaxHealth * (healPercent / 100f)));
                playerHealth.Heal(healAmount);

                // Apply invulnerability frames
                move playerMove = playerHealth.GetComponent<move>();
                if (playerMove != null)
                {
                    // Trigger temporary invulnerability
                    StartCoroutine(TemporaryInvulnerableRoutine(playerMove, 1.5f));
                }

                Debug.Log($"[WeaponManager] LUCK CHEAT-DEATH TRIGGERED! Saved with {healAmount} HP!");
                return true;
            }

            return false;
        }

        private System.Collections.IEnumerator TemporaryInvulnerableRoutine(move playerMove, float duration)
        {
            // Give temporary visual flicker / invulnerability
            var sr = playerMove.GetComponent<SpriteRenderer>();
            Color origColor = sr != null ? sr.color : Color.white;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (sr != null)
                {
                    sr.color = new Color(1f, 0.85f, 0.2f, Mathf.PingPong(t * 8f, 1f));
                }
                yield return null;
            }

            if (sr != null) sr.color = origColor;
        }
    }
}
