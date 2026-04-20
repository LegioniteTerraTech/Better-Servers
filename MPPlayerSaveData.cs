using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.Networking;

namespace Better_Servers
{
    /// <summary>
    /// This must be loaded by all clients, but IS NOT RUN ON THEM
    /// </summary>
    public class MPPlayerSaveData
    {
        public const int MaxLoadingAttempts = 8;

        public string playerID;
        /// <summary> Serialized position </summary>
        public WorldPosition position;
        public int tankID;
        public string lastName = "unknown";
        public string LastDeathReason;

        [JsonIgnore]
        public NetPlayer directPlayer;
        [JsonIgnore]
        public NetTech directTech;
        [JsonIgnore]
        public int loadingAttempts = 0;
        public bool GaveUpOnTechSearch => loadingAttempts > MaxLoadingAttempts;

        [JsonIgnore]
        public MPPlayerTileLoader tileLoader = null;
        /// <summary> Active tile coordinate </summary>
        [JsonIgnore]
        public IntVector2 CurTileCoord = default;
        [JsonIgnore]
        public static List<IntVector2> tiles = new List<IntVector2>();


        internal void OnSave()
        {
            var tech = directTech?.tech;
            if (tech != null)
            {
                tankID = tech.visible.ID;
                position = WorldPosition.FromScenePosition(tech.boundsCentreWorldNoCheck);
            }
        }

