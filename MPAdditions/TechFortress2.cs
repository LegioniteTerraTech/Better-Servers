using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Better_Servers
{
    /// <summary>
    /// A fortress-themed battle mode!
    /// </summary>
    public class TechFortress2
    {
        private static FieldInfo togglePrefab = typeof(UIScreenNetworkLobby).GetField("m_InventoryToggle", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool FortressMode = false;
        public static bool IsFortressMode => FortressMode && ManNetwork.inst.GetTeamCount() == 2;



        private static Toggle toggleFortress = null;

        public static void InsureChanges(UIScreenNetworkLobby __instance)
        {
            if (toggleFortress == null)
            {
                Toggle prefab = (Toggle)togglePrefab.GetValue(__instance);
                var newToggle = UnityEngine.Object.Instantiate(prefab.transform, prefab.transform.parent);
                Vector3 ver = prefab.GetComponent<RectTransform>().anchoredPosition3D;
                ver.y = ver.y - 30;
                newToggle.GetComponent<RectTransform>().anchoredPosition3D = ver;
                //DebugBeS.Log("TREE - " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(newToggle.gameObject, " |"));


                toggleFortress = newToggle.GetComponent<Toggle>();
                try
                {
                    UILocalisedText Te2 = newToggle.transform.Find("Label").GetComponent<UILocalisedText>();
                    LocalisedString LS = new LocalisedString()
                    {
                        m_Bank = "Fortress",
                        m_GUIExpanded = true,
                        m_Id = "Fortress",
                        m_InlineGlyphs = new Localisation.GlyphInfo[0],
                    };
                    Te2.m_String = LS;
                    DebugBeS.Log("Init new lobby menu button");
                }
                catch (Exception e)
                {
                    DebugBeS.Log("Init new lobby menu button FAILED - " + e);
                }
            }
        }
        internal static void SetHost(UIScreenNetworkLobby __instance, bool isHost)
        {
            DebugBeS.Log("SetHost called");
            InsureChanges(__instance);
            toggleFortress.interactable = isHost;
        }
        private static void SetFortressMode(bool setting)
        {
            DebugBeS.Log("SetFortressMode called");
            FortressMode = setting;
        }

        public static void ModLobby(UIScreenNetworkLobby __instance)
        {
            DebugBeS.Log("ModLobby called");
            InsureChanges(__instance);
            toggleFortress.onValueChanged.AddListener(new UnityAction<bool>(SetFortressMode));
        }
        public static void ModLobbyEnd(UIScreenNetworkLobby __instance)
        {
            DebugBeS.Log("ModLobbyEnd called");
            InsureChanges(__instance);
            toggleFortress.onValueChanged.RemoveListener(new UnityAction<bool>(SetFortressMode));
        }
    }
}
