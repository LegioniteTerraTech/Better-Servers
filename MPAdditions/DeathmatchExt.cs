using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.IO;
using TerraTechETCUtil;
using Newtonsoft.Json;

namespace Better_Servers
{
    public class DeathmatchExt : MonoBehaviour
    {
        private static DeathmatchExt inst;

        private static FieldInfo techData = typeof(TankPreset)
                   .GetField("m_TechData", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance),
            assetName = typeof(MultiplayerTechSelectPresetAsset)
                   .GetField("m_TankName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance),
            techList = typeof(UIScreenMultiplayerTechSelect)
                   .GetField("m_CorporationPresets", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance),
            corpList = typeof(UIScreenMultiplayerTechSelect)
                   .GetField("m_CorpItems", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance),
            blockList = typeof(InventoryAsset)
                   .GetField("m_BlockList", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        private static int page = 0;

        private static List<FactionSubTypes> pageDefaultCorps;
        private static List<MultiplayerTechSelectPresetAsset> pageDefault;
        private static List<string> pageDefaultNames = null;
        /*
        new List<string> {
        "Recon",
        "Soldier",
        "Tank",
        "Charger",
        "Assassin",
        "Artillery",
        "Assault",
        "Sniper",
    };*/
        private static List<FactionSubTypes> pageVanillaExtCorps;
        private static List<MultiplayerTechSelectPresetAsset> pageVanillaExt;
        private static List<string> pageVanillaExtNames;
        private static List<List<MultiplayerTechSelectPresetAsset>> MorePages = null;
        private static List<List<FactionSubTypes>> MorePagesCorps = null;
        private static List<List<string>> MorePagesNames = null;

        private static List<BlockCount> BlockListSet = null;

        public static HashSet<BlockTypes> PermittedBlocksDefault = null;
        public static HashSet<BlockTypes> PermittedBlocksCustom = null;
        public enum CrateWeaponRestriction
        { 
            Default,
            MeleeOnly,
            RangedOnly,
        }
        public enum CrateMovementRestriction
        {
            Default,
            WheelsOnly,
            NoHovers,
            HoversOnly,
        }

        private static InventoryBlockList defaultList;

        public static bool Ready = false;
        private static bool AllowedSetModdedBlocks = false;
        public static void OnModeSwitch(Mode mode)
        {
            if (mode is ModeDeathmatch MD && !AllowedSetModdedBlocks)
            {
                DoReady();
            }
        }
        public static void SetReady()
        {
            if (inst == null)
                inst = Instantiate(new GameObject("DeathMatchExt")).AddComponent<DeathmatchExt>();
            ManGameMode.inst.ModeStartEvent.Subscribe(OnModeSwitch);
        }
        private static void AddFallbackOneStep()
        {
            pageVanillaExtCorps.Add(FactionSubTypes.GSO);
            pageVanillaExt.Add(pageDefault[0]);
            pageVanillaExtNames.Add(pageDefaultNames[0]);
            pageVanillaExtNames.Add(pageDefaultNames[1]);
            /*
            pageVanillaExtCorps.Add(FactionSubTypes.GSO);
            pageVanillaExt.Add(MakeNewDeathmatchTechLegacy("Cab Spam", null, out _));
            pageVanillaExtNames.Add("Cabs");
            pageVanillaExtNames.Add("Cabs");
            */
        }
        public static void SetupRestrictions(CrateWeaponRestriction weapons, CrateMovementRestriction move)
        {
            BlockListSet.Clear();
            foreach (var item in PermittedBlocksDefault)
            {
                BlockDetails BD = BlockIndexer.GetBlockDetails(item);
                bool valid = true;
                if (BD.IsWeapon)
                {
                    switch (weapons)
                    {
                        case CrateWeaponRestriction.MeleeOnly:
                            if (!BD.IsMelee)
                                valid = false;
                            break;
                        case CrateWeaponRestriction.RangedOnly:
                            if (BD.IsMelee)
                                valid = false;
                            break;
                        default:
                            break;
                    }
                }
                if (BD.DoesMovement)
                {
                    switch (move)
                    {
                        case CrateMovementRestriction.WheelsOnly:
                            if (!BD.HasWheels)
                                valid = false;
                            break;
                        case CrateMovementRestriction.NoHovers:
                            if (BD.HasHovers)
                                valid = false;
                            break;
                        case CrateMovementRestriction.HoversOnly:
                            if (!BD.HasHovers)
                                valid = false;
                            break;
                        default:
                            break;
                    }
                }
                if (valid)
                    BlockListSet.Add(ConvertToBlockCount(item));
            }
        }
        public static void DoReady()
        {
            if (!Ready)
            {
                try
                {
                    //UIScreenNetworkLobby UISNL = (UIScreenNetworkLobby)ManUI.inst.GetScreen(ManUI.ScreenType.MatchmakingLobbyScreen);
                    MorePages = new List<List<MultiplayerTechSelectPresetAsset>>();
                    MorePagesCorps = new List<List<FactionSubTypes>>();
                    MorePagesNames = new List<List<string>>();
                    PermittedBlocksDefault = new HashSet<BlockTypes>();
                    PermittedBlocksCustom = new HashSet<BlockTypes>();
                    BlockIndexer.ConstructBlockLookupList();
                    List<BlockCount> BCadd = new List<BlockCount>();
                    DebugBeS.Log("DeathmatchExt - Adding in techs...");
                    if (pageVanillaExt == null)
                    {
                        pageVanillaExt = new List<MultiplayerTechSelectPresetAsset>();
                        pageVanillaExtCorps = new List<FactionSubTypes>();
                        pageVanillaExtNames = new List<string>();
                        if (MakeNewDeathmatchTech(MPNewStarters.BFStart, out MultiplayerTechSelectPresetAsset MT, out _, out List<string> names))
                        {
                            pageVanillaExtCorps.Add(FactionSubTypes.BF);
                            pageVanillaExt.Add(MT);
                            pageVanillaExtNames.AddRange(names);
                            DebugBeS.Log("Made BFStart");
                        }
                        else
                            AddFallbackOneStep();

                        if (MakeNewDeathmatchTech(MPNewStarters.RRStart, out MultiplayerTechSelectPresetAsset MT2, out _, out List<string> names2))
                        {
                            pageVanillaExtCorps.Add(FactionSubTypes.EXP);
                            pageVanillaExt.Add(MT2);
                            pageVanillaExtNames.AddRange(names2);
                            DebugBeS.Log("Made RRStart");
                        }
                        else
                            AddFallbackOneStep();

                        if (MakeNewDeathmatchTech(MPNewStarters.SJStart, out MultiplayerTechSelectPresetAsset MT3, out _, out List<string> names3))
                        {
                            pageVanillaExtCorps.Add(FactionSubTypes.SJ);
                            pageVanillaExt.Add(MT3);
                            pageVanillaExtNames.AddRange(names3);
                            DebugBeS.Log("Made SJStart");
                        }
                        else
                            AddFallbackOneStep();
                        /*
                        if (MakeNewDeathmatchTech(MPNewStarters.TACAssault, out MultiplayerTechSelectPresetAsset MT3, out _, out List<BlockCount> blc))
                        {
                            pageVanillaExtCorps.Add(ManMods.inst.GetCorpIndex("TAC"));
                            pageVanillaExt.Add(MT3);
                            BCadd.AddRange(blc);
                            Debug.Log("Made TACAssault");
                        }
                        else //*/
                            AddFallbackOneStep();

                        FactionSubTypes FST = ManMods.inst.GetCorpIndex("TAC");
                        if (FST < FactionSubTypes.GSO)
                            FST = FactionSubTypes.GSO;
                        defaultList = (InventoryBlockList)blockList.GetValue(ManNetwork.inst.CrateDropWhiteList);
                    }
                    BlockListSet = new List<BlockCount>(defaultList.Blocks);
                    BlockListSet.AddRange(BCadd);
                    BlockListSet.AddRange(GetMoreAllowedBlocks(AddedBlocksList()));
                    if (!MorePages.Contains(pageVanillaExt))
                        MorePages.Add(pageVanillaExt);
                    if (!MorePagesCorps.Contains(pageVanillaExtCorps))
                        MorePagesCorps.Add(pageVanillaExtCorps);
                    if (!MorePagesNames.Contains(pageVanillaExtNames))
                        MorePagesNames.Add(pageVanillaExtNames);
                    BlockListSet.AddRange(MakeCustomTechStarters());
                    foreach (var item in BlockListSet.Select(x => x.m_BlockType).Distinct())
                    {
                        PermittedBlocksDefault.Add(item);
                    }
                    InventoryBlockList IBL2 = new InventoryBlockList(BlockListSet);
                    blockList.SetValue(ManNetwork.inst.CrateDropWhiteList, IBL2);
                }
                catch (Exception e)
                { DebugBeS.Log("Error on Load DeathmatchExt: " + e); }
                Ready = true;
            }
        }
        public static void SetUnReady()
        {
            try
            {
                ManGameMode.inst.ModeStartEvent.Unsubscribe(OnModeSwitch);
                MorePages = null;
                MorePagesCorps = null;
                MorePagesNames = null;
                PermittedBlocksDefault = null;
            }
            catch (Exception e)
            { DebugBeS.Log("Error on Unload DeathmatchExt: " + e); }
            Ready = false;
        }

        public static List<BlockCount> MakeCustomTechStarters()
        {
            List<BlockCount> BC = new List<BlockCount>();
            try
            {
                List<StarterTech> starters = GetCustomTechStarters();

                List<FactionSubTypes> pageCorps = new List<FactionSubTypes>();
                List<MultiplayerTechSelectPresetAsset> page = new List<MultiplayerTechSelectPresetAsset>();
                List<string> pageNames = new List<string>();
                foreach (var item in starters)
                {
                    if (MakeNewDeathmatchTech(item, out MultiplayerTechSelectPresetAsset MT, out FactionSubTypes FST, out List<BlockCount> blc, out List<string> nameBatch))
                    {
                        DebugBeS.Log("New Tech " + item.Name);
                        pageCorps.Add(FST);
                        page.Add(MT);
                        BC.AddRange(blc);
                        pageNames.AddRange(nameBatch);
                    }

                    if (page.Count == 4)
                    {
                        MorePagesCorps.Add(pageCorps);
                        MorePages.Add(page);
                        MorePagesNames.Add(pageNames);
                        pageCorps = new List<FactionSubTypes>();
                        page = new List<MultiplayerTechSelectPresetAsset>();
                        pageNames = new List<string>();
                    }
                }
                if (page.Count > 0)
                {
                    for ( ; page.Count < 4; )
                    {
                        DebugBeS.Log("Filler Tech " + pageVanillaExtCorps[0]);
                        page.Add(pageVanillaExt[0]);
                        pageCorps.Add(pageVanillaExtCorps[0]);
                        pageNames.Add(pageVanillaExtNames[0]);
                        pageNames.Add(pageVanillaExtNames[1]);
                    }
                    MorePagesCorps.Add(pageCorps);
                    MorePages.Add(page);
                    MorePagesNames.Add(pageNames);
                }
            }
            catch (Exception e)
            { DebugBeS.Log("Error on MakeCustomTechStarters DeathmatchExt: " + e); }
            Ready = false;
            return BC;
        }
        public static List<StarterTech> GetCustomTechStarters()
        {
            try
            {
                List<StarterTech> toAdd = new List<StarterTech>();
                string location = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.ToString();// Go to the cluster directory

                foreach (string directoryLoc in Directory.GetDirectories(location))
                {
                    try
                    {
                        string fileName = "StarterTech.json";
                        string GO = directoryLoc + "/" + fileName;
                        if (File.Exists(GO))
                        {
                            StarterTech ext = JsonConvert.DeserializeObject<StarterTech>(File.ReadAllText(GO));
                            toAdd.Add(ext);
                            DebugBeS.Log("GetCustomTechStarters - Registered " + ext.Name);
                        }
                    }
                    catch (Exception e) { DebugBeS.Log("GetCustomTechStarters - ERROR " + e); }
                }
                if (toAdd.Count > 0)
                    DebugBeS.Log("GetCustomTechStarters - Added " + toAdd.Count + " techs from mods");
                else
                    DebugBeS.Log("GetCustomTechStarters - No starter techs found");
                return toAdd;
            }
            catch (Exception e)
            { DebugBeS.Log("DeathmatchExt - Error in GetCustomTechStarters: " + e); }
            return new List<StarterTech>();
        }

        public static List<BlockTypes> AddedBlocksList()
        {
            return new List<BlockTypes>
            {
                // BF
                BlockTypes.BF_Cab_212,
                // main
                BlockTypes.BF_Block_111,
                BlockTypes.BF_Block_112,
                BlockTypes.BF_Block_Faired_111,
                BlockTypes.BF_Block_Smooth_111,
                BlockTypes.BF_Block_Smooth_Corner_111,
                BlockTypes.BF_Block_Smooth_Edge_111,
                BlockTypes.BF_Streamline_111,
                BlockTypes.BF_Streamline_112,
                // movement
                BlockTypes.BF_Wheel_Small_Angled_122,
                BlockTypes.BF_Hover_Flipper_Small_Left_212,
                BlockTypes.BF_Hover_Flipper_Small_Right_212,
                BlockTypes.BF_Hover_Ring_Small_212,
                BlockTypes.BF_Rotor_Small_313,
                BlockTypes.BF_Wing_Small_212,
                BlockTypes.BF_Wing_Medium_Middle_413,
                BlockTypes.BF_Wing_Medium_Outside_413,
                BlockTypes.BF_Wing_Tail_532,
                // weapons
                BlockTypes.BF_Laser_Streamlined_111,
                BlockTypes.BF_Laser_Streamlined_112,
                BlockTypes.BF_Laser_112,
                BlockTypes.BF_Laser_Scatter_121,
                BlockTypes.BF_FlamethrowerPlasma_112,
                BlockTypes.BF_Laser_Gatling_423,
                // etc
                BlockTypes.BF_Radar_111,
                BlockTypes.BF_FuelTank_111,
                BlockTypes.BF_FuelTank_112,
                BlockTypes.BF_Wing_Small_212,
                BlockTypes.BF_Booster_111,
                BlockTypes.BF_Booster_112,

                // RR
                BlockTypes.EXP_Cab_112,
                // main
                BlockTypes.EXP_Block_111,
                BlockTypes.EXP_Block_212,
                BlockTypes.EXP_Block_Armoured_111,
                BlockTypes.EXP_Block_Armoured_112,
                BlockTypes.EXP_Block_Faired_111,
                BlockTypes.EXP_Block_Faired_211,
                // movement
                BlockTypes.EXP_Wheel_Side_222,
                BlockTypes.EXP_Wheel_Straight_222,
                BlockTypes.EXP_Wheel_Omni_122,
                // weapons
                BlockTypes.EXP_LaserGun_323,
                BlockTypes.EXP_StickyBomb_223,
                BlockTypes.EXP_VibroKnife_112,
                BlockTypes.EXP_Flares_121,
                BlockTypes.EXP_PrototypeGun_02_626,
                // etc
                BlockTypes.EXP_Chassis_425,
                BlockTypes.EXP_Chassis_526,
                BlockTypes.EXP_Chassis_Angled_332,
                BlockTypes.EXP_Chassis_Cross_332,
                
                // SJ
                BlockTypes.SJ_Cab_122,
                // main
                BlockTypes.SJ_Block_111,
                BlockTypes.SJ_Block_112,
                BlockTypes.SJ_Armour_Bullbar_111,
                BlockTypes.SJ_Armour_Bullbar_211,
                BlockTypes.SJ_Armour_Cab_133,
                BlockTypes.SJ_Armour_Bullbar_Corner_211,
                BlockTypes.SJ_Girder_Angled_233,
                BlockTypes.SJ_Girder_Offset_233,
                // movement
                BlockTypes.SJ_Wheel_212,
                BlockTypes.SJ_Wheel_222,
                BlockTypes.SJ_Wheel_322,
                BlockTypes.SJ_ScrewTracks_123,
                BlockTypes.SJ_Tracks_226,
                BlockTypes.SJ_Hover_322,
                BlockTypes.SJ_Wing_Left_613,
                BlockTypes.SJ_Wing_Right_613,
                BlockTypes.SJ_Wing_Middle_413,
                BlockTypes.SJ_Wing_Tail_132,
                BlockTypes.SJ_Wing_Tail_222,
                // weapons
                BlockTypes.SJ_Armour_Spike_Corner_112,
                BlockTypes.SJ_Catapult_234,
                BlockTypes.SJ_Chainsaw_215,
                BlockTypes.SJ_MachineGun_112,
                BlockTypes.SJ_HarpoonGun_212,
                BlockTypes.SJ_Drill_112,
                // etc
                BlockTypes.SJ_Radar_221,
                BlockTypes.SJ_Booster_123,
                BlockTypes.SJ_FuelTank_411,
            };
        }
        private static BlockCount ConvertToBlockCount(BlockTypes type)
        {
            BlockDetails BD = BlockIndexer.GetBlockDetails(type);
            if (BD.HasWheels)
                return new BlockCount(type, 2);
            else
                return new BlockCount(type, 1);
        }
        private static List<BlockCount> GetMoreAllowedBlocks(IEnumerable<BlockTypes> list)
        {
            List<BlockCount> batch = new List<BlockCount>();
            foreach (var item in list)
                batch.Add(ConvertToBlockCount(item));
            return batch;
        }

        public void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                SaveTechToDisk();
            }
        }
        public static void SaveTechToDisk()
        {
            try
            {
                if (Singleton.playerTank)
                {
                    List<StarterTech> toAdd = new List<StarterTech>();
                    string location = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.ToString();// Go to the cluster directory
                    string corpName = "GSO";
                    FactionSubTypes FST = Singleton.playerTank.GetMainCorp();
                    if (Enum.IsDefined(typeof(FactionSubTypes), FST))
                    {
                        corpName = FST.ToString();
                    }
                    else
                        corpName = ManMods.inst.FindCorpShortName(FST);
                    StarterTech ST = new StarterTech
                    {
                        Name = Singleton.playerTank.name,
                        Corp = corpName,
                        RawTech = BlockIndexer.SaveTechToRawJSON(Singleton.playerTank),
                        DeathStreakAwards = new List<MPBlockGroup>() { new MPBlockGroup("GSO_Cab_111", 2) },
                        KillStreakAwards = new List<MPBlockGroup>() { new MPBlockGroup("GSO_Cab_111", 2) },
                        LoadoutMainName = "temp1",
                        LoadoutSecondaryName = "temp2",
                        LoadoutMain = new List<string>() { "GSO_Cab_111" },
                        LoadoutSecondary = new List<string>() { "GSO_Cab_111" },
                    };
                    File.WriteAllText(location + "/StarterTech.json", JsonConvert.SerializeObject(ST, Formatting.Indented));
                }
            }
            catch (Exception e)
            { DebugBeS.Log("DeathmatchExt - Error in SaveTechToDisk: " + e); }
        }

