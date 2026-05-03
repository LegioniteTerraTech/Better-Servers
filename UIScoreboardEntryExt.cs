using System;
using UnityEngine;
using TerraTechETCUtil;
using TerraTech.Network;

namespace Better_Servers
{
    public class UIScoreboardEntryExt : MonoBehaviour
    {
        public BetterServerPlayer infractions;
        public static void Insure(UIScoreBoardEntry entry, NetPlayer player, UIScoreBoardHUD scoreboard)
        {
            if (entry == null)
                return;
            UIScoreboardEntryExt ext = entry.GetComponent<UIScoreboardEntryExt>();
            if (!ext)
            {
                UIHelpersExt.PrintAllComponentsGameObjectDepth<Component>(entry.gameObject);
                ext = entry.gameObject.AddComponent<UIScoreboardEntryExt>();
            }
            string ID = ManNetworkLobby.inst.LobbySystem.GetPersistentPlayerID(player.GetPlayerIDInLobby()).ToString();
            if (!KickStartBetterServers.blockedInfo.userInfractionsHost.TryGetValue(ID, out var BSP))
            {
                BSP = BetterServerPlayer.GetPlayer(player);
                KickStartBetterServers.blockedInfo.userInfractionsHost.Add(ID, BSP);
            }
            ext.infractions = BSP;
        }
    }
}
