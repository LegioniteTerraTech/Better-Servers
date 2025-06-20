using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using TerraTech.Network;
using HarmonyLib;
using System.Runtime.Serialization.Formatters;
using TerraTechETCUtil;
using UnityEngine.Assertions.Must;
using static MapGenerator;

namespace Better_Servers
{
    class PatchBatch
    {
    }

    public class Patches
    {

        /*
        /// <summary>
        /// Built in a way that weeds out illegal or crash-causing invalid clients
        /// </summary>
        [HarmonyPatch(typeof(ManNetwork))]
        [HarmonyPatch("AddPlayer")]// Setup main menu techs
        internal static class OnPlayerJoin
        {
            private static bool Prefix(ManNetwork __instance, ref NetPlayer player)
            {
                if (player == null)
                    return false;
                if (!ManNetwork.IsHost)
                    return true;
                if (KickStartBetterServers.blockedInfo.bannedUsersHost.TryGetValue(player.GetPlayerIDInLobby().m_NetworkID, out string blockedUser))
                {
                    if (__instance.MyPlayer == player)
                    {   // catch if: Why are you trying to block yourself?!?
                        Debug.LogError("Better Servers: ASSERT - Server host has blocked themselves.  " +
                            "\nThis should not be remotely possible.  " +
                            "\nHave they edited their own player block list?  " +
                            "\nOr is someone cheating Valve's security by faking a user ID?!");
                        // Let the server host continue or the game will crash!
                        try
                        {
                            KickStartBetterServers.SendChatServer("<b>Somehow, you (server host) is banned in your own server!</b>");
                            KickStartBetterServers.SendChatServer("<b>Ban ignored as this is an edge case.  Please contact Legionite.</b>");
                        }
                        catch { }
                        return true;
                    }

                    // Throw warning in Chat Log that blocked user is trying to join.
                    //  Deny connection or any relations with this host.
                    try
                    {
                        KickStartBetterServers.ForceKickPlayer(player);
                        KickStartBetterServers.ChatLogKaren("Blocked user " + blockedUser + " tried to join. <b>Denied.</b>");
                    }
                    catch
                    {
                        KickStartBetterServers.ChatLogKaren("Blocked user " + blockedUser + " tried to join but the auto block failed?  Please contact Legionite.");
                    }
                    return false;
                }
                BetterServerPlayer.GetPlayer(player);
                KickStartBetterServers.ProtectedServerDelay();
                return true;
            }
        }*/
        [HarmonyPatch(typeof(ModeCoOp<ModeCoOpCreative>))]
        [HarmonyPatch("GetTargetScenePosition")]
        internal static class OnGenerateTetherOriginAddFakeIfNeeded
        {
            internal static void Postfix(ref WorldPosition __result)
            {
                if (!MPKingdomsTest.FakePlayerOrigin)
                    return;
                int numPlay = Singleton.Manager<ManNetwork>.inst.GetNumPlayers();
                Vector3 worldPos = __result.GameWorldPosition;
                worldPos *= ((float)numPlay / (numPlay + 1));
                __result = WorldPosition.FromGameWorldPosition(worldPos);
            }
        }
        [HarmonyPatch(typeof(WorldPushbackBarrier))]
        [HarmonyPatch("Update")]
        internal static class DisableGlobalAnchoredPushback
        {
            internal static bool Prefix(WorldPushbackBarrier __instance)
            {
                return !MPKingdomsTest.AreWeKingdoming;
            }
        }
        [HarmonyPatch(typeof(ModeCoOp<ModeCoOpCreative>))]
        [HarmonyPatch("CreateBoundaryMesh")]
        internal static class GrabTheBoundryVisualsEffect
        {
            internal static void Postfix(ModeCoOp<ModeCoOpCreative> __instance)
            {
                object obj = __instance;
                if (obj is ModeCoOp<ModeCoOpCampaign> campaign)
                {
                    MPKingdomsTest.GlobalBarrierVisual = (Transform)typeof(ModeCoOp<ModeCoOpCampaign>).
                        GetField("m_SpawnedBoundaryObject", BindingFlags.NonPublic | BindingFlags.Instance).
                        GetValue(campaign);
                    MPKingdomsTest.BarrierPrefab = (Transform)typeof(ModeCoOp<ModeCoOpCampaign>).
                        GetField("m_BoundaryEdgePrefab", BindingFlags.NonPublic | BindingFlags.Instance).
                        GetValue(campaign);
                    if (MPKingdomsTest.GlobalBarrierVisual != null)
                        DebugBeS.Assert("Found BarrierVisual");
                }
                else
                {
                    MPKingdomsTest.GlobalBarrierVisual = (Transform)typeof(ModeCoOp<ModeCoOpCreative>).
                        GetField("m_SpawnedBoundaryObject", BindingFlags.NonPublic | BindingFlags.Instance).
                        GetValue(__instance);
                    MPKingdomsTest.BarrierPrefab = (Transform)typeof(ModeCoOp<ModeCoOpCreative>).
                        GetField("m_BoundaryEdgePrefab", BindingFlags.NonPublic | BindingFlags.Instance).
                        GetValue(__instance);
                    if (MPKingdomsTest.GlobalBarrierVisual != null)
                        DebugBeS.Assert("Found BarrierVisual");
                }
            }
        }
        [HarmonyPatch(typeof(ModeCoOp<ModeCoOpCreative>))]
        [HarmonyPatch("GenerateTerrain")]
        internal static class OnGenerateTerrainMakeAlterations
        {
            internal static void Postfix()
            {
                //DebugBeS.Log("ModeCoOp<>.GenerateTerrain");
                if (ManGameMode.inst.IsCurrent<ModeCoOpCreative>())
                {
                    MPKingdomsTest.ExtendAll<ModeCoOpCreative>(ManGameMode.inst.AllModes.First(x => x is ModeCoOpCreative));
                }
                if (ManGameMode.inst.IsCurrent<ModeCoOpCampaign>())
                {
                    MPKingdomsTest.ExtendAll<ModeCoOpCampaign>(ManGameMode.inst.AllModes.First(x => x is ModeCoOpCampaign));
                }
            }
        }