        public static int SelectedPage = 0;
        private static int curPage = 0;
        public static void FirstLoadDeathmatchStartersPage(MultiplayerTechSelectGroupAsset MTSGA, ref List<MultiplayerTechSelectPresetAsset> TechList, int pageNum)
        {
            curPage = pageNum;
            try
            {
                if (pageNum <= 0)
                {
                    //Debug.Log("LoadDeathmatchStartersPage 0: " + TechList.Count());
                    TechList = GetPageDeathmatchTechs(TechList, 0, out List<FactionSubTypes> FST, out _);
                    UpdateDisp(FST);
                }
                else
                {
                    if (Ready)
                    {
                        TechList = GetPageDeathmatchTechs(TechList, pageNum, out List<FactionSubTypes> FST, out _);
                        UpdateDisp(FST);
                        //Debug.Log("LoadDeathmatchStartersPage " + pageNum + ": " + TechList.Count());
                    }
                    else
                    {
                        DebugBeS.Log("LoadDeathmatchStarters: Not ready yet");
                    }
                }
            }
            catch (Exception e) { DebugBeS.LogError("LoadDeathmatchStartersPage - OOOOOOOF " + e); }
        }
        public static void LoadDeathmatchStartersPage(UIScreenMultiplayerTechSelect SMTS, int pageNum)
        {
            if (pageNum == curPage)
                return;
            curPage = pageNum;
            try
            {
                if (pageDefaultNames == null)
                {
                    pageDefaultNames = new List<string>();
                    GetUIInBox(SMTS.transform, "Corp_01", "GSO", ref pageDefaultNames);
                    GetUIInBox(SMTS.transform, "Corp_02", "GC", ref pageDefaultNames);
                    GetUIInBox(SMTS.transform, "Corp_03", "VEN", ref pageDefaultNames);
                    GetUIInBox(SMTS.transform, "Corp_04", "HE", ref pageDefaultNames);
                    Transform title = HeavyObjectSearch(SMTS.transform, "Title"); // Name of Corp
                    title.GetComponent<Text>().text = title.GetComponent<Text>().text + " - Press [E] More";
                    DebugBeS.Log("Fetched pageDefaultNames");
                }
                List<string> loadNames;
                List<FactionSubTypes> FST;
                List<MultiplayerTechSelectPresetAsset> TechList = (List<MultiplayerTechSelectPresetAsset>)techList.GetValue(SMTS);
                if (pageNum <= 0)
                {
                    TechList = GetPageDeathmatchTechs(TechList, 0, out FST, out loadNames);
                    UpdateDisp(FST);
                    //Debug.Log("LoadDeathmatchStartersPage 0: " + TechList.Count());

                    // Then we set the UI!
                    //Debug.Log("names " + loadNames.Count);
                    SetupUIInBox(SMTS.transform, "Corp_01", "GSO", FST[0], loadNames[0], loadNames[1]);
                    SetupUIInBox(SMTS.transform, "Corp_02", "GC", FST[1], loadNames[2], loadNames[3]);
                    SetupUIInBox(SMTS.transform, "Corp_03", "VEN", FST[2], loadNames[4], loadNames[5]);
                    SetupUIInBox(SMTS.transform, "Corp_04", "HE", FST[3], loadNames[6], loadNames[7]);
                }
                else
                {/*
                    foreach (var item in ManMods.inst.GetCustomCorpIDs())
                    {
                        Debug.Log("LoadDeathmatchStartersPage  eewe: " + item);
                    }*/
                    if (Ready)
                    {
                        TechList = GetPageDeathmatchTechs(TechList, pageNum, out FST, out loadNames);
                        UpdateDisp(FST);
                        //Debug.Log("LoadDeathmatchStartersPage " + pageNum + ": " + TechList.Count());

                        // Then we set the UI!
                        //Debug.Log("names " + loadNames.Count);
                        SetupUIInBox(SMTS.transform, "Corp_01", "GSO", FST[0], loadNames[0], loadNames[1]);
                        SetupUIInBox(SMTS.transform, "Corp_02", "GC", FST[1], loadNames[2], loadNames[3]);
                        SetupUIInBox(SMTS.transform, "Corp_03", "VEN", FST[2], loadNames[4], loadNames[5]);
                        SetupUIInBox(SMTS.transform, "Corp_04", "HE", FST[3], loadNames[6], loadNames[7]);
                    }
                }
                techList.SetValue(SMTS, TechList);

                SMTS.ApplySelection();
            }
            catch (Exception e) { DebugBeS.LogError("LoadDeathmatchStartersPage - OOOOOOOF " + e); }
        }

