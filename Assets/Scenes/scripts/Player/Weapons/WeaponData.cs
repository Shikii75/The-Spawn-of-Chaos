using System;
using UnityEngine;

namespace SpawnOfChaos.Weapons
{
    public enum WeaponID
    {
        LumiSpear = 0,
        DarkSpear = 1,
        BloodBlade = 2,
        DarkDag = 3,
        DarkBladeSmall = 4,
        DarkAxe = 5
    }

    public enum WeaponAbilityType
    {
        StandardLaunch,
        GroundAnchorDodge,
        AutonomousCombat,
        DuplicationOrbiters,
        LuckCheatDeath,
        SeismicSlam
    }

    [System.Serializable]
    public class WeaponInfo
    {
        public WeaponID id;
        public string displayName;
        public string archetype;
        public WeaponAbilityType abilityType;
        public string abilityName;
        [TextArea(2, 4)]
        public string abilityDescription;

        [Header("Combat Stats (Tier 1 Base)")]
        public int baseDamage = 50;
        public int explosionDamage = 90;
        public float throwSpeed = 34f;
        public float recallSpeed = 38f;
        public float cooldown = 0.5f;

        [Header("Aesthetics")]
        public Color auraColor = Color.cyan;
        public Color trailColor = Color.cyan;
        public string spriteResourcePath;

        [Header("Shop & Mastery Pricing (Coins)")]
        public int unlockCost = 15;
        public int tier2Cost = 35;
        public int tier3Cost = 75;

        private Sprite cachedSprite;
        public Sprite GetSprite()
        {
            if (cachedSprite == null && !string.IsNullOrEmpty(spriteResourcePath))
            {
                cachedSprite = Resources.Load<Sprite>(spriteResourcePath);
            }
            return cachedSprite;
        }

        public int GetDamageForTier(int tier)
        {
            float mult = tier == 1 ? 1.0f : (tier == 2 ? 1.35f : 1.75f);
            return Mathf.RoundToInt(baseDamage * mult);
        }

        public int GetExplosionDamageForTier(int tier)
        {
            float mult = tier == 1 ? 1.0f : (tier == 2 ? 1.35f : 1.75f);
            return Mathf.RoundToInt(explosionDamage * mult);
        }
    }
}
