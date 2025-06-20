using System;
using System.Collections.Generic;
using TerraTech.Network;

namespace Better_Servers
{
    internal class BetterServerPlayer
    {
        internal NetPlayer Player;
        internal int InfractionCount = 0;

        private static Dictionary<PersistentPlayerID,BetterServerPlayer> players = new Dictionary<PersistentPlayerID, BetterServerPlayer>();

        internal BetterServerPlayer(NetPlayer player)
        {
            Player = player;
        }

        internal static BetterServerPlayer GetPlayer(NetPlayer player)
        {
            PersistentPlayerID id = ManNetworkLobby.inst.LobbySystem.GetPersistentPlayerID(player.GetPlayerIDInLobby());
            BetterServerPlayer found;
            if (players.TryGetValue(id, out found))
                return found;
            found = new BetterServerPlayer(player);
            players.Add(id, found);
            return found;
        }
        internal static int GetPlayerInfractionCount(NetPlayer player)
        {
            BetterServerPlayer BSP = GetPlayer(player);
            return BSP.InfractionCount;
        }
        /// <summary>
        /// Returns true if we should kick
        /// </summary>
        /// <param name="player"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        internal static bool GivePlayerInfractions(NetPlayer player, int amount)
        {
            BetterServerPlayer BSP = GetPlayer(player);
            BSP.InfractionCount += amount;
            if (BSP.InfractionCount + 1 > KickStartBetterServers.blockedInfo.maxInfractionLimit)
            {
                try
                {
                    KickStartBetterServers.SendChatServer("User " + player.name + " is 1 more infraction from a " +
                        (KickStartBetterServers.blockedInfo.AutoBlock ? "ban" : "kick") + ", better behave!");
                }
                catch { }
            }
            return BSP.InfractionCount > KickStartBetterServers.blockedInfo.maxInfractionLimit;
        }
        internal static void PurgePlayerCache()
        {
            players.Clear();
        }

    }
}