        private static void GetUIInBox(Transform trans, string name, string boxCorpName, ref List<string> toAddTo)
        {
            Transform corpBox = HeavyObjectSearch(trans, name);
            Transform loadout1 = HeavyObjectSearch(corpBox, boxCorpName + "_Loadout_01"); // 
            Transform loadout2;
            if (boxCorpName.CompareTo("VEN") == 0)
                loadout2 = HeavyObjectSearch(corpBox, "Ven_Loadout_02"); //
            else
                loadout2 = HeavyObjectSearch(corpBox, boxCorpName + "_Loadout_02"); //
            toAddTo.Add(loadout1.GetComponent<Text>().text);
            toAddTo.Add(loadout2.GetComponent<Text>().text);
        }
        private static void SetupUIInBox(Transform trans, string name, string boxCorpName, FactionSubTypes faction, string firstLoadoutNew, string secondLoadoutNew)
        {
            Transform corpBox = HeavyObjectSearch(trans, name);
            Transform corpName = HeavyObjectSearch(corpBox, "Text"); // Name of Corp
            Transform corpIcon = HeavyObjectSearch(corpBox, "GSO_Logo"); // Icon of Corp
            Transform loadout1 = HeavyObjectSearch(corpBox, boxCorpName + "_Loadout_01"); // 
            Transform loadout2;
            if (boxCorpName.CompareTo("VEN") == 0)
                loadout2 = HeavyObjectSearch(corpBox, "Ven_Loadout_02"); //
            else
                loadout2 = HeavyObjectSearch(corpBox, boxCorpName + "_Loadout_02"); //
            if (ManMods.inst.IsModdedCorp(faction))
                corpName.GetComponent<Text>().text = ManMods.inst.FindCorpName(faction);
            else
                corpName.GetComponent<Text>().text = StringLookup.GetCorporationName(faction);
            corpIcon.GetComponent<Image>().sprite = ManUI.inst.GetCorpIcon(faction);
            try
            {
                loadout1.GetComponent<Text>().text = firstLoadoutNew;
            }
            catch
            {
                DebugBeS.Log("Error on loading " + name + " firstLoadoutNew");
            }
            try
            {
                loadout2.GetComponent<Text>().text = secondLoadoutNew;
            }
            catch
            {
                DebugBeS.Log("Error on loading " + name + " secondLoadoutNew");
            }
        }

