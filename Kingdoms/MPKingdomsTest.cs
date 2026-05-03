using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SafeSaves;
using TerraTech.Network;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.Networking;

namespace Better_Servers
{

    /// <summary>
    /// This extends the effective range of players by, A TON, but can be unstable and is WIP!
    /// Essentially each player gets their own loading zone, like in Minecraft MP.
    ///   Bad performance is expected, TerraTech really cannot handle multiple players at long distances lol
    /// </summary>
    [AutoSaveManager]
    public class MPKingdomsTest
    {
        public const int ExtendedPlayerRange = 50000;
        public const int MinTileLoaderRadius = 2;
        public const int MaxTileLoaderRadius = 5;


        [SSManagerInst]
        public static MPKingdomsTest inst = new MPKingdomsTest();

        [SSaveField]
        public Dictionary<string, MPPlayerSaveData> PlayerData = new Dictionary<string, MPPlayerSaveData>();


        public static NetPlayer localPlayer => ManNetwork.inst?.MyPlayer;
        public static bool FakePlayerOrigin = true;
        public static bool ExtendENTIRERadiusAll = false;
        public const int FoundOurLocalPlayerTimerEnd = 60;
        public static int FoundOurLocalPlayerTimer = 0;

        /// <summary>
        /// Sets how far each player loads tiles for.
        ///   This makes it so every player has the same loading range as the server host, in theory...
        /// </summary>
        public static int PlayerTileLoadDistOuter => PlayerTileLoadDist + 1;
        public static int PlayerTileLoadDistInner => PlayerTileLoadDist;
        public static int PlayerTileLoadDist => Mathf.Clamp(Mathf.CeilToInt((Singleton.camera.farClipPlane / ManWorld.inst.TileSize) + 0.5f), 
            MinTileLoaderRadius, MaxTileLoaderRadius);


        internal static FieldInfo enemyActRange = typeof(ManTechs).GetField("m_SleepRangeFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        private static float prevRange = 0;

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

        /// <summary>
        /// Extends the range for danger distance, the boundry/barrier pushback system, and enforces long-range physics.
        /// <b>Server and Client</b>
        /// </summary>
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

        /// <summary>
        /// Activates the manager for long-distance multiplayer.
        /// <b>Server and Client</b>
        /// </summary>
        private static void ActivateKingdoms(Mode mode)
        {
            FoundOurLocalPlayerTimer = 0;
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
                else
                    DebugBeS.Log("Found WorldPushbackBarrier (In-World physical barrier pushback)");
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
                DebugBeS.Log("Created bounds barrier (In-World visual barrier warning)");
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
            DebugBeS.Log("Success on booting Kingdoms - Long distance mp adjustment");
        }
        /// <summary>
        /// Deactivates the manager for long-distance multiplayer.
        /// <b>Server and Client</b>
        /// </summary>
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
                DebugBeS.Log("Reverted enemy Tech interaction range to " + prevRange + ".");
            }
            AreWeKingdoming = false;
            DebugBeS.Log("Success on de-booting Kingdoms");
        }

        /// <summary>
        /// Start the networking hooks for this
        /// <b>Server and Client</b>
        /// </summary>
        internal static void InitHooks()
        {
            BigBannerNetHook.Enable();
            MoveBarrierNetHook.Enable();
        }


        /// <summary>
        /// EVENTUALLY called by ManTileLoader from a load request on both client and server.
        /// <b>Server and Client</b>
        /// </summary>
        /// <param name="tile">Tile that loaded</param>
        private static void OnTileLoaded(WorldTile tile)
        {
            if (tile != null)
            {
                foreach (var item in inst.PlayerData)
                {
                    if (item.Value.directTech == null)
                    {
                        item.Value.TryFindTechAfterTileLoaded();
                    }
                }
            }
        }

        /// <summary>
        /// When a Tech is destroyed, we want to record what happened and report that to the player
        /// <b>Server and Client</b>
        /// </summary>
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
        /// <summary>
        /// When a Tech is removed from the world, we want to record what happened and report that to the player
        /// <b>Server and Client</b>
        /// </summary>
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

        /// <summary>
        /// When the game's mode is switched.  Enable Kingdoms if needed.
        /// <b>Server and Client</b>
        /// </summary>
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
        /// <summary>
        /// When the game's mode is switched.  Disable Kingdoms if needed.
        /// <b>Server and Client</b>
        /// </summary>
        public static void OnModeEndEvent(Mode mode)
        {
            STOPAllPlayersTileLoading();
            if (AreWeKingdoming)
            {
                DeactivateKingdoms(mode);
            }
        }


