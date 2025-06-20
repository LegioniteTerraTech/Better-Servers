using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using SafeSaves;
using UnityEngine;
using TerraTech.Network;
using Newtonsoft.Json;
using UnityEngine.Networking;
using Payload.UI.Commands;
using System.Security.Cryptography;

namespace Better_Servers
{
    /// <summary>
    /// Loaded by all clients
    /// </summary>
    public class MPPlayerSaveData
    {
        public const int MaxLoadingAttempts = 8;

        public string playerID;
        public WorldPosition position;
        public int tankID;
        public string LastDeathReason;

        [JsonIgnore]
        public NetPlayer directPlayer;
        [JsonIgnore]
        public NetTech directTech;
        [JsonIgnore]
        public int loadingAttempts = 0;
        public bool GaveUpOnTechSearch => loadingAttempts > MaxLoadingAttempts;

        internal void OnSaving(NetPlayer inst, NetTech netTech, Tank playerTech)
        {
            tankID = playerTech.visible.ID;
            position = WorldPosition.FromScenePosition(playerTech.boundsCentreWorldNoCheck);
        }
        [Server]
        internal void OnPlayerJoined(NetPlayer inst)
        {
            if (directPlayer != null)
                return;
            DebugBeS.Log("Player " + inst.name + " joined, now loading their data...");
            directPlayer = inst;
            loadingAttempts = 0;
            if (directPlayer == ManNetwork.inst.MyPlayer)
            {
                MPKingdomsTest.BarrierOrigin = position;
                MPKingdomsTest.FoundOurPlayer = true;
            }
            CurCoord = position.TileCoord;
            MPKingdomsTest.MovePlayerLimiter(position, directPlayer);
            TryRushPlayerLoadingArea();
            TryRelinkToTech();
        }
        internal void OnPlayerLeft()
        {
            if (directPlayer == null)
                return;
            DebugBeS.Log("Player " + directPlayer.name + " left, saving their data...");
            if (directTech?.tech != null)
            {
                tankID = directTech.tech.visible.ID;
                position = WorldPosition.FromScenePosition(directTech.tech.boundsCentreWorldNoCheck);
            }
        }
        internal void TryFindTech()
        {
            if (directPlayer == null)
            {
                DebugBeS.Assert("TryFindTech() called whilist directPlayer IS NULL");
                return;
            }
            if (tankID == int.MinValue)
            {
                if (directPlayer.CurTech?.tech != null)
                {
                    tankID = directPlayer.CurTech.tech.visible.ID;
                    directTech = directPlayer.CurTech;
                }
                else
                {
                    DebugBeS.Log(directPlayer.name + " does not have an assigned Tech");
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
                        directTech = TV.visible.tank.netTech;
                        LastDeathReason = null;
                        return;
                    }
                }
            }
        }
        internal void TryRelinkToTech()
        {
            if (directPlayer == null)
            {
                DebugBeS.Assert("TryRelinkToTech() called whilist directPlayer IS NULL");
                return;
            }
            if (tankID == int.MinValue)
            {
                if (directPlayer.CurTech?.tech != null)
                {
                    tankID = directPlayer.CurTech.tech.visible.ID;
                    directTech = directPlayer.CurTech;
                }
                else
                {
                    DebugBeS.Log(directPlayer.name + " does not have an assigned Tech");
                    loadingAttempts = 9001;
                    return;
                }
            }
            if (directTech?.tech != null)
            {
                directPlayer.ServerSetTech(directTech, false);
                DebugBeS.Log("Found Tech for " + directPlayer.name + " and directly linking to Tech " + directTech.tech.name);
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
                        DebugBeS.Log("Found Tech for " + directPlayer.name + " and load linking to Tech " + directTech.tech.name);
                        return;
                    }
                    else
                    {
                        InvokeHelper.Invoke(TryJumpPlayerToTech, 1);
                        DebugBeS.Log("Trying to load Tech for " + directPlayer.name + " as it is far away");
                        return;
                    }
                }
            }
            // No tech exists!
            DebugBeS.Log("Could not find assigned Tech for " + directPlayer.name);
        }
        private void TryJumpPlayerToTech()
        {
            if (directPlayer == null)
            {
                DebugBeS.Assert("TryJumpPlayerToTech() called whilist directPlayer IS NULL");
                return;
            }
            if (GaveUpOnTechSearch)
            {
                DebugBeS.Log("FAILED to load Tech for " + directPlayer.name + "!");
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
                    DebugBeS.Log("[" + loadingAttempts + "] Found Tech for " + directPlayer.name + " and load linking to Tech " + directTech.tech.name);
                    return;
                }
                else
                {
                    InvokeHelper.Invoke(TryJumpPlayerToTech, 1);
                    KeepRushingPlayerLoadingArea();
                    DebugBeS.Log("[" + loadingAttempts + "] Trying to load Tech for " + directPlayer.name + " as it is far away");
                    return;
                }
            }
            // No tech exists!
            DebugBeS.Log("[" + loadingAttempts + "] Could not find assigned Tech for " + directPlayer.name);
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
                if (ManNetwork.IsHost)
                    UpdateUserBounds();
            }
        }


        [JsonIgnore]
        public IntVector2 CurCoord = default;
        [JsonIgnore]
        public static List<IntVector2> tiles = new List<IntVector2>();
        private void UpdateUserBounds()
        {
            if (MPKingdomsTest.tileLoaders.ContainsKey(directPlayer) && directPlayer?.CurTech?.tech != null)
            {
                WorldPosition WP = WorldPosition.FromScenePosition(directPlayer.CurTech.tech.boundsCentreWorldNoCheck);
                IntVector2 newCoord = WP.TileCoord;
                if (newCoord != CurCoord)
                {
                    tiles.Clear();
                    ManWorldTileExt.GetActiveTilesAround(tiles, WP, 2);
                    foreach (IntVector2 tile in tiles)
                    {
                        WorldTile tileInst = ManWorld.inst.TileManager.LookupTile(tile, false);
                        if (tileInst == null)
                            return; // Abort if tiles not loaded!
                    }
                    CurCoord = newCoord;

                    MPKingdomsTest.MovePlayerLimiter(WP, directPlayer);
                    DebugBeS.Log("Moved bounds for " + directPlayer.name + " to " + CurCoord.ToString());
                }
            }
        }

        public void TryRushPlayerLoadingArea()
        {
            IntVector2 pos = position.TileCoord + new IntVector2(
                -MPKingdomsTest.PlayerEnterTileLoadDist, -MPKingdomsTest.PlayerEnterTileLoadDist);
            foreach (var item in pos.IterateRectVolume(new IntVector2(
                MPKingdomsTest.PlayerEnterTileLoadDist, MPKingdomsTest.PlayerEnterTileLoadDist)))
            {
                ManWorldTileExt.ClientTempLoadTile(item, true, MaxLoadingAttempts);
            }
        }
        private void KeepRushingPlayerLoadingArea()
        {
            IntVector2 pos = position.TileCoord + new IntVector2(
                -MPKingdomsTest.PlayerEnterTileLoadDist, -MPKingdomsTest.PlayerEnterTileLoadDist);
            foreach (var item in pos.IterateRectVolume(new IntVector2(
                MPKingdomsTest.PlayerEnterTileLoadDist, MPKingdomsTest.PlayerEnterTileLoadDist)))
            {
                ManWorldTileExt.ClientTempLoadTile(item, true, 3);
            }
        }
    }

    /// <summary>
    /// This extends the effective range of players by, A TON, but can be unstable and is WIP!
    /// Essentially each player gets their own loading zone, like in Minecraft MP.
    ///   Bad performance is expected, TerraTech really cannot handle multiple players at long distances lol
    /// </summary>
    [AutoSaveManager]
    public class MPKingdomsTest
    {
        public const int ExtendedPlayerRange = 50000;
        public const int PlayerEnterTileLoadDist = 1;

        public static bool FakePlayerOrigin = false;
        public static bool ExtendENTIRERadiusAll = false;
        public static bool FoundOurPlayer = false;

        /// <summary>
        /// Sets how far each player loads tiles for.
        ///   This makes it so every player has the same loading range as the server host, in theory...
        /// </summary>
        public static int PlayerTileLoadDistOuter => PlayerTileLoadDist + 1;
        public static int PlayerTileLoadDistInner => PlayerTileLoadDist;
        public static int PlayerTileLoadDist => Mathf.Clamp(Mathf.CeilToInt((Singleton.camera.farClipPlane / ManWorld.inst.TileSize) + 0.5f), 2, 5);

        [SSManagerInst]
        public static MPKingdomsTest inst = new MPKingdomsTest();
        [SSaveField]
        public Dictionary<string, MPPlayerSaveData> PlayerData = new Dictionary<string, MPPlayerSaveData>();


        public class BigBannerMessage : MessageBase
        {
            public BigBannerMessage() { }
            public BigBannerMessage(string desc, bool warnNoise)
            {
                this.desc = desc;
                this.warnNoise = warnNoise;
            }

            public string desc;
            public bool warnNoise;
        }
        internal static NetworkHook<BigBannerMessage> netHookBanner = new NetworkHook<BigBannerMessage>(
            "BetterServers.BigBannerMessage", OnBigBannerNetwork, NetMessageType.ToClientsOnly);

        public static bool SendBigBannerNetwork(string desc, bool warnNoise, NetPlayer targetPlayer)
        {
            if (targetPlayer == ManNetwork.inst.MyPlayer && ManNetwork.IsHost)
            {
                OnBigBannerNetwork(new BigBannerMessage(desc, warnNoise), true);
                return true;
            }
            else
            {
                return netHookBanner.TryBroadcastTarget(new BigBannerMessage(desc, warnNoise), targetPlayer);
            }
        }
        private static bool OnBigBannerNetwork(BigBannerMessage command, bool isServer)
        {
            UIMultiplayerHUD hudBanner = (UIMultiplayerHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.Multiplayer);
            if (command.warnNoise)
                hudBanner.Message1.SetTextWithTimeout(command.desc, UIMultiplayerHUD.Message.StateTypes.Connection, 3f, Ending);
            else
                hudBanner.Message1.SetTextWithTimeout(command.desc, UIMultiplayerHUD.Message.StateTypes.Connection, 3f);
            return true;
        }

        private static NetworkHook<WorldPositionMessage> moveLimCall = new NetworkHook<WorldPositionMessage>(
            "BetterServers.WorldPositionMessage", OnMoveLimiterNetwork, NetMessageType.ToClientsOnly);

        internal static bool MovePlayerLimiter(WorldPosition WP, NetPlayer player)
        {
            if (player == ManNetwork.inst.MyPlayer && ManNetwork.IsHost)
            {
                DebugBeS.Log("Set serverside client barrier to " + WP.TileCoord);
                BarrierOrigin = WP;
                MovePushbackBarrierClient(BarrierOrigin.ScenePosition);
                FoundOurPlayer = true;
                return true;
            }
            else
            {
                if (player == null)
                    throw new NullReferenceException("player is null");
                if (player.connectionToServer == null)
                    throw new NullReferenceException("player.connectionToServer is null");
                return moveLimCall.TryBroadcastTarget(new WorldPositionMessage() { m_Position = WP }, player);
            }
        }
        private static bool OnMoveLimiterNetwork(WorldPositionMessage command, bool isServer)
        {
            DebugBeS.Log("Set clientside barrier to " + command.m_Position.TileCoord);
            BarrierOrigin = command.m_Position;
            MovePushbackBarrierClient(BarrierOrigin.ScenePosition);
            FoundOurPlayer = true;
            return true;
        }



        public static void PrepareForSaving()
        {
            foreach (var item in inst.PlayerData)
            {
                TTNetworkID ID = ManNetworkLobby.inst.LobbySystem.GetTTNetworkIDFromPersistent(new PersistentPlayerID(item.Value.playerID));
                NetPlayer netplay = ManNetwork.inst.FindPlayerByNetworkID(ID);
                if (netplay?.CurTech?.tech != null)
                {
                    item.Value.OnSaving(netplay, netplay.CurTech, netplay.CurTech.tech);
                }
            }
            DebugBeS.Log("Saved " + inst.PlayerData.Count + " players");
        }
        public static void FinishedSaving()
        {
        }
        public static void FinishedLoading()
        {
        }
        public static bool TryGetPlayer(NetPlayer player, out MPPlayerSaveData data)
        {
            string playerIDPersist = GetPlayerID(player).ToString();
            return inst.PlayerData.TryGetValue(playerIDPersist, out data) && data != null;
        }
        [Server]
        public static void OnPlayerJoined(NetPlayer player)
        {
            if (inst == null)
                inst = new MPKingdomsTest();
            string playerIDPersist = GetPlayerID(player).ToString();
            if (inst.PlayerData.TryGetValue(playerIDPersist, out var data) && data != null)
            {
                data.OnPlayerJoined(player);
            }
            else
            {
                inst.PlayerData.Remove(playerIDPersist);
                MPPlayerSaveData playerData = new MPPlayerSaveData()
                {
                    playerID = playerIDPersist,
                    tankID = int.MinValue,
                    position = WorldPosition.FromScenePosition(Singleton.playerPos)
                };
                inst.PlayerData.Add(playerIDPersist, playerData);
                DebugBeS.Log("Added new player " + player.name);
                playerData.OnPlayerJoined(player);
            }
        }
        [Server]
        public static void OnPlayerLeft(NetPlayer player)
        {
            if (inst == null)
                inst = new MPKingdomsTest();
            string playerIDPersist = GetPlayerID(player).ToString();
            if (inst.PlayerData.TryGetValue(playerIDPersist, out var data) && data != null)
            {
                data.OnPlayerLeft();
            }
        }


        public class MPPlayerTileLoader : ITileLoader
        {
            internal NetPlayer player;
            internal WorldPosition lastTile = default;
            public MPPlayerTileLoader(NetPlayer player, IntVector2 lastTile)
            { 
                this.player = player;
                this.lastTile = new WorldPosition(lastTile, new Vector3(ManWorld.inst.TileSize / 2f, 0, ManWorld.inst.TileSize / 2f));
            }
            public bool Valid() => lastTile.TileCoord != IntVector2.invalid;//player != null;
            public void GetActiveTiles(List<IntVector2> tiles)
            {
                if (player && player.CurTech?.tech)
                    lastTile = WorldPosition.FromScenePosition(player.CurTech.tech.boundsCentreWorldNoCheck);
                ManWorldTileExt.GetActiveTilesAround(tiles, lastTile, PlayerTileLoadDistOuter);
            }
        }

        private static WorldPushbackBarrier GlobalPushback = null;
        public static Transform GlobalBarrierVisual = null;
        public static Transform BarrierPrefab = null;

        /// <summary>
        /// We are doing things in relation to the PLAYERS, not the world origin!
        /// </summary>
        public static bool AreWeKingdoming = false;
        public static float DDistanceDefault = 0;
        internal static float TPDistanceDefault = 0;
        private static float PushbackDefault = 0;
        private static float PushbackVeloCancel = 0;

        public static WorldPosition BarrierOrigin = default;
        public static Transform ClientBarrierVisual = null;

        private static void MovePushbackBarrierClient(Vector3 scenePos)
        {
            if (GlobalBarrierVisual == null)
            {
                DebugBeS.Assert("Failed to find GlobalBarrierVisual, things will not be able to move properly!!!!");
                return;
            }
            if (BarrierPrefab == null)
            {
                DebugBeS.Assert("Failed to find BarrierPrefab, things will not be able to move properly!!!!");
                return;
            }
            GlobalBarrierVisual.gameObject.SetActive(false);
            Vector3 prevPos = ClientBarrierVisual.transform.position;
            ClientBarrierVisual.position = scenePos.SetY(0);
            BoundaryMesh BM = ClientBarrierVisual.GetComponent<BoundaryMesh>();
            BM.enabled = true;
            BM.Move(prevPos, ClientBarrierVisual.transform.position, 1f);
            DebugBeS.Log("Moved clientside barrier to " + scenePos);
        }

        internal static FieldInfo enemyActRange = typeof(ManTechs).GetField("m_SleepRangeFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        private static float prevRange = 0;
        private static void SetExtendedRange(bool enable)
        {
            if (enable)
            {
                ManNetwork.inst.SetMapSettings(ManNetwork.inst.MapCenter, DDistanceDefault + ExtendedPlayerRange);
                ManNetwork.inst.SetBoundaryPushbackSettings(
                    TPDistanceDefault + ExtendedPlayerRange,
                    ManNetwork.inst.PushBackConst,
                    PushbackDefault + ExtendedPlayerRange,
                    PushbackVeloCancel + ExtendedPlayerRange);
                ManWorldTileExt.FORCEExtendedBroadphase();
            }
            else
            {
                ManNetwork.inst.SetMapSettings(ManNetwork.inst.MapCenter, DDistanceDefault);
                ManNetwork.inst.SetBoundaryPushbackSettings(
                    TPDistanceDefault,
                    ManNetwork.inst.PushBackConst,
                    PushbackDefault,
                    PushbackVeloCancel);
            }
        }

        private static void ActivateKingdoms(Mode mode)
        {
            FoundOurPlayer = false;
            /*
            if (mode.GetGameType() == ManGameMode.GameType.CoOpCampaign)
                ExtendAll<ModeCoOpCampaign>(mode);
            else if (mode.GetGameType() == ManGameMode.GameType.CoOpCreative)
                ExtendAll<ModeCoOpCreative>(mode);
            */

            InsureDefaults();

            if (GlobalPushback == null)
            {
                GlobalPushback = UnityEngine.Object.FindObjectOfType<WorldPushbackBarrier>();
                if (GlobalPushback == null)
                    DebugBeS.Assert("Failed to find WorldPushbackBarrier, things will not be able to move properly!!!!");
            }

            if (prevRange == 0)
                prevRange = (float)enemyActRange.GetValue(ManTechs.inst);
            if ((float)enemyActRange.GetValue(ManTechs.inst) != ExtendedPlayerRange)
            {   // MEGA RANGE
                enemyActRange.SetValue(ManTechs.inst, ExtendedPlayerRange);
                DebugBeS.Log("Extended enemy Tech interaction range to " + ExtendedPlayerRange + ".");
            }
            if (ClientBarrierVisual == null)
            {
                BoundaryMesh BMOG = BarrierPrefab.GetComponentInChildren<BoundaryMesh>(true);
                ClientBarrierVisual = UnityEngine.Object.Instantiate(BMOG.transform, null);
                ClientBarrierVisual.localScale = BMOG.transform.lossyScale;
                ClientBarrierVisual.position = Vector3.zero;
                ClientBarrierVisual.rotation = Quaternion.identity;
                ClientBarrierVisual.gameObject.SetActive(false);//true);
                DebugBeS.Log("Made NEW clientside barrier");
            }
            SetExtendedRange(true);
            /*
            if (ClientPushback == null || ManNetwork.IsHost)
            {
                if (ExtendENTIRERadiusAll)
                {
                    //SetExtendedRange(true);
                    if (ManNetwork.IsHost)
                        DebugBeS.Log("Extended Multiplayer world range for own player host");
                    else
                        DebugBeS.Log("Extended Multiplayer world range for own player client THAT FAILED TO LOAD WorldPushbackBarrier IN TIME");
                }
                else if (ManNetwork.IsHost)
                    DebugBeS.Log("Clamped Multiplayer world range for own player host only");
                else
                    DebugBeS.Log("Clamped Multiplayer world range for own player client THAT FAILED TO LOAD WorldPushbackBarrier IN TIME");
            }
            else
                DebugBeS.Log("Clamped Multiplayer world range for own player client only");
            */
            ManWorld.inst.TileManager.TileLoadedEvent.Subscribe(OnTileLoaded);
            ManTechs.inst.TankRecycledEvent.Subscribe(OnTechRecycled);
            ManTechs.inst.TankDestroyedEvent.Subscribe(OnTechDestroyed);
            InvokeHelper.InvokeSingleRepeat(UpdateAllPlayers, 0.1f);

            if (GlobalPushback != null)
                GlobalPushback.enabled = false;

            AreWeKingdoming = true;
            UpdateAllPlayers();
            DebugBeS.Log("Success on booting Kingdoms");
        }
        private static void DeactivateKingdoms(Mode mode)
        {
            ManWorld.inst.TileManager.TileLoadedEvent.Unsubscribe(OnTileLoaded);
            ManTechs.inst.TankRecycledEvent.Unsubscribe(OnTechRecycled);
            InvokeHelper.CancelInvokeSingleRepeat(UpdateAllPlayers);
            if (ClientBarrierVisual != null)
            {
                ClientBarrierVisual.Recycle();
                ClientBarrierVisual = null;
            }
            InvokeHelper.CancelInvokeSingleRepeat(UpdateAllPlayers);
            if (prevRange != 0)
            {
                enemyActRange.SetValue(ManTechs.inst, prevRange);
                DebugBeS.Log("Reverted enemy Tech interaction range to " + ExtendedPlayerRange + ".");
            }
            AreWeKingdoming = false;
            DebugBeS.Log("Success on de-booting Kingdoms");
        }

        internal static void InitHooks()
        {
            netHookBanner.Enable();
            moveLimCall.Enable();
        }

        private static void OnTileLoaded(WorldTile tile)
        {
            if (tile != null)
            {
                foreach (var item in inst.PlayerData)
                {
                    if (item.Value.directTech == null)
                    {
                        item.Value.TryFindTech();
                    }
                }
            }
        }

        private static void OnTechDestroyed(Tank tech, ManDamage.DamageInfo killBlow)
        {
            foreach (var item in inst.PlayerData)
            {
                if (item.Value.directTech != null && tech == item.Value.directTech.tech &&
                    item.Value.LastDeathReason.NullOrEmpty())
                {
                    if (killBlow.SourceTank != null)
                    {
                        if (killBlow.SourceTank.netTech?.NetPlayer != null)
                            item.Value.LastDeathReason = "Destroyed by " + killBlow.SourceTank.netTech?.NetPlayer.name;
                        else
                            item.Value.LastDeathReason = "Destroyed by " + killBlow.SourceTank.name;
                    }
                    else if (killBlow.Source != null)
                    {
                        if (killBlow.Source is Explosion)
                            item.Value.LastDeathReason = "Blown up";
                        else
                            item.Value.LastDeathReason = "Destroyed by " + killBlow.Source.name;
                    }
                    else
                        item.Value.LastDeathReason = "Obliterated";
                }
            }
        }
        private static void OnTechRecycled(Tank tech)
        {
            foreach (var item in inst.PlayerData)
            {
                if (item.Value.directTech != null && tech == item.Value.directTech.tech &&
                    item.Value.LastDeathReason.NullOrEmpty())
                {
                    item.Value.LastDeathReason = "In inventory";
                }
            }
        }


        public static void InformAllTeamPlayersBIGBanner(string desc, bool warnNoise, int teamID)
        {
            foreach (var item in inst.PlayerData)
            {
                if (item.Value.directPlayer != null && item.Value.directPlayer.TechTeamID == teamID)
                {
                    SendBigBannerNetwork(desc, warnNoise, item.Value.directPlayer);
                }
            }
        }




        public static Dictionary<NetPlayer, MPPlayerTileLoader> tileLoaders = new Dictionary<NetPlayer, MPPlayerTileLoader>();
        public static PersistentPlayerID GetPlayerID(NetPlayer NP) =>
            ManNetworkLobby.inst.LobbySystem.GetPersistentPlayerID(NP.GetPlayerIDInLobby());
        public static void InsureDefaults()
        {
            if (DDistanceDefault == 0)
                DDistanceDefault = ManNetwork.inst.DangerDistance;
            if (TPDistanceDefault == 0)
                TPDistanceDefault = ManNetwork.inst.TeleportDistance;
            if (PushbackDefault == 0)
                PushbackDefault = ManNetwork.inst.PushBackDistance;
            if (PushbackVeloCancel == 0)
                PushbackVeloCancel = ManNetwork.inst.PushBackVelocityCancel;
        }
        public static void ExtendAll<T>(Mode mode) where T : ModeCoOp<T>
        {
            InsureDefaults();
            try
            {
                typeof(ModeCoOp<T>).GetField("m_BoundaryDistance", BindingFlags.Instance | BindingFlags.NonPublic).
                    SetValue(mode, ExtendedPlayerRange);
            }
            catch { DebugBeS.Log("Failed to change m_BoundaryDistance"); }
            try
            {
                typeof(ModeCoOp<T>).GetField("m_BoundaryTeleportDistance", BindingFlags.Instance | BindingFlags.NonPublic).
                    SetValue(mode, ExtendedPlayerRange);
            }
            catch { DebugBeS.Log("Failed to change m_BoundaryTeleportDistance"); }
            try
            {
                typeof(ModeCoOp<T>).GetField("m_BoundaryMessageDistance", BindingFlags.Instance | BindingFlags.NonPublic).
                    SetValue(mode, TPDistanceDefault + ExtendedPlayerRange);
            }
            catch { DebugBeS.Log("Failed to change m_BoundaryMessageDistance"); }
            try
            {
                typeof(ModeCoOp<T>).GetField("m_PushBackDistance", BindingFlags.Instance | BindingFlags.NonPublic).
                    SetValue(mode, PushbackDefault + ExtendedPlayerRange);
            }
            catch { DebugBeS.Log("Failed to change m_PushBackDistance"); }
            DebugBeS.Log("MPKingdomsTest.ExtendAll<" + mode.GetType().Name + ">()");
        }
        public static void OnSwitchMode(Mode mode)
        {
            try
            {
                if (mode != null &&
                    (mode.GetGameType() == ManGameMode.GameType.CoOpCampaign ||
                    mode.GetGameType() == ManGameMode.GameType.CoOpCreative))
                {
                    ActivateKingdoms(mode);
                }
            }
            catch (Exception e)
            {
                DebugBeS.Log("Error on extending world range - " + e);
            }
        }
        private static void UpdateAllPlayers()
        {
            foreach (var item in inst.PlayerData)
            {
                item.Value.UpdateUser();
            }
            for (int i = tileLoaders.Count - 1; i >= 0; i--)
            {
                var loader = tileLoaders.ElementAt(i);
                if (!loader.Value.Valid())
                {
                    ManWorldTileExt.ClientUnregisterDynamicTileLoader(loader.Value);
                    tileLoaders.Remove(loader.Key);
                }
            }
            // Update player tileloading from Server
            for (int i = 0; i < ManNetwork.inst.GetNumPlayers(); i++)
            {
                NetPlayer NP = ManNetwork.inst.GetPlayer(i);
                if (!tileLoaders.ContainsKey(NP))
                {
                    MPPlayerTileLoader loader;
                    if (inst.PlayerData.TryGetValue(GetPlayerID(NP).ToString(), out MPPlayerSaveData val))
                        loader = new MPPlayerTileLoader(NP, val.position.TileCoord);
                    else
                        loader = new MPPlayerTileLoader(NP, IntVector2.invalid);
                    tileLoaders.Add(NP, loader);
                    ManWorldTileExt.ClientRegisterDynamicTileLoader(loader);
                }
                if (NP.CurTech?.tech)
                {
                    string playerIDPersist = GetPlayerID(NP).ToString();
                    if (!inst.PlayerData.ContainsKey(playerIDPersist))
                    {
                        inst.PlayerData.Add(playerIDPersist, new MPPlayerSaveData
                        {
                            playerID = playerIDPersist,
                            position = WorldPosition.FromScenePosition(NP.CurTech.tech.boundsCentreWorldNoCheck),
                        });
                    }
                    else
                        inst.PlayerData[playerIDPersist].position = WorldPosition.FromScenePosition(NP.CurTech.tech.boundsCentreWorldNoCheck);
                }
            }
            PreventOverspeedUpdate();
        }

        /// <summary>
        /// Emergency teleport Tech back within loaded lands
        /// </summary>
        private static void PreventOverspeedUpdate()
        {
            Tank tank = Singleton.playerTank;
            if (tank != null && FoundOurPlayer && !ManNetwork.inst.MyPlayer.IsSwitchingTech)
            {
                Vector3 originDelta = (tank.boundsCentreWorldNoCheck - BarrierOrigin.ScenePosition).SetY(0);
                if (originDelta.magnitude > TPDistanceDefault + 25)
                {
                    Vector3 newPos = BarrierOrigin.ScenePosition.SetY(tank.boundsCentreWorldNoCheck.y) +
                        originDelta.SetY(0f).normalized * TPDistanceDefault * 0.95f;
                    UIMultiplayerHUD hudBanner = (UIMultiplayerHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.Multiplayer);
                    hudBanner.Message1.SetTextWithTimeout("You are going too fast", UIMultiplayerHUD.Message.StateTypes.Connection, 3f, Ending);
                    //UIHelpersExt.BigF5broningBanner("You are going too fast", true);
                    ManSFX.inst.PlayMiscLoopingSFX(ManSFX.MiscSfxType.PayloadIncoming);
                    tank.visible.Teleport(newPos, tank.trans.rotation, false, false);
                }
            }
        }
        private static void Ending()
        {
            ManSFX.inst.StopMiscLoopingSFX(ManSFX.MiscSfxType.PayloadIncoming);
        }


        public static void OnModeEndEvent(Mode mode)
        {
            STOPAllPlayersTileLoading();
            if (AreWeKingdoming)
            {
                DeactivateKingdoms(mode);
            }
        }
        private static void STOPAllPlayersTileLoading()
        {
            for (int i = tileLoaders.Count - 1; i >= 0; i--)
            {
                var loader = tileLoaders.ElementAt(i);
                ManWorldTileExt.ClientUnregisterDynamicTileLoader(loader.Value);
                tileLoaders.Remove(loader.Key);
            }
        }
    }
}
