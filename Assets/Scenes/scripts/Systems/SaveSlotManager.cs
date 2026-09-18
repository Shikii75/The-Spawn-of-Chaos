using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpawnOfChaos.Systems
{
    [Serializable]
    public class SaveSlotData
    {
        public int slotIndex;
        public bool isEmpty = true;
        public string locationName = "Empty Slot";
        public string sceneName = "TutorialScene";
        public float posX = 0f;
        public float posY = 0f;
        public float posZ = 0f;
        public int playerLevel = 1;
        public int coins = 0;
        public string currentWeapon = "DarkSpear";
        public string saveTimestamp = "";
        public float playTimeSeconds = 0f;

        public string GetFormattedPlaytime()
        {
            TimeSpan t = TimeSpan.FromSeconds(playTimeSeconds);
            return string.Format("{0:D2}h {1:D2}m", (int)t.TotalHours, t.Minutes);
        }
    }

    /// <summary>
    /// Manages 12 persistent save slots for The Spawn of Chaos.
    /// Provides serialization, slot deletion, quick continue resolution,
    /// and seamless level transition coordination.
    /// </summary>
    public static class SaveSlotManager
    {
        public const int TOTAL_SLOTS = 12;
        private const string PREF_KEY_PREFIX = "SpawnOfChaos_SaveSlot_";
        private const string PREF_ACTIVE_SLOT = "SpawnOfChaos_ActiveSlotIndex";
        private const string PREF_LAST_PLAYED_SLOT = "SpawnOfChaos_LastPlayedSlot";

        public static int ActiveSlotIndex
        {
            get => PlayerPrefs.GetInt(PREF_ACTIVE_SLOT, 1);
            set
            {
                PlayerPrefs.SetInt(PREF_ACTIVE_SLOT, value);
                PlayerPrefs.Save();
            }
        }

        public static int LastPlayedSlotIndex
        {
            get => PlayerPrefs.GetInt(PREF_LAST_PLAYED_SLOT, 1);
            set
            {
                PlayerPrefs.SetInt(PREF_LAST_PLAYED_SLOT, value);
                PlayerPrefs.Save();
            }
        }

        public static SaveSlotData GetSlot(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > TOTAL_SLOTS) return null;

            string key = PREF_KEY_PREFIX + slotIndex;
            if (!PlayerPrefs.HasKey(key))
            {
                return new SaveSlotData
                {
                    slotIndex = slotIndex,
                    isEmpty = true,
                    locationName = "Empty Slot"
                };
            }

            try
            {
                string json = PlayerPrefs.GetString(key);
                SaveSlotData data = JsonUtility.FromJson<SaveSlotData>(json);
                if (data != null)
                {
                    data.slotIndex = slotIndex;
                    return data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSlotManager] Failed to read slot {slotIndex}: {ex.Message}");
            }

            return new SaveSlotData { slotIndex = slotIndex, isEmpty = true, locationName = "Corrupted Slot" };
        }

        public static List<SaveSlotData> GetAllSlots()
        {
            var list = new List<SaveSlotData>();
            for (int i = 1; i <= TOTAL_SLOTS; i++)
            {
                list.Add(GetSlot(i));
            }
            return list;
        }

        public static void SaveSlot(int slotIndex, SaveSlotData data)
        {
            if (slotIndex < 1 || slotIndex > TOTAL_SLOTS || data == null) return;

            data.slotIndex = slotIndex;
            data.isEmpty = false;
            data.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PREF_KEY_PREFIX + slotIndex, json);
            LastPlayedSlotIndex = slotIndex;
            ActiveSlotIndex = slotIndex;
            PlayerPrefs.Save();
            Debug.Log($"<color=#D47BFF>[SaveSlotManager] Saved Slot {slotIndex}: {data.locationName} ({data.sceneName})</color>");
        }

        public static void DeleteSlot(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > TOTAL_SLOTS) return;

            string key = PREF_KEY_PREFIX + slotIndex;
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.Log($"[SaveSlotManager] Deleted Slot {slotIndex}.");
            }
        }

        public static bool HasAnySave()
        {
            for (int i = 1; i <= TOTAL_SLOTS; i++)
            {
                if (!GetSlot(i).isEmpty) return true;
            }
            return false;
        }

        public static int GetMostRecentSlotIndex()
        {
            int lastIndex = LastPlayedSlotIndex;
            if (!GetSlot(lastIndex).isEmpty) return lastIndex;

            for (int i = 1; i <= TOTAL_SLOTS; i++)
            {
                if (!GetSlot(i).isEmpty) return i;
            }

            return 1;
        }

        public static int FindFirstEmptySlotIndex()
        {
            for (int i = 1; i <= TOTAL_SLOTS; i++)
            {
                if (GetSlot(i).isEmpty) return i;
            }
            return 1; // Fallback overwrite slot 1
        }

        /// <summary>
        /// Saves current player state into the active save slot.
        /// </summary>
        public static void SaveCurrentGameState(string customLocationName = null)
        {
            int slot = ActiveSlotIndex;
            string currentScene = SceneManager.GetActiveScene().name;

            Vector3 playerPos = Vector3.zero;
            GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player") ?? GameObject.Find("BasePlayer");
            if (player != null) playerPos = player.transform.position;

            int coins = PlayerCurrency.Instance != null ? PlayerCurrency.Instance.Coins : 0;
            string weapon = Weapons.WeaponManager.Instance != null ? Weapons.WeaponManager.Instance.ActiveWeapon.ToString() : "DarkSpear";

            string locName = customLocationName;
            if (string.IsNullOrEmpty(locName))
            {
                locName = InferLocationName(currentScene);
            }

            SaveSlotData currentData = GetSlot(slot);
            float playTime = (currentData != null && !currentData.isEmpty) ? currentData.playTimeSeconds + 60f : 60f;

            SaveSlotData newData = new SaveSlotData
            {
                slotIndex = slot,
                isEmpty = false,
                locationName = locName,
                sceneName = currentScene,
                posX = playerPos.x,
                posY = playerPos.y,
                posZ = playerPos.z,
                playerLevel = 1,
                coins = coins,
                currentWeapon = weapon,
                playTimeSeconds = playTime
            };

            SaveSlot(slot, newData);
        }

        public static string InferLocationName(string sceneName)
        {
            switch (sceneName)
            {
                case "TutorialScene": return "Primordial Void & Tutorial";
                case "MountainPathScene": return "Mountain Path & Cherry Grove";
                case "Dojo1Scene": return "Strawhat Clan Dojo";
                case "Dojo2Scene": return "Samurai Clan Dojo";
                case "CaveScene": return "Subterranean Cavern & Boss";
                case "SampleScene": return "Level 1: Cherry Blossom Forest";
                default: return sceneName;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureLevel1SlotAvailable()
        {
            SaveSlotData slot1 = GetSlot(1);
            if (slot1 == null || slot1.isEmpty || slot1.sceneName == "TutorialScene" || slot1.locationName != "Start Cherry Blossom")
            {
                SaveSlotData cherrySlot1 = new SaveSlotData
                {
                    slotIndex = 1,
                    isEmpty = false,
                    locationName = "Start Cherry Blossom",
                    sceneName = "SampleScene",
                    posX = -33.4f,
                    posY = -8.3f,
                    posZ = 0f,
                    playerLevel = 1,
                    coins = 100,
                    currentWeapon = "DarkSpear",
                    saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    playTimeSeconds = 60f
                };
                SaveSlot(1, cherrySlot1);
            }

            SaveSlotData slot2 = GetSlot(2);
            if (slot2 == null || slot2.isEmpty || slot2.sceneName == "TutorialScene" || slot2.locationName != "Start Cherry Blossom")
            {
                SaveSlotData cherrySlot2 = new SaveSlotData
                {
                    slotIndex = 2,
                    isEmpty = false,
                    locationName = "Start Cherry Blossom",
                    sceneName = "SampleScene",
                    posX = -33.4f,
                    posY = -8.3f,
                    posZ = 0f,
                    playerLevel = 1,
                    coins = 100,
                    currentWeapon = "DarkSpear",
                    saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    playTimeSeconds = 60f
                };
                SaveSlot(2, cherrySlot2);
            }
            Debug.Log("<color=#55FF88>[SaveSlotManager] Save slots seeded with 'Start Cherry Blossom' (SampleScene at Torii Gate)!</color>");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Spawn of Chaos/Save Slots/Set Slot to Start Cherry Blossom")]
        public static void SeedSlot2ToLevel1()
        {
            SaveSlotData cherrySlot = new SaveSlotData
            {
                slotIndex = 1,
                isEmpty = false,
                locationName = "Start Cherry Blossom",
                sceneName = "SampleScene",
                posX = -33.4f,
                posY = -8.3f,
                posZ = 0f,
                playerLevel = 1,
                coins = 100,
                currentWeapon = "DarkSpear",
                saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                playTimeSeconds = 60f
            };
            SaveSlot(1, cherrySlot);
            cherrySlot.slotIndex = 2;
            SaveSlot(2, cherrySlot);
            Debug.Log("<color=#55FF88>[SaveSlotManager] Slots 1 & 2 set to 'Start Cherry Blossom' (SampleScene)!</color>");
        }
#endif
    }
}