        /// <summary>
        /// Attaches the player to this MPPlayerSaveData instance.
        /// <b>SERVER ONLY</b>
        /// </summary>
        /// <param name="inst">The player associated with this so that it can keep track of them.</param>
        internal void OnPlayerJoined_SERVER(NetPlayer inst)
        {
            if (!OnPlayerJoined(inst))
                return;
            if (directPlayer == ManNetwork.inst.MyPlayer)
            {
                DebugBeS.Log("Player " + inst.name + " is the server host");
                MPKingdomsTest.BarrierOrigin = position;
                MPKingdomsTest.FoundOurLocalPlayer = true;
            }
            MPKingdomsTest.TryMoveBarrier(directPlayer, position);
            TryRushPlayerLoadingArea();
            TryRelinkToTechPlayerJoin();
        }
        /// <summary>
        /// Attaches the player to this MPPlayerSaveData instance.
        /// </summary>
        /// <param name="inst">The player associated with this so that it can keep track of them.</param>
        internal bool OnPlayerJoined(NetPlayer inst)
        {
            if (inst == null)
                throw new ArgumentNullException("param (NetPlayer)inst");
            if (directPlayer != null)
            {
                DebugBeS.Warning("Player " + inst.name + " joined, but a player with the EXACT same UID was already registered?... - " + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            DebugBeS.Log("Player " + inst.name + " joined, now loading their data...");
            directPlayer = inst;
            lastName = inst.name;
            loadingAttempts = 0;
            CurTileCoord = position.TileCoord;

            tileLoader = new MPPlayerTileLoader(inst, CurTileCoord);
            ManWorldTileExt.ClientRegisterDynamicTileLoader(tileLoader);
            return true;
        }
        /// <summary>
        /// Detaches the player from this MPPlayerSaveData instance.
        /// </summary>
        internal bool OnPlayerLeft()
        {
            if (directPlayer == null)
            {
                DebugBeS.Warning("Player " + lastName + " left, but a player with the EXACT same UID already left?... - " + StackTraceUtility.ExtractStackTrace());
                return false;
            }

            ManWorldTileExt.ClientUnregisterDynamicTileLoader(tileLoader);
            tileLoader = null;
            if (directTech?.tech != null)
            {
                tankID = directTech.tech.visible.ID;
                position = WorldPosition.FromScenePosition(directTech.tech.boundsCentreWorldNoCheck);
            }
            directPlayer = null;
            return true;
        }
        /// <summary>
        /// Detaches the player from this MPPlayerSaveData instance.
        /// <b>SERVER ONLY</b>
        /// </summary>
        internal void OnPlayerLeft_SERVER()
        {
            if (!OnPlayerLeft())
                return;

            DebugBeS.Log("Player " + lastName + " left, saving their data...");
        }
        internal void TryFindTechAfterTileLoaded()
        {
            if (directPlayer == null)
            {
                DebugBeS.Warning("TryFindTech() called whilist directPlayer IS NULL");
                return;
            }
            if (tankID == int.MinValue)
            {
                if (directPlayer.CurTech?.tech != null)
                {
                    DebugBeS.Log(lastName + " has an assigned Tech, and we are linking it now");
                    tankID = directPlayer.CurTech.tech.visible.ID;
                    directTech = directPlayer.CurTech;
                }
                else
                {
                    DebugBeS.Info(lastName + "'s assigned Tech isn't in the tile, waiting for it...");
                    return;
                }
            }
            if (directTech?.tech == null)
            {
                var TV = ManVisible.inst.GetTrackedVisible(tankID);
                if (TV != null)
                {
                    if (TV.visible?.tank?.netTech != null)
                    {
                        DebugBeS.Log(lastName + " has a loaded Tech in the world, and we are linking it now");
                        directTech = TV.visible.tank.netTech;
                        LastDeathReason = null;
                        return;
                    }
                    //else
                    //    DebugBeS.Log(lastName + " does not have a loaded tech???");
                }
            }
            // Continue waiting for tiles to load
        }
        internal void TryRelinkToTechPlayerJoin()
        {
            if (directPlayer == null)
            {
                DebugBeS.Warning("TryRelinkToTech() called whilist directPlayer IS NULL");
                return;
            }
            if (tankID == int.MinValue)
            {
                if (directPlayer.CurTech?.tech != null)
                {
                    DebugBeS.Log(lastName + " has an assigned Tech, and we are re-linking it now");
                    tankID = directPlayer.CurTech.tech.visible.ID;
                    directTech = directPlayer.CurTech;
                }
                else
                {
                    DebugBeS.Log(lastName + " does not have an assigned Tech");
                    loadingAttempts = 9001;
                    return;
                }
            }
            if (directTech?.tech != null)
            {
                directPlayer.ServerSetTech(directTech, false);
                DebugBeS.Log("Found Tech for " + lastName + " and directly linking to Tech " + directTech.tech.name);
                return;
            }
            else
            {
                var TV = ManVisible.inst.GetTrackedVisible(tankID);
                if (TV != null)
                {
                    if (TV.visible?.tank?.netTech != null)
                    {
                        directTech = TV.visible.tank.netTech;
                        directPlayer.ServerSetTech(directTech, false);
                        LastDeathReason = null;
                        DebugBeS.Log("Found Tech for " + lastName + " and load linking to Tech " + directTech.tech.name);
                        return;
                    }
                    else
                    {
                        InvokeHelper.Invoke(TryJumpPlayerToTech, 1);
                        DebugBeS.Log("Trying to load Tech for " + lastName + " as it is far away");
                        return;
                    }
                }
            }
            // No tech exists!
            DebugBeS.Log("Could not find assigned Tech for " + lastName);
        }
        private void TryJumpPlayerToTech()
        {
            if (directPlayer == null)
            {
                DebugBeS.Warning("TryJumpPlayerToTech() called whilist directPlayer IS NULL");
                return;
            }
            if (GaveUpOnTechSearch)
            {
                DebugBeS.Warning("FAILED to load Tech for " + lastName + "!");
                if (!LastDeathReason.NullOrEmpty())
                {
                    MPKingdomsTest.SendBigBannerNetwork(LastDeathReason, true, directPlayer);
                }
                return;
            }
            loadingAttempts++;
            var TV = ManVisible.inst.GetTrackedVisible(tankID);
            if (TV != null)
            {
                if (TV.visible?.tank?.netTech != null)
                {
                    directTech = TV.visible.tank.netTech;
                    directPlayer.ServerSetTech(directTech, false);
                    DebugBeS.Log("[" + loadingAttempts + "] Found Tech for " + lastName + " and load linking to Tech " + directTech.tech.name);
                    return;
                }
                else
                {
                    InvokeHelper.Invoke(TryJumpPlayerToTech, 1);
                    KeepRushingPlayerLoadingArea();
                    DebugBeS.Log("[" + loadingAttempts + "] Trying to load Tech for " + lastName + " as it is far away");
                    return;
                }
            }
            // No tech exists!
            DebugBeS.Log("[" + loadingAttempts + "] Could not find assigned Tech for " + lastName);
        }

        internal void UpdateUser()
        {
            if (directPlayer != null)
            {
                if (directPlayer.CurTech != directTech)
                {
                    directTech = directPlayer.CurTech;
                    if (directTech?.tech != null)
                    {
                        tankID = directTech.tech.visible.ID;
                    }
                    else
                        tankID = int.MinValue;
                }
                UpdateTileLoader();
                if (ManNetwork.IsHost)
                    UpdatePlayerBarrier_SERVER();
            }
        }

        /// <summary>
        /// Update player tileloading from Server 
        /// </summary>
        private void UpdateTileLoader()
        {
        }


        /// <summary>
        /// This insures that the user has fully loaded WorldTiles around the next area for the bounds before moving the bounds to that area!<br/>
        /// <b>SERVER ONLY</b>
        /// </summary>
        private void UpdatePlayerBarrier_SERVER()
        {
            if (directPlayer?.CurTech?.tech != null)
            {   // Our managed player has a fully ready Tech assigned to them!
                WorldPosition WP = WorldPosition.FromScenePosition(directPlayer.CurTech.tech.boundsCentreWorldNoCheck);
                IntVector2 newCoord = WP.TileCoord;
                if (newCoord != CurTileCoord)
                {   // The player is in a new WorldTile
                    tiles.Clear();
                    ManWorldTileExt.GetActiveTilesAround(tiles, WP, MPKingdomsTest.MinTileLoaderRadius);
                    foreach (IntVector2 tile in tiles)
                    {
                        WorldTile tileInst = ManWorld.inst.TileManager.LookupTile(tile, false);
                        if (tileInst == null)
                            return; // Abort if tiles not loaded!
                    }
                    CurTileCoord = newCoord;

                    MPKingdomsTest.TryMoveBarrier(directPlayer, WP);
                    DebugBeS.Log("Moved bounds for " + lastName + " to " + CurTileCoord.ToString());
                }
            }
        }

        public void TryRushPlayerLoadingArea()
        {
            IntVector2 pos = position.TileCoord + new IntVector2(
                -MPKingdomsTest.MinTileLoaderRadius, -MPKingdomsTest.MinTileLoaderRadius);
            foreach (var item in pos.IterateRectVolume(new IntVector2(
                MPKingdomsTest.MinTileLoaderRadius, MPKingdomsTest.MinTileLoaderRadius)))
            {
                ManWorldTileExt.ClientTempLoadTile(item, true, MaxLoadingAttempts);
            }
        }
        private void KeepRushingPlayerLoadingArea()
        {
            IntVector2 pos = position.TileCoord + new IntVector2(
                -MPKingdomsTest.MinTileLoaderRadius, -MPKingdomsTest.MinTileLoaderRadius);
            foreach (var item in pos.IterateRectVolume(new IntVector2(
                MPKingdomsTest.MinTileLoaderRadius, MPKingdomsTest.MinTileLoaderRadius)))
            {
                ManWorldTileExt.ClientTempLoadTile(item, true, 3);
            }
        }
    }
}
