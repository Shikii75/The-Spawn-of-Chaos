using System;
using UnityEngine;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// Singleton manager tracking player Experience Points (EXP) and Level Progression.
    /// Handles level-ups, stat restores, and level-up notifications.
    /// </summary>
    public class PlayerLevelSystem : MonoBehaviour
    {
        public static PlayerLevelSystem Instance { get; private set; }

        [Header("Level Data")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentExp = 0;
        [SerializeField] private int expToNextLevel = 100;

        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => expToNextLevel;
        public float ExpRatio => Mathf.Clamp01((float)currentExp / Mathf.Max(1, expToNextLevel));

        public event Action<int, int> onExpChanged; // (currentExp, maxExp)
        public event Action<int> onLevelUp;        // (newLevel)

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0) return;

            currentExp += amount;
            Debug.Log($"[PlayerLevelSystem] Gained {amount} EXP! Current: {currentExp}/{expToNextLevel}");

            while (currentExp >= expToNextLevel)
            {
                LevelUp();
            }

            onExpChanged?.Invoke(currentExp, expToNextLevel);
        }

        private void LevelUp()
        {
            currentExp -= expToNextLevel;
            currentLevel++;
            expToNextLevel = Mathf.RoundToInt(expToNextLevel * 1.5f);

            Debug.Log($"[PlayerLevelSystem] LEVEL UP! Player is now Level {currentLevel}!");

            // Restore Player Health & Mana on Level Up
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Health h = player.GetComponent<Health>();
                if (h != null) h.Heal(h.maxHealth);

                MageCombat mc = player.GetComponent<MageCombat>();
                if (mc != null) mc.currentMana = mc.maxMana;
            }

            onLevelUp?.Invoke(currentLevel);
        }
    }
}