        /// <summary>
        /// Update the state of all players in the game.
        /// <b>Server and Client</b>
        /// </summary>
        private static void UpdateAllPlayers()
        {
            if (Singleton.playerTank != null && FoundOurLocalPlayerTimer <= FoundOurLocalPlayerTimerEnd)
                FoundOurLocalPlayerTimer++;
            foreach (var item in inst.PlayerData)
            {
                item.Value.UpdateUser();
            }
            for (int i = 0; i < ManNetwork.inst.GetNumPlayers(); i++)
            {
                NetPlayer NP = ManNetwork.inst.GetPlayer(i);
                if (NP?.CurTech?.tech)
                {
                    if (TryGetPlayer(NP, out var data))
                        data.position = WorldPosition.FromScenePosition(NP.CurTech.tech.boundsCentreWorldNoCheck);
                    else
                        DebugBeS.Assert("NetPlayer " + NP.name + "'s tech exists but they aren't registered in Kingdoms.  This shouldn't happen.");
                }
            }
            UpdateClientWorldBoarder();
        }




        // ---------------------------------------  PLAYER BANNER NOTIFICATION SYSTEM  --------------------------------------- 

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
        internal static NetworkHook<BigBannerMessage> BigBannerNetHook { get; } = new NetworkHook<BigBannerMessage>(
            "BetterServers.BigBannerMessage", OnBigBannerNetwork, NetMessageType.ToClientsOnly);


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

        public static bool SendBigBannerNetwork(string desc, bool warnNoise, NetPlayer targetPlayer)
        {
            if (targetPlayer == ManNetwork.inst.MyPlayer && ManNetwork.IsHost)
            {
                OnBigBannerNetwork(new BigBannerMessage(desc, warnNoise), true);
                return true;
            }
            else
            {
                return BigBannerNetHook.TryBroadcastTarget(new BigBannerMessage(desc, warnNoise), targetPlayer);
            }
        }
        private static bool OnBigBannerNetwork(BigBannerMessage command, bool isServer)
        {
            UIMultiplayerHUD hudBanner = (UIMultiplayerHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.Multiplayer);
            if (command.warnNoise)
                hudBanner.Message1.SetTextWithTimeout(command.desc, UIMultiplayerHUD.Message.StateTypes.Connection, 3f, EndingBannerWarnSFX);
            else
                hudBanner.Message1.SetTextWithTimeout(command.desc, UIMultiplayerHUD.Message.StateTypes.Connection, 3f);
            return true;
        }

        private static void EndingBannerWarnSFX()
        {
            ManSFX.inst.StopMiscLoopingSFX(ManSFX.MiscSfxType.PayloadIncoming);
        }




        // ---------------------------------------  PLAYER MANAGEMENT  --------------------------------------- 
        public static bool TryGetPlayer(NetPlayer player, out MPPlayerSaveData data)
        {
            string playerIDPersist = GetPlayerID(player).ToString();
            return inst.PlayerData.TryGetValue(playerIDPersist, out data) && data != null;
        }

