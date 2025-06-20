using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TerraTech.Network;
using HarmonyLib;
using Newtonsoft.Json;
using TerraTechETCUtil;
using SafeSaves;
using Snapshots;
using System.Runtime.CompilerServices;

/// <summary>
/// This mod is for TerraTech's Steam Workshop only.  
/// The code is NOT to be edited by an external source.
/// </summary>
namespace Better_Servers
{
    /// <summary>
    /// Makes a virtually impenetrable host-side blocking system that is unbypassable 
    /// within legal means.  This mod only changes host side netcode, nothing more.
    /// 
    /// This is because the Host handles all of the main physics.  
    /// That means when you play multiplayer, you are using that Host's processing power.
    /// It seems totally reasonable for the host to have more control pover their own 
    /// resources.  The Host (usually) does not like cab bombs so it should be preventable.
    /// 
    /// Basically the host refuses all requests from one who has been blocked via a
    /// Steam ID.  
    /// There isn't and will NEVER be a universal banned users list in this mod!
    /// All of that is determined by the server host on their own computer.
    /// 
    /// The Host yields ultimate control over who enters and who
    /// doesn't using a blacklist system.  Any external edits to this mod by clients will
    /// have no impact in joining a server.  
    /// Any repeated requests to join a blocking host using a modified version of this mod (or any mod for that matter) 
    /// from a client can eventually reported by the Host for a DDOS attack and Steam Support can become involved
    /// because spamming requests can overload the Steam servers, just like any other existing game.
    /// </summary>
    public class KickStartBetterServers : ModBase
    {
        public static string ModID = "Better Servers";

        private static KickStartBetterServers oInst;

        private static KeyCode BanButton = KeyCode.N;
        private static bool isInit = false;
        private static bool firstInit = false;
        internal static ServerBlockInfos blockedInfo = new ServerBlockInfos();

        internal static string DLLDirectory;
        private static string BlockDirectory;
        internal static char up = '\\';

        private static DelayedChatSequencer DCS;


        internal static JsonSerializerSettings JSONSaver = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Populate,
            MaxDepth = 30,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void EarlyInit()
        {
            if (isInit)
                return;

            TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Better Multiplayer", CompleteInit);
            isInit = true;
        }
        public override void Init()
        {
            if (isInit)
                return;

            TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Better Multiplayer", CompleteInit);
            isInit = true;
        }
        public override void DeInit()
        {
            if (!isInit)
                return;
            MainDeInit();
            isInit = false;
        }

        private void CompleteInit()
        {
            if (isInit)
                return;

            PreInit();
            MainInit();
            isInit = true;
        }

        private static Harmony harmonyInst;
        public void PreInit()
        {
            if (oInst != null)
                return;
            if (oInst == null)
                oInst = this;
            harmonyInst = new Harmony("legionite.better_servers");
            harmonyInst.PatchAll(Assembly.GetExecutingAssembly());
            InitSpecialPatch();
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX)
            {
                up = '/';
            }
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            DLLDirectory = di.Parent.ToString();
            DirectoryInfo game = new DirectoryInfo(Application.dataPath);
            BlockDirectory = game.Parent.ToString() + up + "ServerBlocklist";
            ValidateDirectory(BlockDirectory);
            DebugBeS.Log("PreInit");
        }
        private static bool SpecialPatch(ref Vector3 __result)
        {
            if (MPKingdomsTest.AreWeKingdoming)
            {
                __result = MPKingdomsTest.BarrierOrigin.GameWorldPosition;
                return false;
            }
            else
                return true;
        }
        private static bool SpecialPatch2(ref float __result)
        {
            if (MPKingdomsTest.AreWeKingdoming)
            {
                __result = MPKingdomsTest.DDistanceDefault;
                return false;
            }
            else
                return true;
        }
        public static void InitSpecialPatch()
        {
            var targetClass = typeof(UIMiniMapLayerTech).GetNestedType("MapAreaMultiplayerBoundary", BindingFlags.NonPublic);
            var targetMethod1 = targetClass.GetProperty("WorldPosition", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false);
            harmonyInst.Patch(targetMethod1, prefix: new HarmonyMethod(typeof(KickStartBetterServers).
                GetMethod("SpecialPatch", BindingFlags.NonPublic | BindingFlags.Static)));
            var targetMethod2 = targetClass.GetProperty("Radius", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false);
            harmonyInst.Patch(targetMethod2, prefix: new HarmonyMethod(typeof(KickStartBetterServers).
                GetMethod("SpecialPatch2", BindingFlags.NonPublic | BindingFlags.Static)));
        }
        public static void DeInitSpecialPatch()
        {
            var targetClass = typeof(UIMiniMapLayerTech).GetNestedType("MapAreaMultiplayerBoundary", BindingFlags.NonPublic);
            var targetMethod1 = targetClass.GetProperty("WorldPosition", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false);
            harmonyInst.Unpatch(targetMethod1, HarmonyPatchType.Prefix, harmonyInst.Id);
            var targetMethod2 = targetClass.GetProperty("Radius", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false);
            harmonyInst.Unpatch(targetMethod2, HarmonyPatchType.Prefix, harmonyInst.Id);
        }