        private static Transform HeavyObjectSearch(Transform trans, string name)
        {
            return trans.gameObject.GetComponentsInChildren<Transform>().ToList().Find(delegate (Transform cand) { return cand.name.CompareTo(name) == 0; });
        }
        public static void UpdateSelection()
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            {
                SelectedPage++;
                if (SelectedPage > MorePages.Count)
                    SelectedPage = 0;
            }
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                SelectedPage--;
                if (SelectedPage < 0)
                    SelectedPage = MorePages.Count;
            }
        }


        private static List<MultiplayerTechSelectPresetAsset> GetPageDeathmatchTechs(List<MultiplayerTechSelectPresetAsset> hostInst, int pageNum, out List<FactionSubTypes> Corps, out List<string> loadoutNames)
        {
            try
            {
                if (pageDefault == null)
                {
                    if (hostInst != null)
                    {
                        pageDefault = hostInst;
                        pageDefaultCorps = new List<FactionSubTypes> { FactionSubTypes.GSO, FactionSubTypes.GC, FactionSubTypes.VEN, FactionSubTypes.HE };
                    }
                }
            }
            catch (Exception e)
            { DebugBeS.Log("Error GetPageDeathmatchTechs - Could not fetch default Techs: " + e); }
            if (Ready && pageNum > 0)
            {
                DebugBeS.Log("GetPageDeathmatchTechs - Presented Custom Techs Page " + page);
                int selection = Mathf.Clamp(pageNum - 1, 0, MorePages.Count - 1);
                Corps = MorePagesCorps[selection];
                loadoutNames = MorePagesNames[selection];
                return MorePages[selection];
            }
            DebugBeS.Log("GetPageDeathmatchTechs - Presented Default Techs");
            Corps = pageDefaultCorps;
            loadoutNames = pageDefaultNames;
            return pageDefault;
        }

        private static MultiplayerTechSelectPresetAsset Temp;
        private static MultiplayerTechSelectPresetAsset MakeNewDeathmatchTechLegacy(string Name, string RTBlueprint, out FactionSubTypes Corp)
        {
            TechData TD = null;
            if (RTBlueprint != null)
                TD = BlockIndexer.RawTechToTechData(Name, RTBlueprint, out _);

            if (TD == null)
            {
                TD = new TechData();
                TD.Name = Name;
                TD.m_BlockSpecs = new List<TankPreset.BlockSpec>();
                TD.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
                TD.m_CreationData = new TechData.CreationData();
                TD.m_SkinMapping = new Dictionary<uint, string>();

                DebugBeS.Log("DeathmatchExt: No tech blueprint was set!");
                BlockTypes BT = BlockTypes.GSOCockpit_111;
                TD.m_BlockSpecs.Add(
                        new TankPreset.BlockSpec
                        {
                            m_BlockType = BT,
                            m_SkinID = 0,
                            m_VisibleID = 0,
                            block = "GSO_Cab",
                            position = IntVector3.zero,
                            orthoRotation = new OrthoRotation(Quaternion.LookRotation(Vector3.forward)),
                            saveState = new Dictionary<int, Module.SerialData>(),
                            textSerialData = new List<string>(),
                        }
                    );
                TD.m_BlockSpecs.Add(
                        new TankPreset.BlockSpec
                        {
                            m_BlockType = BT,
                            m_SkinID = 0,
                            m_VisibleID = 0,
                            block = "GSO_Cab",
                            position = IntVector3.forward,
                            orthoRotation = new OrthoRotation(Quaternion.LookRotation(Vector3.forward)),
                            saveState = new Dictionary<int, Module.SerialData>(),
                            textSerialData = new List<string>(),
                        }
                    );
            }

            TankPreset TP = TankPreset.CreateInstance();
            techData.SetValue(TP, TD); // Creates the base instance

            BlockCount[] BC1 = new BlockCount[3]
            {
                new BlockCount(BlockTypes.GSO_Cab_211, 1),
                new BlockCount(BlockTypes.HE_AITurret_112, 1),
                new BlockCount(BlockTypes.HE_AIModule_Guard_112, 6)
            };
            BlockCount[] BC2 = new BlockCount[2]
            {
                new BlockCount(BlockTypes.GSOCockpit_111, 4),
                new BlockCount(BlockTypes.HE_AIModule_Guard_112, 6)
            };
            BlockCount[] BC3 = new BlockCount[5]
            {
                new BlockCount(BlockTypes.GSOCockpit_111, 1),
                new BlockCount(BlockTypes.GSOAIGuardController_111, 1),
                new BlockCount(BlockTypes.HE_AITurret_112, 1),
                new BlockCount(BlockTypes.HE_AIModule_Guard_112, 1),
                new BlockCount(BlockTypes.BF_AIModule_Guard_212, 1)
            };

            MultiplayerTechSelectPresetAsset MTSPA = ScriptableObject.CreateInstance<MultiplayerTechSelectPresetAsset>();
            MTSPA.m_TankName = new LocalisedString() { m_Bank = Name, m_Id = "MOD" };
            MTSPA.m_TankPreset = TP;
            MTSPA.m_DeathStreakRewards = MakeDeathStreakRewards(BC3);

            MTSPA.m_KillStreakRewards = MakeKillStreakRewards(BC3);

            MTSPA.m_InventoryBlockList1 = MakeBlockList(BC1);
            MTSPA.m_InventoryBlockList2 = MakeBlockList(BC2);

            Corp = FactionSubTypes.GSO;
            Temp = MTSPA;
            return MTSPA;
        }

        private static bool MakeNewDeathmatchTech(StarterTech ST, out MultiplayerTechSelectPresetAsset MTSPA, out FactionSubTypes Corp, out List<BlockCount> newCorpBlocks, out List<string> names)
        {
            MTSPA = null;
            names = null;
            newCorpBlocks = new List<BlockCount>();
            Corp = FactionSubTypes.GSO;
            if (ST == null)
                return false;
            newCorpBlocks = ST.GetPermittedBlocks();
            if (!Enum.IsDefined(typeof(FactionSubTypes), ST.Corp))
            {
                if (ManMods.inst.FindCorp(ST.Corp) == null)
                    return false;
            }

            TechData TD = null;
            if (ST.RawTech != null)
                TD = BlockIndexer.RawTechToTechData(ST.Name, ST.RawTech, out _);
            if (TD == null || TD.m_BlockSpecs.Count == 0)
            {
                TD = new TechData();
                TD.Name = ST.Name;
                TD.m_BlockSpecs = new List<TankPreset.BlockSpec>();
                TD.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
                TD.m_CreationData = new TechData.CreationData();
                TD.m_SkinMapping = new Dictionary<uint, string>();

                if (TD == null)
                    DebugBeS.Log("DeathmatchExt: No tech blueprint was set!");
                if (TD.m_BlockSpecs.Count == 0)
                    DebugBeS.Log("DeathmatchExt: There were no blocks!");
                BlockTypes BT = BlockTypes.GSOCockpit_111;
                TD.m_BlockSpecs.Add(
                        new TankPreset.BlockSpec
                        {
                            m_BlockType = BT,
                            m_SkinID = 0,
                            m_VisibleID = 0,
                            block = "GSO_Cab",
                            position = IntVector3.zero,
                            orthoRotation = new OrthoRotation(Quaternion.LookRotation(Vector3.forward)),
                            saveState = new Dictionary<int, Module.SerialData>(),
                            textSerialData = new List<string>(),
                        }
                    );
            }
            else
            {
                
            }

            TankPreset TP = TankPreset.CreateInstance();
            techData.SetValue(TP, TD); // Creates the base instance

            MTSPA = ScriptableObject.CreateInstance<MultiplayerTechSelectPresetAsset>();
            MTSPA.m_TankName = new LocalisedString() { m_Bank = ST.Name, m_Id = "MOD",  };
            MTSPA.m_TankPreset = TP;
            MTSPA.m_DeathStreakRewards = MakeDeathStreakRewards(ST.GetBlockCounts(ST.DeathStreakAwards, 5));

            MTSPA.m_KillStreakRewards = MakeKillStreakRewards(ST.GetBlockCounts(ST.KillStreakAwards, 5));

            MTSPA.m_InventoryBlockList1 = MakeBlockList(ST.GetBlockCounts(ST.LoadoutMain));
            MTSPA.m_InventoryBlockList2 = MakeBlockList(ST.GetBlockCounts(ST.LoadoutSecondary));

            FactionSubTypes FST = ManMods.inst.GetCorpIndex(ST.Corp);
            if (FST < FactionSubTypes.GSO)
                Corp = FactionSubTypes.GSO;
            else
            {
                DebugBeS.Log("Corp " + ST.Corp + " ID:" + FST);
                Corp = FST;
            }
            Temp = MTSPA;
            names = new List<string> { ST.LoadoutMainName, ST.LoadoutSecondaryName };
            return MTSPA;
        }

        private static bool MakeNewDeathmatchTech(StarterTechInternal ST, out MultiplayerTechSelectPresetAsset MTSPA, out FactionSubTypes Corp, out List<string> names)
        {
            MTSPA = null;
            names = null;
            Corp = FactionSubTypes.GSO;
            if (ST == null)
                return false;
            TechData TD = null;
            if (ST.RawTech != null)
                TD = BlockIndexer.RawTechToTechData(ST.Name, ST.RawTech, out _);

            if (TD == null)
            {
                TD = new TechData();
                TD.Name = ST.Name;
                TD.m_BlockSpecs = new List<TankPreset.BlockSpec>();
                TD.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
                TD.m_CreationData = new TechData.CreationData();
                TD.m_SkinMapping = new Dictionary<uint, string>();

                DebugBeS.Log("DeathmatchExt: No tech blueprint was set!");
                BlockTypes BT = BlockTypes.GSOCockpit_111;
                TD.m_BlockSpecs.Add(
                        new TankPreset.BlockSpec
                        {
                            m_BlockType = BT,
                            m_SkinID = 0,
                            m_VisibleID = 0,
                            block = "GSO_Cab",
                            position = IntVector3.zero,
                            orthoRotation = new OrthoRotation(Quaternion.LookRotation(Vector3.forward)),
                            saveState = new Dictionary<int, Module.SerialData>(),
                            textSerialData = new List<string>(),
                        }
                    );
            }

            TankPreset TP = TankPreset.CreateInstance();
            techData.SetValue(TP, TD); // Creates the base instance

            MTSPA = ScriptableObject.CreateInstance<MultiplayerTechSelectPresetAsset>();
            MTSPA.m_TankName = new LocalisedString() { m_Bank = ST.Name, m_Id = "MOD" };
            MTSPA.m_TankPreset = TP;
            MTSPA.m_DeathStreakRewards = MakeDeathStreakRewards(ST.DeathStreakAwards.ToArray());

            MTSPA.m_KillStreakRewards = MakeKillStreakRewards(ST.KillStreakAwards.ToArray());

            MTSPA.m_InventoryBlockList1 = MakeBlockList(ST.LoadoutMain.ToArray());
            MTSPA.m_InventoryBlockList2 = MakeBlockList(ST.LoadoutSecondary.ToArray());

            Corp = ST.Corp;
            Temp = MTSPA;
            names = new List<string> { ST.LoadoutMainName, ST.LoadoutSecondaryName };
            return MTSPA;
        }


        private static void UpdateDisp(List<FactionSubTypes> FST)
        {
            try
            {
                UIScreenMultiplayerTechSelect UISMTS = (UIScreenMultiplayerTechSelect)ManUI.inst.GetScreen(ManUI.ScreenType.MultiplayerTechSelect);
                List<UIMultiplayerTechSelectItem> MTSI = (List<UIMultiplayerTechSelectItem>)corpList.GetValue(UISMTS);
                int step = 0;
                foreach (var item in FST)
                {
                    MTSI[step].SetCorpIndex(step, item);
                }
            }
            catch { }
        }
        private static InventoryBlockList MakeBlockList(BlockCount[] BC)
        {
            InventoryBlockList IBL = new InventoryBlockList();
            IBL.m_BlockList = BC;
            return IBL;
        }

        private static MultiplayerDeathStreakRewards MakeDeathStreakRewards(BlockCount[] BC)
        {
            MultiplayerDeathStreakRewards MDSRS = ScriptableObject.CreateInstance<MultiplayerDeathStreakRewards>();
            MultiplayerDeathStreakReward[] MDSR = new MultiplayerDeathStreakReward[5]
            {
                new MultiplayerDeathStreakReward{
                    m_Rewards = new BlockCount[1]{
                        BC[0]
                    }
                },
                new MultiplayerDeathStreakReward{
                    m_Rewards = new BlockCount[1]{
                        BC[1]
                    }
                },
                new MultiplayerDeathStreakReward{
                    m_Rewards = new BlockCount[1]{
                        BC[2]
                    }
                },
                new MultiplayerDeathStreakReward{
                    m_Rewards = new BlockCount[1]{
                        BC[3]
                    }
                },
                new MultiplayerDeathStreakReward{
                    m_Rewards = new BlockCount[1]{
                        BC[4]
                    }
                },
            };

            MDSRS.m_RewardLevels = MDSR;
            return MDSRS;
        }
        private static MultiplayerKillStreakRewardAsset MakeKillStreakRewards(BlockCount[] BC)
        {
            MultiplayerKillStreakRewardAsset MKSRA = ScriptableObject.CreateInstance<MultiplayerKillStreakRewardAsset>();
            MultiplayerKillStreakRewardLevel[] MDSR = new MultiplayerKillStreakRewardLevel[5]
            {
                new MultiplayerKillStreakRewardLevel{
                    m_KillsRequired = 1,
                    m_BlockReward = BC[0]
                },
                new MultiplayerKillStreakRewardLevel{
                    m_KillsRequired = 2,
                    m_BlockReward = BC[1]
                },
                new MultiplayerKillStreakRewardLevel{
                    m_KillsRequired = 3,
                    m_BlockReward = BC[2]
                },
                new MultiplayerKillStreakRewardLevel{
                    m_KillsRequired = 4,
                    m_BlockReward = BC[3]
                },
                new MultiplayerKillStreakRewardLevel{
                    m_KillsRequired = 5,
                    m_BlockReward = BC[4]
                },
            };

            MKSRA.m_RewardLevels = MDSR;
            return MKSRA;
        }
    }

    [Serializable]
    public class StarterTech
    {
        public string Name = "Unset";
        public string Corp = "GSO";
        public string RawTech = "";
        public List<MPBlockGroup> DeathStreakAwards = new List<MPBlockGroup>();
        public List<MPBlockGroup> KillStreakAwards = new List<MPBlockGroup>();

        public string LoadoutMainName = "Loadout 1";
        public List<string> LoadoutMain = new List<string>();
        public string LoadoutSecondaryName = "Loadout 2";
        public List<string> LoadoutSecondary = new List<string>();
        public List<string> PermittedBlocks = new List<string>();

        public List<BlockCount> GetPermittedBlocks()
        {
            List<BlockCount> BC = new List<BlockCount>();
            foreach (var item in PermittedBlocks)
            {
                if (item != null && BlockIndexer.StringToBlockType(item, out BlockTypes BT))
                {
                    BC.Add(new BlockCount(BT, 0));
                }
            }
            return BC;
        }
        public BlockCount[] GetBlockCounts(List<MPBlockGroup> group, int MandatoryCount = 0)
        {
            if (MandatoryCount == 0)
                MandatoryCount = group.Count;
            BlockCount[] BC = new BlockCount[MandatoryCount];
            for (int step = 0; step < MandatoryCount; step++)
            {
                MPBlockGroup prev = null;
                BlockCount prevC = null;
                if (group.Count > step + 1 && group[step] != null)
                {
                    prev = group[step];
                    if (prev.Count > 0 && BlockIndexer.StringToBlockType(prev.BlockType, out BlockTypes BT))
                    {
                        prevC = new BlockCount(BT, prev.Count);
                    }
                }
                if (prevC == null)
                    BC[step] = fallback;
                else
                    BC[step] = prevC;

            }
            return BC;
        }
        public BlockCount[] GetBlockCounts(List<string> groupRaw)
        {
            List<BlockTypes> group = new List<BlockTypes>();
            foreach (var item in groupRaw)
            {
                if (item != null && BlockIndexer.StringToBlockType(item, out BlockTypes BT))
                {
                    group.Add(BT);
                }
            }
            List<BlockCount> BC = new List<BlockCount>();
            BlockTypes batching = BlockTypes.GSOAIController_111;
            BlockCount prevC = null;
            foreach (var item in group.Distinct())
            {
                batching = item;
                BC.Add(new BlockCount(item, group.Count(x => x == item)));
            }
            if (batching == BlockTypes.GSOAIController_111)
                return new BlockCount[1] { fallback };
            return BC.ToArray();
        }
        private static BlockCount fallback => new BlockCount(BlockTypes.GSOCockpit_111, 1);
    }
    [Serializable]
    public class MPBlockGroup
    {
        public string BlockType = null;
        public int Count = 1;

        public MPBlockGroup(string blockNameGameObject, int count = 1)
        {
            BlockType = blockNameGameObject;
            Count = count;
        }
    }

    internal class StarterTechInternal
    {
        public string Name = "Unset";
        public FactionSubTypes Corp = FactionSubTypes.GSO;
        public string RawTech = null;
        public List<BlockCount> DeathStreakAwards = new List<BlockCount>();
        public List<BlockCount> KillStreakAwards = new List<BlockCount>();

        public string LoadoutMainName = "Loadout 1";
        public List<BlockCount> LoadoutMain = new List<BlockCount>();
        public string LoadoutSecondaryName = "Loadout 2";
        public List<BlockCount> LoadoutSecondary = new List<BlockCount>();
    }
}