        [HarmonyPatch(typeof(TileManager))]
        [HarmonyPatch("UpdateTileRequestStatesInStandardMode")]
        internal static class DisableLoadingOriginTiles
        {  // Each Tech now loads it's own tiles!
            private static FieldInfo TileLookupGet = typeof(TileManager).GetField("m_TileLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            private static Dictionary<IntVector2, WorldTile> TileLookup = null;
            internal static bool Prefix(TileManager __instance, List<IntVector2> tileCoordsToCreate)
            {
                if (MPKingdomsTest.AreWeKingdoming)
                {
                    // Removes the load areas from loading
                    if (TileLookup == null)
                        TileLookup = (Dictionary<IntVector2, WorldTile>)TileLookupGet.GetValue(__instance);
                    if (MPKingdomsTest.FoundOurPlayer)
                    {
                        if (TileLookup.Count == 0 && !__instance.IsClearing)
                        {
                            tileCoordsToCreate.Add(IntVector2.zero);
                        }
                        else if (TileLookup.Count > 1)
                        {
                            foreach (var pair in TileLookup)
                            {
                                //if (!pair.Value.m_Regenerate)
                                pair.Value.m_RequestState = WorldTile.State.Empty;
                            }
                        }
                    }
                    foreach (var item in ManWorldTileExt.RequestedLoaded)
                    {
                        if (TileLookup.TryGetValue(item.Key, out var tile))
                        {
                            if (!tile.m_Regenerate)
                                tile.m_RequestState = item.Value;
                        }
                        else
                        {
                            tileCoordsToCreate.Add(item.Key);
                        }
                    }
                    /*
                    bool unloaded = false;
                    foreach (var pair in TileLookup)
                    {
                        if (pair.Value.m_RequestState == WorldTile.State.Empty)
                        {
                            unloaded = true;
                            DebugBeS.Log("Unloaded tile at " + pair.Key);
                        }
                    }
                    if (unloaded)
                    {
                        DebugBeS.Log("We are trying to load:");
                        foreach (var item in ManWorldTileExt.RequestedLoaded)
                        {
                            DebugBeS.Log(item.Key.ToString());
                        }
                    }
                    */
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Built in a way that weeds out illegal or crash-causing invalid clients
        /// </summary>
        [HarmonyPatch(typeof(NetPlayer))]
        [HarmonyPatch("OnStartServer")]
        internal static class OnPlayerJoinServerside
        {
            internal static bool Prefix(NetPlayer __instance)
            {
                NetPlayer player = __instance;
                if (player == null)
                {
                    DebugBeS.Assert("The hecc?  We were handed NULL player");
                    return true;
                }
                DebugBeS.Log("OnStartServer for " + player.name);
                /*
                if (KickStartBetterServers.blockedInfo.bannedUsersHost.TryGetValue(player.GetPlayerIDInLobby().m_NetworkID, out string blockedUser))
                {
                    if (ManNetwork.inst.MyPlayer == player)
                    {   // catch if: Why are you trying to block yourself?!?
                        DebugBeS.LogError("Better Servers: ASSERT - Server host has blocked themselves.  " +
                            "\nThis should not be remotely possible.  " +
                            "\nHave they edited their own player block list?  " +
                            "\nOr is someone cheating Valve's security by faking a user ID?!");
                        // Let the server host continue or the game will crash!
                        try
                        {
                            KickStartBetterServers.SendChatServer("<b>Somehow, you (server host) is banned in your own server!</b>");
                            KickStartBetterServers.SendChatServer("<b>Ban ignored as this is an edge case.  Please contact Legionite.</b>");
                        }
                        catch { }
                        return true;
                    }

                    // Throw warning in Chat Log that blocked user is trying to join.
                    //  Deny connection or any relations with this host.
                    try
                    {
                        KickStartBetterServers.ForceKickPlayer(player);
                    }
                    catch
                    {
                        KickStartBetterServers.ChatLogKaren("Patch: OnStartClient_Prefix - ForceKickPlayer encountered error");
                    }
                    KickStartBetterServers.ChatLogKaren("Blocked user " + blockedUser + " tried to join. <b>Denied.</b>");
                    return false;
                }
                */
                MPKingdomsTest.OnPlayerJoined(__instance);
                BetterServerPlayer.GetPlayer(player);
                return true;
            }
        }

        /// <summary>
        /// Built in a way that weeds out illegal or crash-causing invalid clients
        /// </summary>
        [HarmonyPatch(typeof(NetPlayer))]
        [HarmonyPatch("OnStartClient")]
        internal static class OnPlayerJoin
        {
            internal static bool Prefix(NetPlayer __instance)
            {
                NetPlayer player = __instance;
                if (player == null)
                {
                    DebugBeS.Assert("The hecc?  We were handed NULL player");
                    return true;
                }
                DebugBeS.Log("OnStartClient for " + player.name);
                KickStartBetterServers.ProtectedServerDelay();
                return true;
            }
        }

        [HarmonyPatch(typeof(UIScoreBoardEntry))]
        [HarmonyPatch("SetPlayer")]// Setup main menu techs
        internal static class AddNewIcons
        {
            internal static void Postfix(UIScoreBoardEntry __instance, ref NetPlayer player, ref UIScoreBoardHUD scoreboard)
            {
                UIScoreboardEntryExt.Insure(__instance, player, scoreboard);
            }
        }


        [HarmonyPatch(typeof(ManNetwork))]
        [HarmonyPatch("RemovePlayer")]// Setup main menu techs
        internal static class OnPlayerLeaving
        {
            internal static void Prefix(ManNetwork __instance, ref NetPlayer player)
            {
                if (player == null || !ManNetwork.IsHost)
                    return;
                KickStartBetterServers.OnPlayerDisconnect(player);
                MPKingdomsTest.OnPlayerLeft(player);
            }
        }

        [HarmonyPatch(typeof(NetTech))]
        [HarmonyPatch("OnDeserialize")]// deal with tech loading
        internal static class HandleTechDeviation
        {
            internal static void Postfix(NetTech __instance)
            {
                if (!__instance || !ManNetwork.IsHost)
                    return;
                DebugBeS.Log("OnDeserialize");
                NetPlayer NP = __instance.NetPlayer;
                if (NP)
                {
                    DebugBeS.Log("Player " + NP.name);
                    int infractionCount = KickStartBetterServers.OnTechSpawn(__instance, __instance.NetPlayer);
                    if (BetterServerPlayer.GivePlayerInfractions(__instance.NetPlayer, infractionCount))
                    {
                        PersistentPlayerID playerRealID = MPKingdomsTest.GetPlayerID(NP);
                        DebugBeS.Log("Infraction - player Tech does not abide by the rules set");
                        if (KickStartBetterServers.blockedInfo.AutoBlock)
                        {
                            if (!ManNetworkLobby.inst.LobbySystem.BanList.IsPlayerBanned(playerRealID))
                                ManNetworkLobby.inst.LobbySystem.CurrentLobby.BanPlayer(NP.GetPlayerIDInLobby());
                            else
                            {
                                KickStartBetterServers.ChatLog("User " + (NP.name.NullOrEmpty() ? "NULL NAME" : NP.name) +
                                    " has evaded the auto-block system, please notify the devs to fix this issue ASAP.");
                                DebugBeS.Log("Vanilla has failed us, time to brute-force eject kick them!");
                                try
                                {
                                    NP.ServerKickPlayer();
                                }
                                catch
                                {
                                    KickStartBetterServers.ChatLog("fallback kick FAILED");
                                    DebugBeS.Log("Direct kicking failed!!!");
                                }
                            }
                        }
                        try
                        {
                            NP.ServerKickPlayer();
                        }
                        catch { }
                    }
                }
            }
        }


        // ------------------------------------------------------
        //                      MP Changes
        // ------------------------------------------------------

        [HarmonyPatch(typeof(ManSpawn))]
        [HarmonyPatch("IsBlockAllowedInCurrentGameMode")]//
        private class PermitAdditionalBlocks
        {
            /*
            static FieldInfo loadouts = typeof(ModePVP<>)
                       .GetField("m_AvailableLoadouts", BindingFlags.NonPublic | BindingFlags.Instance);
            */
            internal static bool Prefix(ref BlockTypes blockType, ref bool __result)
            {
                if (DeathmatchExt.PermittedBlocksDefault != null && DeathmatchExt.PermittedBlocksDefault.Contains(blockType))
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        public static bool lieOnce = false;
        [HarmonyPatch(typeof(ManMods))]
        [HarmonyPatch("AutoAssignIDs", new Type[4] { typeof(ModSessionInfo), typeof(List<string>), typeof(Dictionary<string, List<string>>), typeof(List<string>) })]//
        private class BypassCorpBlock
        {
            internal static void Prefix()
            {
                //lieOnce = true;
            }
        }
        [HarmonyPatch(typeof(ManGameMode))]
        [HarmonyPatch("GetCurrentGameType")]//
        private class BypassCorpBlock2
        {
            internal static bool Prefix(ref ManGameMode.GameType __result)
            {
                if (lieOnce)
                {
                    __result = ManGameMode.GameType.CoOpCreative;
                    lieOnce = false;
                    return false;
                }
                return true;
            }
        }


        [HarmonyPatch(typeof(MultiplayerTechSelectGroupAsset))]
        [HarmonyPatch("GetTechPresets")]//
        private class ChangeDeathmatchChoices
        {
            /*
            static FieldInfo loadouts = typeof(ModePVP<>)
                       .GetField("m_AvailableLoadouts", BindingFlags.NonPublic | BindingFlags.Instance);
            */
            internal static void Postfix(MultiplayerTechSelectGroupAsset __instance, ref List<MultiplayerTechSelectPresetAsset> __result)
            {
                DeathmatchExt.FirstLoadDeathmatchStartersPage(__instance, ref __result, DeathmatchExt.SelectedPage);
            }
        }

        [HarmonyPatch(typeof(UIScreenMultiplayerTechSelect))]
        [HarmonyPatch("Update")]//
        private class DeathmatchChoicesMultiMenu
        {
            private static bool setup = false;
            internal static void Postfix(UIScreenMultiplayerTechSelect __instance)
            {
                /*
                if (!setup)
                {
                    Debug.Log("Getting menu info...");
                    Debug.Log(Nuterra.NativeOptions.UIUtilities.GetComponentTree(__instance.gameObject));
                    setup = true;
                }*/
                DeathmatchExt.UpdateSelection();
                DeathmatchExt.LoadDeathmatchStartersPage(__instance, DeathmatchExt.SelectedPage);
            }
        }


        // -----------------------  New Gamemodes!  -------------------------

        [HarmonyPatch(typeof(UIScreenNetworkLobby))]
        [HarmonyPatch("Show")]//
        private class ExpandDeathmatchOptionsStart
        {
            internal static void Postfix(UIScreenNetworkLobby __instance)
            {
                TechFortress2.ModLobby(__instance);
            }
        }
        [HarmonyPatch(typeof(UIScreenNetworkLobby))]
        [HarmonyPatch("Hide")]//
        private class ExpandDeathmatchOptionsEnd
        {
            internal static void Postfix(UIScreenNetworkLobby __instance)
            {
                TechFortress2.ModLobbyEnd(__instance);
            }
        }

        [HarmonyPatch(typeof(UIScreenNetworkLobby))]
        [HarmonyPatch("SetHost")]//
        private class ExpandDeathmatchOptionsHost
        {
            internal static void Postfix(UIScreenNetworkLobby __instance, ref bool isHost)
            {
                TechFortress2.SetHost(__instance, isHost);
            }
        }
    }
}
