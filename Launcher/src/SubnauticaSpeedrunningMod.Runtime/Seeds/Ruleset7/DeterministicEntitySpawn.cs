using UnityEngine;
using UWE;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset7
{
    internal static class DeterministicEntitySpawn
    {
        internal static void Spawn(GameObject virtualPrefab, Transform parent, Vector3 position, Quaternion rotation,
            EntitySlot.Filler filler, ulong slotKey, bool cumulativeOffset)
        {
            if (string.IsNullOrEmpty(filler.classId))
            {
                return;
            }

            WorldEntityInfo info;
            if (!WorldEntityDatabase.TryGetInfo(filler.classId, out info))
            {
                UWE.Utils.LogReportFormat("Set Seed: missing world entity info for prefab '{0}'", filler.classId);
                return;
            }

            Vector3 runningPosition = position;
            for (int i = 0; i < filler.count; i++)
            {
                Vector3 spawnPosition = cumulativeOffset ? runningPosition : position;
                if (i > 0)
                {
                    Vector3 offset = SeedRuntime.InsideUnitSphere(DeterministicHash.Add(slotKey, i)) * 4f;
                    if (cumulativeOffset)
                    {
                        runningPosition += offset;
                        spawnPosition = runningPosition;
                    }
                    else
                    {
                        spawnPosition += offset;
                    }
                }

                Quaternion spawnRotation = rotation;
                if (info.prefabZUp)
                {
                    spawnRotation *= Quaternion.Euler(-90f, 0f, 0f);
                }

                GameObject entity = Object.Instantiate(virtualPrefab, spawnPosition, spawnRotation) as GameObject;
                entity.transform.SetParent(parent, false);
                entity.transform.localScale = info.localScale;
                VirtualPrefabIdentifier identifier = entity.GetComponent<VirtualPrefabIdentifier>();
                identifier.ClassId = filler.classId;
                LargeWorldEntity largeWorldEntity = entity.GetComponent<LargeWorldEntity>();
                largeWorldEntity.cellLevel = info.cellLevel;
                if (LargeWorldStreamer.main != null)
                {
                    LargeWorldStreamer.main.cellManager.RegisterEntity(largeWorldEntity);
                }
                entity.SetActive(true);
            }
        }
    }
}

