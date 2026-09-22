using SpawnOfChaos.Entities;
using SpawnOfChaos.Minigames;
using UnityEngine;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// Utility class for spawning collectible 2D world Orbs (Health, Mana, Currency, EP).
    /// </summary>
    public static class OrbSpawner
    {
        /// <summary>
        /// Spawns a specific CollectibleOrb at the given world position.
        /// </summary>
        public static GameObject SpawnOrb(Vector3 position, OrbType type, int amount = 15)
        {
            GameObject orbGO = new GameObject($"CollectibleOrb_{type}");
            orbGO.transform.position = position;

            CollectibleOrb orb = orbGO.AddComponent<CollectibleOrb>();
            orb.orbType = type;
            orb.restoreAmount = amount;

            return orbGO;
        }

        /// <summary>
        /// Drops a random cluster of loot orbs (Health, Currency, EXP) at a location (e.g. on enemy death or chest open).
        /// Note: Generic mob loot drops exclude Mana Orbs to maintain scarce mana economy; shrines/savepoints refill mana.
        /// </summary>
        public static void SpawnLootCluster(Vector3 position, int orbCount = 3)
        {
            for (int i = 0; i < orbCount; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.6f), 0f);
                float roll = Random.value;

                OrbType type;
                int amount;

                if (roll < 0.50f)
                {
                    type = OrbType.EP; // Experience
                    amount = Random.Range(20, 35);
                }
                else if (roll < 0.85f)
                {
                    type = OrbType.Currency; // Coins
                    amount = Random.Range(5, 15);
                }
                else
                {
                    type = OrbType.Health; // Rare health drop
                    amount = Random.Range(2, 5);
                }

                SpawnOrb(position + offset, type, amount);
            }
        }
    }
}