        private static ManToolbar.ToolbarToggle BanListToggle;
        private void ModeSwitchEvent(Mode mode)
        {
            MPKingdomsTest.OnSwitchMode(mode);
        }
        private void ModeEndEvent(Mode mode)
        {
            MPKingdomsTest.OnModeEndEvent(mode);
        }
        public static void OnSaveManagers(bool Doing)
        {
            if (Doing)
            {
                MPKingdomsTest.PrepareForSaving();
            }
            else
            {
                MPKingdomsTest.FinishedSaving();
            }
        }
        public static void OnLoadManagers(bool Doing)
        {
            if (Doing)
            {
            }
            else
            {
                MPKingdomsTest.FinishedLoading();
            }
        }
        private void MainInitENCAPSULATEInitExtras()
        {
            try
            {
                KickStartExtras.InitExtras();
            }
            catch { }
        }
        private void MainInit()
        {
            GetBlockLog();
           //ManPointer.inst.MouseEvent.Subscribe(OnTechSelect);
            ManGameMode.inst.ModeStartEvent.Subscribe(ModeSwitchEvent);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(ModeEndEvent);
            
            if (GUIWindow == null)
            {
                Initiate();
                try
                {
                    MainInitENCAPSULATEInitExtras();
                }
                catch { }
            }
            ManWorldTileExt.InsureInit();
            BlockIndexer.ConstructBlockLookupListDelayed();
            DeathmatchExt.SetReady();
            ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly(), OnSaveManagers, OnLoadManagers);
            MPKingdomsTest.InitHooks();

