using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace TerraTechETCUtil
{
    // STEAM ONLY
    /// <summary>
    /// A temporary way of doing reverse block lookups for the time being
    /// </summary>
    public class BlockIndexer : MonoBehaviour
    {
        private static BlockIndexer inst;
        private static bool isBlockInjectorPresent => false;
        private static string FolderDivider => "/";
        private static bool Compiled = false;


        /// <summary>
        /// Searches Block Injector for the block based on root GameObject name.
        /// </summary>
        /// <param name="mem">The name of the block's root GameObject.  This is also set in the Official Mod Tool by the Name ID (filename of the .json), not the name you give it.</param>
        /// <returns>The Block Type to use if it found it, otherwise returns BlockTypes.GSOCockpit_111</returns>
        public static BlockTypes StringToBlockType(string mem)
        {
            if (!Enum.TryParse(mem, out BlockTypes type))
            {
                if (!TryGetMismatchNames(mem, ref type))
                {
                    if (StringToBIBlockType(mem, out BlockTypes BTC))
                    {
                        return BTC;
                    }
                    type = GetBlockIDLogFree(mem);
                }
            }
            return type;
        }
        /// <summary>
        /// Searches the ENTIRE GAME for the block based on root GameObject name.
        /// </summary>
        /// <param name="mem">The name of the block's root GameObject.  This is also set in the Official Mod Tool by the Name ID (filename of the .json), not the name you give it.</param>
        /// <param name="BT">The Block Type to use if it found it</param>
        /// <returns>True if it found it in Block Injector</returns>
        public static bool StringToBlockType(string mem, out BlockTypes BT)
        {
            if (!Enum.TryParse(mem, out BT))
            {
                if (!TryGetMismatchNames(mem, ref BT))
                {
                    if (StringToBIBlockType(mem, out BT))
                        return true;
                    if (GetBlockIDLogFree(mem, out BT))
                        return true;
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Searches Block Injector for the block based on root GameObject name.
        /// </summary>
        /// <param name="mem">The name of the block's root GameObject.  This is also set in the Official Mod Tool by the Name ID (filename of the .json), not the name you give it.</param>
        /// <param name="BT">The Block Type to use if it found it</param>
        /// <returns>True if it found it in Block Injector</returns>
        public static bool StringToBIBlockType(string mem, out BlockTypes BT) // BLOCK INJECTOR
        {
            BT = BlockTypes.GSOAIController_111;

            if (TryGetIDSwap(mem.GetHashCode(), out BlockTypes BTC))
            {
                BT = BTC;
                return true;
            }
            return false;
        }

        public static BlockTypes GetBlockIDLogFree(string name)
        {
            if (ModdedBlocksGrabbed == null)
                PrepareModdedBlocksSearch();
            if (ModdedBlocksGrabbed != null && ModdedBlocksGrabbed.TryGetValue(name, out int blockType))
                return (BlockTypes)blockType;
            else if (name == "GSO_Exploder_A1_111")
                return (BlockTypes)622;
            return BlockTypes.GSOCockpit_111;
        }

        public static bool GetBlockIDLogFree(string name, out BlockTypes BT)
        {
            if (ModdedBlocksGrabbed == null)
                PrepareModdedBlocksSearch();
            if (ModdedBlocksGrabbed != null && ModdedBlocksGrabbed.TryGetValue(name, out int blockType))
            {
                BT = (BlockTypes)blockType;
                return true;
            }
            else if (name == "GSO_Exploder_A1_111")
            {
                BT = (BlockTypes)622;
                return true;
            }
            BT = BlockTypes.GSOCockpit_111;
            return false;
        }


        // Logless block loader
        private static Dictionary<string, int> ModdedBlocksGrabbed;
        private static readonly FieldInfo allModdedBlocks = typeof(ManMods).GetField("m_BlockIDReverseLookup", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Dictionary<int, BlockTypes> errorNames = new Dictionary<int, BlockTypes>();
        public static void ResetBlockLookupList()
        {
            if (!Compiled)
                return;
            errorNames.Clear();
            try
            {
                ManGameMode.inst.ModeStartEvent.Unsubscribe(ConstructBlockLookupListTrigger);
            }
            catch { }
            Compiled = false;
        }
        /// <summary>
        /// Builds the lookup to use when using block names to find BlockTypes
        /// </summary>
        public static void ConstructBlockLookupListDelayed()
        {
            if (Compiled)
                return;
            if (inst == null)
            {
                inst = new GameObject("BlockIndexerUtil").AddComponent<BlockIndexer>();
            }
            ManGameMode.inst.ModeStartEvent.Subscribe(ConstructBlockLookupListTrigger);
        }
        public static void ConstructBlockLookupListTrigger(Mode mode)
        {
            ConstructBlockLookupList();
        }

        /// <summary>
        /// Builds the lookup to use when using block names to find BlockTypes
        /// </summary>
        public static void ConstructBlockLookupList()
        {
            if (Compiled)
                return;
            ManGameMode.inst.ModeStartEvent.Unsubscribe(ConstructBlockLookupListTrigger);
            try
            {
                List<BlockTypes> types = Singleton.Manager<ManSpawn>.inst.GetLoadedTankBlockNames().ToList();
                foreach (BlockTypes type in types)
                {
                    TankBlock prefab = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type);
                    string name = prefab.name;
                    if (prefab.GetComponent<Damageable>() && type.ToString() != name) //&& !Singleton.Manager<ManMods>.inst.IsModdedBlock(type))
                    {
                        int hash = name.GetHashCode();
                        if (!errorNames.Keys.Contains(hash))
                        {
                            errorNames.Add(hash, type);
#if DEBUG
                            /*
                        if ((int)type > 5000)
                            Debug.Log("TACtical_AI: ConstructErrorBlocksList - Added Modded Block " + name + " | " + type.ToString());
                            */
#endif
                        }
                    }
                }
                ConstructModdedIDList();
            }
            catch { };

            Debug.Log("BlockUtils: ConstructErrorBlocksList - There are " + errorNames.Count + " blocks with names not equal to their type");
            Compiled = true;
        }

        /// <summary>
        /// Delay this until AFTER Block Injector to setup the lookups
        /// </summary>
        /// <summary>
        /// Call at least once to hook up to modding
        /// </summary>
        public static void PrepareModdedBlocksSearch()
        {
            ModdedBlocksGrabbed = (Dictionary<string, int>)allModdedBlocks.GetValue(Singleton.Manager<ManMods>.inst);
        }

        public static void ConstructModdedIDList()
        {
            ModSessionInfo session = (ModSessionInfo)access.GetValue(ManMods.inst);
            UnOf_Offi.Clear();
            try
            {
                Dictionary<int, string> blocc = session.BlockIDs;
                foreach (KeyValuePair<int, string> pair in blocc)
                {
                    ModdedBlockDefinition MBD = ManMods.inst.FindModdedAsset<ModdedBlockDefinition>(pair.Value);

                    string SCAN = MBD.m_Json.text;

                    if (SCAN.Contains("NuterraBlock"))
                    {
                        int num = 0;
                        string name = "";
                        if (FindInt(SCAN, "\"ID\":", ref num)) //&& FindText(SCAN, "\"Name\" :", ref name))
                        {
                            UnOf_Offi.Add(("_C_BLOCK:" + num.ToString()).GetHashCode(), (BlockTypes)ManMods.inst.GetBlockID(MBD.name));
                            //Debug.Log("TACtical_AI: ConstructModdedIDList - " + "_C_BLOCK:" + num.ToString() + " | " + MBD.name + " | " + (BlockTypes)ManMods.inst.GetBlockID(MBD.name));
                        }
                    }
                }
            }
            catch { Debug.Log("BlockUtils: ConstructModdedIDList - Error on compile"); };
            Debug.Log("BlockUtils: ConstructModdedIDList - compiled " + UnOf_Offi.Count());
        }


        private static bool TryGetMismatchNames(string name, ref BlockTypes type)
        {
            if (errorNames.TryGetValue(name.GetHashCode(), out BlockTypes val))
            {
                type = val;
                return true;
            }
            return false;
        }
        private static bool GetNameJSON(string FolderDirectory, out string output, bool excludeJSON)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == FolderDivider.ToCharArray()[0])
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }
            if (!final.ToString().Contains(".RAWTECH"))
            {
                if (!final.ToString().Contains(".JSON") && !excludeJSON)
                {
                    output = null;
                    return false;
                }
                else
                    final.Remove(final.Length - 5, 5);// remove ".JSON"
            }
            else
                final.Remove(final.Length - 8, 8);// remove ".RAWTECH"

            output = final.ToString();
            return true;
        }

        private static Dictionary<int, BlockTypes> UnOf_Offi = new Dictionary<int, BlockTypes>();
        private static FieldInfo access = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool TryGetIDSwap(int hash, out BlockTypes blockType)
        {
            return UnOf_Offi.TryGetValue(hash, out blockType);
        }

        private static bool FindInt(string text, string searchBase, ref int intCase)
        {
            int indexFind = text.IndexOf(searchBase);
            if (indexFind >= 0)
            {
                int searchEnd = 0;
                int searchLength = 0;
                string output = "";
                try
                {
                    searchEnd = indexFind + searchBase.Length;
                    searchLength = text.Substring(searchEnd).IndexOf(",");
                    if (searchLength != -1)
                    {
                        output = text.Substring(searchEnd, searchLength);
                        intCase = (int)float.Parse(output);
                        return true;
                    }
                    //Debug.Log(searchEnd + " | " + searchLength + " | " + output + " | ");
                }
                catch (Exception e) { Debug.LogError(searchEnd + " | " + searchLength + " | " + output + " | " + e); }
            }
            return false;
        }
        private static bool FindText(string text, string searchBase, ref string name)
        {
            int indexFind = text.IndexOf(searchBase);
            if (indexFind >= 0)
            {
                int searchEnd = 0;
                int searchLength = 0;
                string output = "";
                try
                {
                    searchEnd = indexFind + searchBase.Length;
                    searchLength = text.Substring(searchEnd).IndexOf(",");
                    output = text.Substring(searchEnd, searchLength);
                    name = output;
                    return true;
                }
                catch (Exception e) { Debug.LogError(searchEnd + " | " + searchLength + " | " + output + " | " + e); }
            }
            return false;
        }



        // RawTech Support

        private static List<BlockMemory> JSONToMemory(string toLoad)
        {   // Loading a Tech from the BlockMemory
            StringBuilder RAW = new StringBuilder();
            foreach (char ch in toLoad)
            {
                if (ch != '\\')
                {
                    RAW.Append(ch);
                }
            }
            List<BlockMemory> mem = new List<BlockMemory>();
            StringBuilder blockCase = new StringBuilder();
            string RAWout = RAW.ToString();
            foreach (char ch in RAWout)
            {
                if (ch == '|')//new block
                {
                    mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase.ToString()));
                    blockCase.Clear();
                }
                else
                    blockCase.Append(ch);
            }
            mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase.ToString()));
            //Debug.Log("TACtical_AI:  DesignMemory: saved " + mem.Count);
            return mem;
        }
        private static FactionTypesExt GetCorpExtended(BlockTypes type)
        {
            return (FactionTypesExt)Singleton.Manager<ManSpawn>.inst.GetCorporation(type);
        }
        private static FactionSubTypes CorpExtToCorp(FactionTypesExt corpExt)
        {
            switch (corpExt)
            {
                case FactionTypesExt.GT:
                case FactionTypesExt.IEC:
                    return FactionSubTypes.GSO;
                case FactionTypesExt.EFF:
                case FactionTypesExt.LK:
                    return FactionSubTypes.GC;
                case FactionTypesExt.OS:
                    return FactionSubTypes.VEN;
                case FactionTypesExt.BL:
                case FactionTypesExt.TAC:
                    return FactionSubTypes.HE;
                case FactionTypesExt.DL:
                case FactionTypesExt.EYM:
                case FactionTypesExt.HS:
                    return FactionSubTypes.BF;
            }
            return (FactionSubTypes)corpExt;
        }
        internal static TechData RawTechToTechData(string name, string blueprint, out int[] blockIDs)
        {
            if (!Compiled)
            {
                ConstructBlockLookupList();
            }
            TechData data = new TechData();
            data.Name = name;
            data.m_Bounds = new IntVector3(new Vector3(18, 18, 18));
            data.m_SkinMapping = new Dictionary<uint, string>();
            data.m_TechSaveState = new Dictionary<int, TechComponent.SerialData>();
            data.m_CreationData = new TechData.CreationData();
            data.m_BlockSpecs = new List<TankPreset.BlockSpec>();
            List<BlockMemory> mems = JSONToMemory(blueprint);
            List<int> BTs = new List<int>();

            bool skinChaotic = UnityEngine.Random.Range(0, 100) < 2;
            byte skinset = (byte)UnityEngine.Random.Range(0, 2);
            byte skinset2 = (byte)UnityEngine.Random.Range(0, 1);
            foreach (BlockMemory mem in mems)
            {
                BlockTypes type = StringToBlockType(mem.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) || !TechDataAvailValidation.IsBlockAvailableInMode(type))
                {
                    Debug.Log("TACtical_AI: InstantTech - Removed " + mem.t + " as it was invalidated");
                    continue;
                }
                if (!BTs.Contains((int)type))
                {
                    BTs.Add((int)type);
                }

                TankPreset.BlockSpec spec = default;
                spec.block = mem.t;
                spec.m_BlockType = type;
                spec.orthoRotation = new OrthoRotation(mem.r);
                spec.position = mem.p;
                spec.saveState = new Dictionary<int, Module.SerialData>();
                spec.textSerialData = new List<string>();
                FactionTypesExt factType = GetCorpExtended(type);
                if (skinChaotic)
                {
                    byte rand = (byte)UnityEngine.Random.Range(0, 2);
                    if (factType == FactionTypesExt.GSO && rand != 0)
                        rand += 3;
                    if (!ManDLC.inst.IsSkinDLC(rand, CorpExtToCorp(factType)))
                        spec.m_SkinID = rand;
                    else
                        spec.m_SkinID = 0;
                }
                else
                {
                    if (factType == FactionTypesExt.GSO)
                    {
                        if (!ManDLC.inst.IsSkinDLC(skinset + (skinset != 0 ? 3 : 0), CorpExtToCorp(factType)))
                            spec.m_SkinID = (byte)(skinset + (skinset != 0 ? 3 : 0));
                        else if (!ManDLC.inst.IsSkinDLC(skinset2 + (skinset2 != 0 ? 3 : 0), CorpExtToCorp(factType)))
                            spec.m_SkinID = (byte)(skinset2 + (skinset2 != 0 ? 3 : 0));
                        else
                            spec.m_SkinID = 0;
                    }
                    else
                    {
                        if (!ManDLC.inst.IsSkinDLC(skinset, CorpExtToCorp(factType)))
                            spec.m_SkinID = skinset;
                        else if (!ManDLC.inst.IsSkinDLC(skinset2, CorpExtToCorp(factType)))
                            spec.m_SkinID = skinset2;
                        else
                            spec.m_SkinID = 0;
                    }
                }

                data.m_BlockSpecs.Add(spec);
            }
            //Debug.Log("TACtical_AI: ExportRawTechToTechData - Exported " + name);

            blockIDs = BTs.ToArray();
            return data;
        }

        public static string SaveTechToRawJSON(Tank tank)
        {
            return TechToJSONExternal(tank);
        }

        /// <summary>
        /// Mandatory for techs that have plans to be built over time by the building AI.
        /// The first block being an anchored block will determine if the entire techs live or not
        ///   on spawning. If this fails, there's a good chance the AI could have wasted money on it.
        /// </summary>
        /// <param name="ToSearch">The list of blocks to find the new root in</param>
        /// <returns>The new Root block</returns>
        public static TankBlock FindProperRootBlockExternal(List<TankBlock> ToSearch)
        {
            bool IsAnchoredAnchorPresent = false;
            float close = 128 * 128;
            TankBlock newRoot = ToSearch.First();
            foreach (TankBlock bloc in ToSearch)
            {
                Vector3 blockPos = bloc.CalcFirstFilledCellLocalPos();
                float sqrMag = blockPos.sqrMagnitude;
                if (bloc.GetComponent<ModuleAnchor>() && bloc.GetComponent<ModuleAnchor>().IsAnchored)
                {   // If there's an anchored anchor, then we base the root off of that
                    //  It's probably a base
                    IsAnchoredAnchorPresent = true;
                    break;
                }
                if (sqrMag < close && (bloc.GetComponent<ModuleTechController>() || bloc.GetComponent<ModuleAIBot>()))
                {
                    close = sqrMag;
                    newRoot = bloc;
                }
            }
            if (IsAnchoredAnchorPresent)
            {
                close = 128 * 128;
                foreach (TankBlock bloc in ToSearch)
                {
                    Vector3 blockPos = bloc.CalcFirstFilledCellLocalPos();
                    float sqrMag = blockPos.sqrMagnitude;
                    if (sqrMag < close && bloc.GetComponent<ModuleAnchor>() && bloc.GetComponent<ModuleAnchor>().IsAnchored)
                    {
                        close = sqrMag;
                        newRoot = bloc;
                    }
                }
            }
            return newRoot;
        }
        private static List<BlockMemory> TechToMemoryExternal(Tank tank)
        {
            // This resaves the whole tech cab-forwards regardless of original rotation
            //   It's because any solutions that involve the cab in a funny direction will demand unholy workarounds.
            //   I seriously don't know why the devs didn't try it this way, perhaps due to lag reasons.
            //   or the blocks that don't allow upright placement (just detach those lmao)
            List<BlockMemory> output = new List<BlockMemory>();
            List<TankBlock> ToSave = tank.blockman.IterateBlocks().ToList();
            Vector3 coreOffset = Vector3.zero;
            Quaternion coreRot;
            TankBlock rootBlock = FindProperRootBlockExternal(ToSave);
            if (rootBlock != null)
            {
                if (rootBlock != ToSave.First())
                {
                    ToSave.Remove(rootBlock);
                    ToSave.Insert(0, rootBlock);
                }
                coreOffset = rootBlock.trans.localPosition;
                coreRot = rootBlock.trans.localRotation;
                tank.blockman.SetRootBlock(rootBlock);
            }
            else
                coreRot = new OrthoRotation(OrthoRotation.r.u000);

            foreach (TankBlock bloc in ToSave)
            {
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(bloc.BlockType))
                    continue;
                Quaternion deltaRot = Quaternion.Inverse(coreRot);
                BlockMemory mem = new BlockMemory
                {
                    t = bloc.name,
                    p = deltaRot * (bloc.trans.localPosition - coreOffset)
                };
                // get rid of floating point errors
                mem.TidyUp();
                //Get the rotation
                mem.r = SetCorrectRotation(bloc.trans.localRotation, deltaRot).rot;
                if (!IsValidRotation(bloc, mem.r))
                {   // block cannot be saved - illegal rotation.
                    Debug.Log("TACtical_AI:  DesignMemory - " + tank.name + ": could not save " + bloc.name + " in blueprint due to illegal rotation.");
                    continue;
                }
                output.Add(mem);
            }

            return output;
        }
        public static string TechToJSONExternal(Tank tank)
        {   // Saving a Tech from the BlockMemory
            return MemoryToJSONExternal(TechToMemoryExternal(tank));
        }
        private static string MemoryToJSONExternal(List<BlockMemory> mem)
        {   // Saving a Tech from the BlockMemory
            if (mem.Count == 0)
                return null;
            StringBuilder JSONTechRAW = new StringBuilder();
            JSONTechRAW.Append(JsonUtility.ToJson(mem.First()));
            for (int step = 1; step < mem.Count; step++)
            {
                JSONTechRAW.Append("|");
                JSONTechRAW.Append(JsonUtility.ToJson(mem.ElementAt(step)));
            }
            string JSONTechRAWout = JSONTechRAW.ToString();
            StringBuilder JSONTech = new StringBuilder();
            foreach (char ch in JSONTechRAWout)
            {
                if (ch == '"')
                {
                    //JSONTech.Append("\\");
                    JSONTech.Append(ch);
                }
                else
                    JSONTech.Append(ch);
            }
            //Debug.Log("TACtical_AI: " + JSONTech.ToString());
            return JSONTech.ToString();
        }

        public static bool IsValidRotation(TankBlock TB, OrthoRotation.r r)
        {
            return true; // can't fetch proper context for some reason
        }

        public static OrthoRotation SetCorrectRotation(Quaternion blockRot, Quaternion changeRot)
        {
            Quaternion qRot2 = Quaternion.identity;
            Vector3 endRotF = blockRot * Vector3.forward;
            Vector3 endRotU = blockRot * Vector3.up;
            Vector3 foA = changeRot * endRotF;
            Vector3 upA = changeRot * endRotU;
            qRot2.SetLookRotation(foA, upA);
            OrthoRotation rot = new OrthoRotation(qRot2);
            if (rot != qRot2)
            {
                bool worked = false;
                for (int step = 0; step < OrthoRotation.NumDistinctRotations; step++)
                {
                    OrthoRotation rotT = new OrthoRotation(OrthoRotation.AllRotations[step]);
                    bool isForeMatch = rotT * Vector3.forward == foA;
                    bool isUpMatch = rotT * Vector3.up == upA;
                    if (isForeMatch && isUpMatch)
                    {
                        rot = rotT;
                        worked = true;
                        break;
                    }
                }
                if (!worked)
                {
                    Debug.Log("TerraTechETCUtil: ReplaceBlock - Matching failed - OrthoRotation is missing edge case");
                }
            }
            return rot;
        }



        [Serializable]
        private class BlockMemory
        {   // Save the blocks!
            public string t = BlockTypes.GSOAIController_111.ToString(); //blocktype
            public Vector3 p = Vector3.zero;
            public OrthoRotation.r r = OrthoRotation.r.u000;

            /// <summary>
            /// get rid of floating point errors
            /// </summary>
            public void TidyUp()
            {
                p.x = Mathf.RoundToInt(p.x);
                p.y = Mathf.RoundToInt(p.y);
                p.z = Mathf.RoundToInt(p.z);
            }
        }

        private enum FactionTypesExt
        {
            // No I do not make any of the corps below (exclusing some of TAC and EFF) 
            //  - but these are needed to allow the AI to spawn the right bases with 
            //    the right block ranges
            // OFFICIAL
            NULL,   // not a corp, really, probably the most unique of all lol
            GSO,    // Galactic Survey Organization
            GC,     // GeoCorp
            EXP,    // Reticule Research
            VEN,    // VENture
            HE,     // HawkEye
            SPE,    // Special
            BF,     // Better Future
            SJ,     // Space Junkers
            LEG,    // LEGION!!1!

            // Community
            AER,    // Aerion
            BL,     // Black Labs (EXT OF HE)
            CC,     // CrystalCorp
            DC,     // DEVCorp
            DL,     // DarkLight
            EYM,    // Ellydium
            GT,     // GreenTech
            HS,     // Hyperion Systems
            IEC,    // Independant Earthern Colonies
            LK,     // Lemon Kingdom
            OS,     // Old Stars
            TC,     // Tofuu Corp
            TAC,    // Technocratic AI Colony

            // idk
            EFF,    // Emperical Forge Fabrication
            MCC,    // Mechaniccoid Cooperative Confederacy 
            BLN,    // BuLwark Nation (Bulin)
            CNC,    // ClaNg Clads (ChanClas)
            LOL,    // Larry's Overlord Laser
        }
    }

}
