using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ModeAttract;
using static SpawnList;

namespace Better_Servers
{
    internal class MPKingdomsPatches
    {
        internal static class ManTechsPatches
        {
            internal static Type target = typeof(ManTechs);

            /// <summary> CatchPlayerInitCheats </summary>
            internal static bool CheckSleepRange_Prefix(ManTechs __instance, ref Tank tech)
            {
                if (!MPKingdomsTest.AreWeKingdoming)
                    return true;
                WorldTile tile = ManWorld.inst.TileManager.LookupTile(WorldPosition.FromScenePosition(tech.boundsCentreWorldNoCheck).TileCoord);
                return tile == null || !tile.IsLoaded;
            }
        }

        /*
        internal static class ManPurchasesPatches
        {
            internal static Type target = typeof(ManPurchases);

            /// <summary> LetPlayerTeamsHaveTheirOwn </summary>
            internal static bool CheckSleepRange_Prefix(ManTechs __instance, ref Tank tech)
            {
                if (!MPKingdomsTest.AreWeKingdoming)
                    return true;
                WorldTile tile = ManWorld.inst.TileManager.LookupTile(WorldPosition.FromScenePosition(tech.boundsCentreWorldNoCheck).TileCoord);
                return tile == null || !tile.IsLoaded;
            }
        }//*/
        internal static class ModeCoopPatches
        {
            internal static Type target = typeof(ModeCoOp<ModeCoOpCreative>);

            /// <summary> WaitOnSpawningFTUEWhileGettingTech </summary>
            internal static bool UpdateSpawnLogicForPlayer_Prefix(NetPlayer player)
            {
                if (!MPKingdomsTest.AreWeKingdoming)
                    return true;
                if (MPKingdomsTest.TryGetPlayer(player, out var playerData))
                {
                    //DebugBeS.Log("Waiting on tech search for " + player.name);
                    return playerData.GaveUpOnTechSearch;
                }
                return true;
            }
            internal static bool CanPlayerChangeTech_Prefix(NetPlayer player, ref bool __result)
            {
                if (!MPKingdomsTest.AreWeKingdoming)
                    return true;
                __result = true;
                return false;
            }
        }



        internal static class ManPopPatches
        {
            internal static Type target = typeof(ManPop);
            public static FieldInfo savData = typeof(ManPop).GetField("m_SaveData", BindingFlags.Instance | BindingFlags.NonPublic);
            public static FieldInfo minRad = typeof(ManPop).GetField("m_SpawnRadiusMin", BindingFlags.Instance | BindingFlags.NonPublic);
            public static FieldInfo maxRad = typeof(ManPop).GetField("m_SpawnRadiusMax", BindingFlags.Instance | BindingFlags.NonPublic);
            public static FieldInfo spawner = typeof(ManPop).GetField("m_Spawner", BindingFlags.Instance | BindingFlags.NonPublic);
            public static MethodInfo getSpawnTech = typeof(ManPop).GetMethod("ChoosePopTechToSpawn", BindingFlags.Instance | BindingFlags.NonPublic);
            public static MethodInfo getWaveTech = typeof(ManPop).GetMethod("ChooseWaveTechToSpawn", BindingFlags.Instance | BindingFlags.NonPublic);
            public static MethodInfo getRandAngle= typeof(ManPop).GetMethod("GenerateRandomSpawnAngleDegrees", BindingFlags.Instance | BindingFlags.NonPublic);
            public static Type savFile = typeof(ManPop).GetNestedType("SaveData", BindingFlags.NonPublic);
            public static FieldInfo wavState = savFile.GetField("m_WaveState", BindingFlags.Instance | BindingFlags.Public);
            public static FieldInfo spawnTime = savFile.GetField("m_TimeBeforeSpawn", BindingFlags.Instance | BindingFlags.Public);

            static float minRadCached = 0f;
            static float maxRadCached = 0f;
            static object[] paraSpawnTech = new object[1];
            static object[] paraWaveTech = new object[2];

            /// <summary> Allow the all-might of enemies to spawn by ANY random player! </summary>
            internal static bool TryToSpawn_Prefix(ManPop __instance, ref bool debugForceFromPopulation)
            {
                if (!MPKingdomsTest.AreWeKingdoming)
                    return true;
                if (minRadCached == 0)
                {
                    minRadCached = (float)minRad.GetValue(__instance);
                    maxRadCached = (float)maxRad.GetValue(__instance);
                }
                ObjectSpawner OS = spawner.GetValue(__instance) as ObjectSpawner;
                if (OS.IsBusy)
                    return false;

                AITreeType aitype = null;
                TechData techData;
                float spawnRadiusMin;
                float spawnRadiusMax;
                var savDat = savData.GetValue(__instance);
                if ((ManPop.WaveState)wavState.GetValue(savDat) == ManPop.WaveState.NotDoingWaves ||
                    debugForceFromPopulation)
                {
                    techData = (TechData)getSpawnTech.Invoke(__instance, paraSpawnTech);
                    spawnRadiusMin = minRadCached;
                    spawnRadiusMax = maxRadCached;
                }
                else
                {
                    techData = (TechData)getWaveTech.Invoke(__instance, paraWaveTech);
                    spawnRadiusMin = (float)paraWaveTech[0];
                    spawnRadiusMax = (float)paraWaveTech[1];
                }
                if (techData != null)
                {
                    NetPlayer playerRand = ManNetwork.inst.GetPlayer(UnityEngine.Random.Range(0, ManNetwork.inst.GetNumPlayers()));
                    if (playerRand.CurTech != null)
                    {
                        float d = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
                        float y = (float)getRandAngle.Invoke(__instance, Array.Empty<object>());
                        Transform targetPlayerTrans = playerRand.CurTech.transform;
                        Vector3 a = Quaternion.Euler(0f, y, 0f) * Maths.VecToXZUnitVec(targetPlayerTrans.forward);
                        Vector3 vector = targetPlayerTrans.position + a * d;
                        ManFreeSpace.FreeSpaceParams freeSpaceParams = new ManFreeSpace.FreeSpaceParams
                        {
                            m_ObjectsToAvoid = ManSpawn.AvoidSceneryVehiclesCrates,
                            m_CircleRadius = techData.Radius,
                            m_CenterPosWorld = WorldPosition.FromScenePosition(vector),
                            m_CircleIndex = 0,
                            m_CameraSpawnConditions = ManSpawn.CameraSpawnConditions.Anywhere,
                            m_CheckSafeArea = !debugForceFromPopulation,
                            m_RejectFunc = null
                        };
                        ManSpawn.TechSpawnParams objectSpawnParams = new ManSpawn.TechSpawnParams
                        {
                            m_TechToSpawn = techData,
                            m_AIType = aitype,
                            m_Team = -1,
                            m_Rotation = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f),
                            m_Grounded = true,
                            m_SpawnVisualType = ManSpawn.SpawnVisualType.Bomb,
                            m_IsPopulation = true
                        };
                        bool autoRetry = false;
                        OS.TrySpawn(objectSpawnParams, freeSpaceParams, null, "PopSpawn", autoRetry);
                        return false;
                    }
                }
                spawnTime.SetValue(savDat, 2f);
                return false;
            }
        }

    }
}