            MassPatcher.MassPatchAllWithin(harmonyInst, typeof(MPKingdomsPatches), ModID);
        }
        private void MainDeInit()
        {
            DeInitSpecialPatch();
            MassPatcher.MassUnPatchAllWithin(harmonyInst, typeof(MPKingdomsPatches), ModID);
            ManSafeSaves.UnregisterSaveSystem(Assembly.GetExecutingAssembly(), OnSaveManagers, OnLoadManagers);
            ManGameMode.inst.ModeFinishedEvent.Unsubscribe(ModeEndEvent);
            ManGameMode.inst.ModeStartEvent.Unsubscribe(ModeSwitchEvent);
            //ManPointer.inst.MouseEvent.Unsubscribe(OnTechSelect);
            blockedInfo = new ServerBlockInfos();
            DeathmatchExt.SetUnReady();
            BlockIndexer.ResetBlockLookupList();
        }

        // OBSOLETE - Base game now has ban function!
        /*
        private void OnTechSelect(ManPointer.Event click, bool down, bool yes)
        {
            if (!ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked && click == ManPointer.Event.RMB && down && Input.GetKey(BanButton))
            {
                if (ManPointer.inst.targetTank)
                {
                    //if (ManPointer.inst.targetTank != Singleton.playerTank)
                        ToggleBlockFromTech(ManPointer.inst.targetTank);
                }
            }
        }
        */

        [Client]
        internal static void ProtectedServerDelay()
        {
            try
            {
                DCS.Invoke("ProtectedServer", 7);
            }
            catch { }
        }
        [Client]
        internal static void ProtectedServer()
        {
            try
            {
                ChatLog("This server is protected against griefers with an infraction system.");
                if (!DCS)
                    DCS = new GameObject().AddComponent<DelayedChatSequencer>();
                DCS.Invoke("ProtectedServer2", 3);
            }
            catch { }
        }

        // OBSOLETE - Base game now has ban function!
        /*
        /// <summary>
        /// Local Host only
        /// </summary>
        private void ToggleBlockFromTech(Tank playerTech)
        {
            if (playerTech)
            {
                if (playerTech.netTech)
                {
                    if (playerTech.netTech.NetPlayer)
                    {
                        ToggleBlockPlayer(playerTech.netTech.NetPlayer);
                    }
                    else
                        ChatLog("Could not block: NetTech.NetPlayer is null!");
                }
                else
                    ChatLog("Could not block: NetTech is null!");
            }
            else
                ChatLog("Could not block: TECH IS NULL!");
        }
        /// <summary>
        /// Local Host only
        /// </summary>
        private void ToggleBlockPlayer(NetPlayer player)
        {
            if (blockedInfo.bannedUsersHost.TryGetValue(player.GetPlayerIDInLobby().m_NetworkID, out _))
            {
                ChatLog("UNBLOCKING USER " + player.name);
                if (blockedInfo.bannedUsersHost.Remove(player.GetPlayerIDInLobby().m_NetworkID))
                {
                    SetBlockLog();
                    ChatLog("Unblocked successfully.");
                }
                else
                    ChatLog("user " + player.name + " is already unblocked?  CONTACT LEGIONITE");
            }
            else
            {
                if (ManNetwork.inst.MyPlayer == player)
                {
                    ChatLog("ERROR - Cannot block server host");
                }
                else
                {
                    ChatLog("BLOCKING USER " + player.name);
                    blockedInfo.bannedUsersHost.Add(player.GetPlayerIDInLobby().m_NetworkID, player.name);
                    SetBlockLog();
                    if (ManNetwork.IsHost && blockedInfo.KickOnBlock)
                    {
                        ForceKickPlayer(player);
                        ChatLog("Kicked & Blocked successfully.");
                    }
                    else
                        ChatLog("Blocked successfully.");
                }
            }
        }
        internal static void ForceKickPlayer(NetPlayer player)
        {
            player.ServerKickPlayer();
            if (player != null && player.IsPlayerActive)
                ManNetwork.inst.RemovePlayer(player);
            ManNetwork.inst.KickedTidyUp();
        }
        */

        /// <summary>
        /// Local Host only
        /// </summary>
        [Server]
        private void GetBlockLog()
        {
            string destination = BlockDirectory + up + "blockList.json";
            ValidateDirectory(BlockDirectory);
            if (File.Exists(destination))
            {
                try
                {
                    string output = "";
                    output = File.ReadAllText(destination);

                    blockedInfo = JsonConvert.DeserializeObject<ServerBlockInfos>(output);
                    DebugBeS.Log("Loaded blockList.json successfully.");

                }
                catch (Exception e)
                {
                    DebugBeS.LogError("Could not load contents of blockList.json!");
                    DebugBeS.Log(e);
                    return;
                }
                return;
            }
            else
            {
                try
                {
                    File.WriteAllText(destination, JsonConvert.SerializeObject(blockedInfo, Formatting.Indented, JSONSaver));
                    DebugBeS.Log("Edited blockList.json successfully.");
                    return;
                }
                catch
                {
                    DebugBeS.Log("Could not read blockList.json\n   This could be due to a bug with this mod or file permissions.");
                    return;
                }
            }
        }

        /// <summary>
        /// Local Host only
        /// </summary>
        [Server]
        private void SetBlockLog()
        {
            DebugBeS.Log("Setting up template reference...");
            string destination = BlockDirectory + up + "blockList.json";
            ValidateDirectory(BlockDirectory);
            try
            {
                File.WriteAllText(destination, JsonConvert.SerializeObject(blockedInfo, Formatting.Indented, JSONSaver));
                DebugBeS.Log("Saved blockList.json successfully.");
            }
            catch
            {
                DebugBeS.LogError("Could not save blockList.json.\n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }

        // INFRACTIONS
        [Server]
        internal static bool IsHost(NetPlayer offendingPlayer)
        {
            if (!ManNetwork.IsHost)
                throw new InvalidOperationException("Called IsHost() on non-host");
            return ManNetwork.inst.MyPlayer == offendingPlayer;
        }
        [Server]
        internal static string SendChatServer(string chatMsg)
        {
            try
            {
                if (ManNetwork.IsHost)
                    Singleton.Manager<ManNetworkLobby>.inst.LobbySystem.CurrentLobby.SendChat("[SERVER] " + chatMsg, -1, ManNetwork.inst.MyPlayer.netId.Value);
                else
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("[CLIENT] " + chatMsg);
                    DebugBeS.Assert("Called SendChatServer() on non-host, only client will see this!!!");
                }
            }
            catch { }
            return chatMsg;
        }
        [Server]
        internal static string Infraction(NetPlayer offendingPlayer, string infraction, string desc)
        {
            if (!ManNetwork.IsHost)
                throw new InvalidOperationException("Called Infraction() on non-host");
            string infract = "INFRACTION: " + offendingPlayer.name + " has violated " + infraction + " for the following: " + desc;
            try
            {
                return SendChatServer(infract);
            }
            catch { }
            return infract;
        }
        [Server]
        internal static void HostException()
        {
            if (blockedInfo.IWannaBeUnjust)
                return;
            try
            {
                SendChatServer("The server host has violated their own rules!  Leave the server if this happens too frequently!");
            }
            catch { }
        }
        [Server]
        internal static void ChatLog(string toLog)
        {
            try
            {
                SendChatServer(toLog);
            }
            catch { }
            DebugBeS.Log(toLog);
        }
        [Server]
        internal static void ChatLogKaren(string toLog)
        {
            if (blockedInfo.ShutUpKaren)
                return;
            try
            {
                SendChatServer(toLog);
            }
            catch { }
            DebugBeS.Log(toLog);
        }


        private static FieldInfo data = typeof(NetTech).GetField("m_TechData", BindingFlags.NonPublic | BindingFlags.Instance);
        /// <summary>
        /// Returns a value greater than zero if a rule was violated AND action was taken automatically.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="offendingPlayer"></param>
        /// <returns></returns>
        [Server]
        internal static int OnTechSpawn(NetTech tech, NetPlayer offendingPlayer)
        {
            if (!ManNetwork.IsHost)
                return 0;
            DebugBeS.Log("OnTechSpawn");
            int count = ManTechs.inst.IteratePlayerTechs().Count();
            if (count > blockedInfo.maxTechLimit)
            {
                List<Tank> toCull = new List<Tank>();
                foreach (Tank tech2 in ManTechs.inst.IteratePlayerTechs())
                {
                    if (tech2?.netTech?.NetPlayer?.CurTech)
                    {
                        if (tech2.netTech.NetPlayer.CurTech != tech2)
                        {
                            toCull.Add(tech2);
                            count--;
                            if (count <= blockedInfo.maxTechLimit)
                                break;
                        }
                    }
                }
                count = toCull.Count();
                if (count > 0)
                {
                    ChatLog("Removing " + count + " techs over the Max Tech Limit...");
                    for (int step = 0; step < count; count--)
                    {
                        if (ManNetwork.inst.InventoryAvailable)
                        {
                            toCull[0].netTech.RequestRemoveFromGame(ManNetwork.inst.MyPlayer, false);
                        }
                        else
                            toCull[0].netTech.RequestRemoveFromGame(null, false);
                    }
                }
                else
                    ChatLogKaren("Host, the maxTechLimit is too low for this lobby size at " + blockedInfo.maxTechLimit + ", please raise it.");
            }

            TechData techData = (TechData)data.GetValue(tech);
            count = techData.m_BlockSpecs.Count;
            if (count > blockedInfo.maxBlockLimit)
            {
                Infraction(offendingPlayer, "Block Restriction", "Tech had " + count + " blocks over the limit of " + blockedInfo.maxBlockLimit);
                if (IsHost(offendingPlayer))
                {
                    HostException();
                    return 0;
                }
                PurgeTechData(techData);
                data.SetValue(tech, techData);
                return blockedInfo.BlockLimitInfractionSeverity;
            }

            int bolts = 0;
            int cabs = 0;
            foreach (var item in techData.m_BlockSpecs)
            {
                TankBlock TB = ManSpawn.inst.GetBlockPrefab(item.m_BlockType);
                if (!TB)
                    continue;
                if (TB.GetComponent<ModuleDetachableLink>())
                    bolts++;
                if (TB.GetComponent<ModuleTechController>())
                    cabs++;
            }

            if (bolts > blockedInfo.maxBoltLimit)
            {
                Infraction(offendingPlayer, "Bolts Rule", "Tech had " + bolts + " explosive bolts over the limit of " + blockedInfo.maxBoltLimit);
                if (IsHost(offendingPlayer))
                {
                    HostException();
                    return 0;
                }
                PurgeTechData(techData);
                data.SetValue(tech, techData);
                return blockedInfo.BoltInfractionSeverity;
            }
            if (cabs > blockedInfo.maxCabLimit)
            {
                Infraction(offendingPlayer, "Cab Spam Rule", "Tech had " + cabs + " cabs, which is over the limit of " + blockedInfo.maxCabLimit); 
                if (IsHost(offendingPlayer))
                {
                    HostException();
                    return 0;
                }
                PurgeTechData(techData);
                data.SetValue(tech, techData);
                return blockedInfo.CabInfractionSeverity;
            }
            return 0;
        }
        // Leaves only one block behind on the purged tech
        [Server]
        private static void PurgeTechData(TechData tech)
        {
            if (tech.m_BlockSpecs.Count > 0)
            {
                TankPreset.BlockSpec BS = tech.m_BlockSpecs.First();
                tech.m_BlockSpecs.Clear();
                tech.m_BlockSpecs.Add(BS);
                tech.m_TechSaveState.Clear();
                tech.m_SkinMapping.Clear();
                tech.m_Bounds = IntVector3.one;
            }
        }


        /// <summary>
        /// Removes Techs from the lobby on player exit.  Try prevent theft of Techs.
        /// </summary>
        /// <param name="player"></param>
        [Server]
        internal static void OnPlayerDisconnect(NetPlayer player)
        {
            if (!ManNetwork.IsHost)
                return;
            if (!IsHost(player) && player?.CurTech?.tech?.visible)
            {
                if (ManGameMode.inst.IsCurrent<ModeCoOpCampaign>())
                {
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(player.CurTech.HostID);
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
                    {
                        m_HostID = TV.HostID,
                        m_CheatBypassInventory = false,
                    }
                    );
                    ChatLog("Returned a tech to inventory from a departing player to save resources.");
                }
                else
                {
                    player.CurTech.RequestRemoveFromGame(player, false);
                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(player.CurTech.HostID);
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
                    {
                        m_HostID = TV.HostID,
                        m_CheatBypassInventory = true,
                    }
                    );
                    ChatLog("Cleared a tech from a departing player to save resources.");
                }
            }
        }




        private static bool GetName(string FolderDirectory, out string output, bool doJSON = false)
        {
            if (FolderDirectory == null)
                throw new NullReferenceException("FolderDirectory is NULL");
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == up)
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }
            if (doJSON)
            {
                if (!final.ToString().Contains(".json"))
                {
                    output = "error";
                    return false;
                }
                final.Remove(final.Length - 5, 5);// remove ".json"
            }
            output = final.ToString();
            //Debug.Log("Cleaning Name " + output);
            return true;
        }
        private static void ValidateDirectory(string DirectoryIn)
        {
            if (!GetName(DirectoryIn, out string name))
                return;// error
            if (!Directory.Exists(DirectoryIn))
            {
                DebugBeS.Log("Generating " + name + " folder.");
                try
                {
                    Directory.CreateDirectory(DirectoryIn);
                    DebugBeS.Log("Made new " + name + " folder successfully.");
                }
                catch
                {
                    DebugBeS.LogError("Could not create new " + name + " folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }
            }
        }

        internal class DelayedChatSequencer : MonoBehaviour
        {
            internal void ProtectedServer2()
            {
                try
                {
                    ChatLog("If you violate the host's rules, the server host can ban you from their servers forever.");
                    DCS.Invoke("ProtectedServer3", 3);
                }
                catch { }
            }
            internal void ProtectedServer3()
            {
                try
                {
                    if (blockedInfo.IWannaBeUnjust)
                    {
                        ChatLog("The host of this server has admitted they do not abide by their own rules.  Play at your own risk.");
                        DCS.Invoke("ProtectedServer4", 3);
                    }
                    else
                    {
                        ChatLog("Max Blocks per Tech: " + blockedInfo.maxBlockLimit
                            + " | Max Cabs per Tech: " + blockedInfo.maxCabLimit
                            + " | Max Bolts per Tech: " + blockedInfo.maxBoltLimit
                            + " | Max World tech limit: " + blockedInfo.maxTechLimit);
                        DCS.Invoke("ProtectedServer5", 3);
                    }
                }
                catch { }
            }
            internal void ProtectedServer4()
            {
                try
                {
                    ChatLog("Max Blocks per Tech: " + blockedInfo.maxBlockLimit
                    + " | Max Cabs per Tech: " + blockedInfo.maxCabLimit
                    + " | Max Bolts per Tech: " + blockedInfo.maxBoltLimit
                    + " | Max World tech limit: " + blockedInfo.maxTechLimit);
                    DCS.Invoke("ProtectedServer5", 3);
                }
                catch { }
            }
            internal void ProtectedServer5()
            {
                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("[LOCAL] Kick with Left-Click + [N], Open block menu with [ALT] + [N]");
                }
                catch { }
            }

            private void Update()
            {
                if (!ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked && ManNetwork.IsNetworked
                    && Input.GetKeyDown(BanButton) && (Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftAlt)))
                {
                    openTime = 4;
                    MoveMenuToCursor(true);
                    GUIWindow.SetActive(true);
                }
            }
        }



        // THE JUMP MANAGER

        public static void Initiate()
        {
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplayBlockList>();
            GUIWindow.SetActive(false);
        }

        public static bool IsIngame { get { return !ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked; } }

        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 400, 230);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;
        private const int GUIBlockerID = 9780;
        internal class GUIDisplayBlockList : MonoBehaviour
        {
            private void OnGUI()
            {
                if (IsIngame)
                {
                    HotWindow = AltUI.Window(GUIBlockerID, HotWindow, GUIHandler, "<b>Unblock User Menu</b>", CloseGUI);
                }
                else
                    gameObject.SetActive(false);
            }
        }

        private static float openTime = 0;
        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private const int ButtonWidth = 300;
        private const int MaxWindowHeight = 500;
        private static int MaxWindowWidth = ButtonWidth;
        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionY = false;
            int index = 0;
            string playerSelected = string.Empty;

            scrolll = GUILayout.BeginScrollView(scrolll);
            if (blockedInfo == null || blockedInfo.userInfractionsHost.Count == 0)
            {
                if (GUILayout.Button("No Players Banned"))
                {
                    CloseGUI();
                }
            }
            else
            {
                int Entries = blockedInfo.userInfractionsHost.Count();
                for (int step = 0; step < Entries; step++)
                {
                    try
                    {
                        KeyValuePair<string, PlayerHistory> temp = blockedInfo.userInfractionsHost.ElementAt(step);
                            HoriPosOff = 0;
                            VertPosOff += 30;
                            if (VertPosOff >= MaxWindowHeight)
                                MaxExtensionY = true;
                        try
                        {
                            string disp = "<color=#90ee90ff>" + temp.Value.ToString() + "</color>";

                            if (GUILayout.Button(disp))
                            {
                                index = step;
                                clicked = true;
                            }
                            HoriPosOff += ButtonWidth;
                        }
                        catch { }
                    }
                    catch { }// error on handling something
                }

                GUILayout.EndScrollView();
                scrolllSize = VertPosOff + 50;

                if (MaxExtensionY)
                    HotWindow.height = MaxWindowHeight + 80;
                else
                    HotWindow.height = VertPosOff + 80;

                HotWindow.width = MaxWindowWidth + 60;
                if (clicked)
                {
                    blockedInfo.userInfractionsHost.Remove(playerSelected);
                    oInst.SetBlockLog();
                    CloseGUI();
                }

                GUI.DragWindow();
                if (openTime <= 0 && !MouseIsOverSubMenu())
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Close);
                    CloseGUI();
                }
            }
            openTime -= Time.deltaTime / 2;
        }
        private static void CloseGUI()
        {
            ReleaseControl();
            GUIWindow.SetActive(false);
        }
        public static void MoveMenuToCursor(bool centerOnMouse)
        {
            if (centerOnMouse)
            {
                Vector3 Mous = Input.mousePosition;
                xMenu = Mous.x - (HotWindow.width / 2);
                yMenu = Display.main.renderingHeight - Mous.y - 90;
            }
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
        }
        public static bool MouseIsOverSubMenu()
        {
            if (GUIWindow.activeSelf)
            {
                Vector3 Mous = Input.mousePosition;
                Mous.y = Display.main.renderingHeight - Mous.y;
                float xMenuMin = HotWindow.x;
                float xMenuMax = HotWindow.x + HotWindow.width;
                float yMenuMin = HotWindow.y;
                float yMenuMax = HotWindow.y + HotWindow.height;
                //Debug.Log(Mous + " | " + xMenuMin + " | " + xMenuMax + " | " + yMenuMin + " | " + yMenuMax);
                if (Mous.x > xMenuMin && Mous.x < xMenuMax && Mous.y > yMenuMin && Mous.y < yMenuMax)
                {
                    return true;
                }
            }
            return false;
        }
        public static void ReleaseControl(string Name = null)
        {
            string focused = GUI.GetNameOfFocusedControl();
            if (Name == null)
            {
                GUI.FocusControl(null);
                GUI.UnfocusWindow();
                GUIUtility.hotControl = 0;
            }
            else
            {
                if (focused == Name)
                {
                    GUI.FocusControl(null);
                    GUI.UnfocusWindow();
                    GUIUtility.hotControl = 0;
                }
            }
        }

    }
}