        /// <summary>
        /// Called when player joins on serverside.<br/>
        /// Registers and/or attaches NetPlayers to their respective MPPlayerSaveData instances.<br/>
        /// <b>SERVER ONLY</b>
        /// </summary>
        /// <param name="player">The player that just joined.</param>
        internal static void OnPlayerJoined_SERVER(NetPlayer player)
        {
            if (inst == null)
                inst = new MPKingdomsTest();
            if (TryGetPlayer(player, out var data))
            {
                DebugBeS.Log("Loading existing player " + player.name);
                data.OnPlayerJoined_SERVER(player);
            }
            else
            {
                string playerIDPersist = GetPlayerID(player).ToString();
                inst.PlayerData.Remove(playerIDPersist);
                MPPlayerSaveData playerData = new MPPlayerSaveData()
                {
                    playerID = playerIDPersist,
                    tankID = int.MinValue,
                    position = WorldPosition.FromScenePosition(Singleton.playerPos)
                };
                inst.PlayerData.Add(playerIDPersist, playerData);
                DebugBeS.Log("Added new player " + player.name);
                playerData.OnPlayerJoined_SERVER(player);
            }
        }
        /// <summary>
        /// Called when player joins on clientside.<br/>
        /// Registers and/or attaches NetPlayers to their respective MPPlayerSaveData instances.<br/>
        /// <b>CLIENT ONLY</b>
        /// </summary>
        /// <param name="player">The player that just joined.</param>
        internal static void OnPlayerJoined_CLIENT(NetPlayer player)
        {
            if (inst == null)
                inst = new MPKingdomsTest();
            if (TryGetPlayer(player, out var data))
            {
                DebugBeS.Log("Loading existing player " + player.name);
                data.OnPlayerJoined(player);
            }
            else
            {
                string playerIDPersist = GetPlayerID(player).ToString();
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
        /// <summary>
        /// Called when player leaves on serverside.<br/>
        /// Detaches NetPlayers to their respective MPPlayerSaveData instances, leaving them for saving purposes.<br/>
        /// <b>SERVER ONLY</b>
        /// </summary>
        /// <param name="player">The player that just left.</param>
        internal static void OnPlayerLeft_SERVER(NetPlayer player)
        {
            if (inst == null)
                inst = new MPKingdomsTest();
            if (TryGetPlayer(player, out var data) && data != null)
                data.OnPlayerLeft_SERVER();
        }

        /// <summary>
        /// Called when player leaves on serverside.<br/>
        /// Detaches NetPlayers to their respective MPPlayerSaveData instances, leaving them for saving purposes.<br/>
        /// <b>CLIENT ONLY</b>
        /// </summary>
        /// <param name="player">The player that just left.</param>
        internal static void OnPlayerLeft_CLIENT(NetPlayer player)
        {
            if (inst == null)
                inst = new MPKingdomsTest();
            if (TryGetPlayer(player, out var data) && data != null)
                data.OnPlayerLeft();
        }



        // ---------------------------------------  SERIALIZATION  --------------------------------------- 

        public static void PrepareForSaving()
        {
            int count = 0;
            foreach (var item in inst.PlayerData)
            {
                if (item.Value != null)
                {
                    item.Value.OnSave();
                    count++;
                }
            }
            DebugBeS.Log("Saved " + count + " players");
        }
        public static void FinishedSaving()
        {
        }
        public static void FinishedLoading()
        {
            int count = 0;
            foreach (var item in inst.PlayerData)
            {
                if (item.Value != null)
                    count++;
            }
            DebugBeS.Log("Loaded " + count + " players");
            if (inst.PlayerData.Count != count)
                DebugBeS.Warning("Loaded " + count + " players, but there were " + inst.PlayerData.Count + 
                    " players in the database.  Could they have been corrupted?");
        }




        // ---------------------------------------  PUSHBACK BARRIER AND SYNC CONTROL  --------------------------------------- 


        private static WorldPushbackBarrier GlobalPushback = null;
        public static Transform GlobalBarrierVisual = null;
        public static Transform BarrierPrefab = null;
        public static Vector3 OurPlayerPos { get; private set; }

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

        /// <summary>
        /// Emergency teleport Tech back within loaded lands
        /// </summary>
        private static void UpdateClientWorldBoarder()
        {
            Tank tank = Singleton.playerTank;
            if (tank != null && FoundOurLocalPlayerTimer > 120 && !ManNetwork.inst.MyPlayer.IsSwitchingTech)
            {
                Vector3 originDelta = (OurPlayerPos - BarrierOrigin.ScenePosition).SetY(0);
                if (originDelta.magnitude > TPDistanceDefault + 25)
                {
                    Vector3 newPos = BarrierOrigin.ScenePosition.SetY(OurPlayerPos.y) +
                        originDelta.SetY(0f).normalized * TPDistanceDefault * 0.95f;
                    UIMultiplayerHUD hudBanner = (UIMultiplayerHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.Multiplayer);
                    hudBanner.Message1.SetTextWithTimeout("You are going too fast", UIMultiplayerHUD.Message.StateTypes.Connection, 4f, EndingBannerWarnSFX);
                    //UIHelpersExt.BigF5broningBanner("You are going too fast", true);
                    ManSFX.inst.PlayMiscLoopingSFX(ManSFX.MiscSfxType.PayloadIncoming);
                    tank.visible.Teleport(newPos, tank.trans.rotation, false, false);
                }
                // This gives a WHOLE UPDATE to fix itself
                OurPlayerPos = tank.boundsCentreWorldNoCheck;
            }
        }

        /// <summary>
        /// Server-side call to move the world barrier for a specific player.<br/>
        /// <b>SERVER ONLY</b>
        /// </summary>
        /// <param name="player">The player we are moving the barrier for.</param>
        /// <param name="WP">The EXACT WorldPosition we want to move the barrier for the player.</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">ManNetwork or Player null.</exception>
        [Server]
        internal static bool TryMoveBarrier(NetPlayer player, WorldPosition WP)
        {
            // ManNetwork.inst.MyPlayer can be null when the player is first added to the world!
            if (ManNetwork.inst == null)
            {
                DebugBeS.Warning("MANNETWORK IS NULL");
                throw new NullReferenceException("MANNETWORK IS NULL");
            }
            if (ManNetwork.IsHost)
            {   // Manage Server Host - Does not need to be networked.
                if (localPlayer == null)
                {
                    // This is our first setup, so we set as normal.
                    DebugBeS.Log("First setup server barrier " + WP.TileCoord);
                    BarrierOrigin = WP;
                    DoMoveBarrier(BarrierOrigin.ScenePosition, true);
                    FoundOurLocalPlayerTimer = 0;
                    return true;
                }
                else if (localPlayer == player)
                {
                    DebugBeS.Info("Set serverside client barrier to " + WP.TileCoord);
                    KickStartBetterServers.SendChatOurClientOnly("Server barrier set " + WP.TileCoord);
                    BarrierOrigin = WP;
                    DoMoveBarrier(BarrierOrigin.ScenePosition, true);
                    FoundOurLocalPlayerTimer = 0;
                    return true;
                }
            }
            // Handle other players, and send respective repositioning for their clients.
            if (player == null)
                throw new NullReferenceException("player is null");
            if (player.connectionToServer == null)
            {
                string name = player.name.NullOrEmpty() ? "<NULL>" : player.name;
                if (localPlayer != null)
                    DebugBeS.Assert("Player " + name + " added but isn't connected to server?!  Also our server doesn't have a player yet somehow...");
                //throw new NullReferenceException("player.connectionToServer is null for player " + name);
                else
                    DebugBeS.Assert("Player " + name + " added but isn't connected to server?!");
                // Else this is our first setup, so we ignore.
            }
            return MoveBarrierNetHook.TryBroadcastTarget(new WorldPositionMessage() { m_Position = WP }, player);
        }

        /// <summary>
        /// <b>TO BE CALLED ONLY BY <c>MoveBarrierNetHook</c></b>
        /// </summary>
        private static NetworkHook<WorldPositionMessage> MoveBarrierNetHook { get; } = new NetworkHook<WorldPositionMessage>(
            "BetterServers.WorldPositionMessage", OnClientMoveBarrier, NetMessageType.ToClientsOnly);
        /// <summary>
        /// <b>TO BE CALLED ONLY BY <c>MoveBarrierNetHook</c></b>
        /// </summary>
        private static bool OnClientMoveBarrier(WorldPositionMessage command, bool isServer)
        {
            DebugBeS.Info("Set clientside barrier to " + command.m_Position.TileCoord);
            BarrierOrigin = command.m_Position;
            KickStartBetterServers.SendChatOurClientOnly("Client barrier set " + BarrierOrigin.TileCoord);
            DoMoveBarrier(BarrierOrigin.ScenePosition, FoundOurLocalPlayerTimer < FoundOurLocalPlayerTimerEnd);
            FoundOurLocalPlayerTimer = 0;
            return true;
        }

        /// <summary>
        /// <b>Called by only method <c>DoMoveBarrier</c> and field <c>MoveBarrierNetHook</c></b><br/>
        /// Moves the world barrier which prevents the player from moving outside the world<br/>
        /// Client and Server<br/>
        /// <b>SERVER</b> - Direct call this<br/>
        /// <b>CLIENT</b> - Relay through <c>MKKingdomsTest.TryBroadcastTarget()</c> instead.<br/>
        /// </summary>
        /// <param name="moveBarrierHere">Where in Scene space it should move to.</param>
        /// <param name="IMMEDEATELY">Force it to move to <c>moveBarrierHere</c> immedeately rather than take one second to get there from previous.</param>
        private static void DoMoveBarrier(Vector3 moveBarrierHere, bool IMMEDEATELY)
        {
            if (GlobalBarrierVisual == null)
            {
                DebugBeS.Exception("Failed to find GlobalBarrierVisual, things will not be able to move properly!!!!");
                return;
            }
            if (BarrierPrefab == null)
            {
                DebugBeS.Exception("Failed to find BarrierPrefab, things will not be able to move properly!!!!");
                return;
            }
            GlobalBarrierVisual.gameObject.SetActive(false);
            Vector3 endpoint = moveBarrierHere.SetY(0);
            Vector3 prevPos;
            if (IMMEDEATELY)
                prevPos = endpoint;
            else
                prevPos = ClientBarrierVisual.transform.position;
            ClientBarrierVisual.position = endpoint;
            BoundaryMesh BM = ClientBarrierVisual.GetComponent<BoundaryMesh>();
            BM.enabled = true;
            BM.Move(prevPos, endpoint, 1f);
            DebugBeS.Info("Moved clientside barrier to " + moveBarrierHere);
        }



        // ---------------------------------------  PLAYER TILELOADING  --------------------------------------- 

        internal static void ExtendAllBoundries<T>(Mode mode) where T : ModeCoOp<T>
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
        private static void STOPAllPlayersTileLoading()
        {
            for (int i = inst.PlayerData.Count - 1; i >= 0; i--)
            {
                var player = inst.PlayerData.ElementAt(i);
                if (player.Value?.tileLoader != null)
                    ManWorldTileExt.ClientUnregisterDynamicTileLoader(player.Value.tileLoader);
            }
        }
    }
}
