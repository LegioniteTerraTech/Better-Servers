using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TerraTech.Network;

namespace Better_Servers
{
    public class BetterServerPlayer
    {
        [JsonIgnore]
        internal NetPlayer Player;
        /// <summary> Not accurate as they can change usernames.  They will still be blocked by Steam ID though. </summary>
        public string name;
        public int InfractionCount = 0;

        private static Dictionary<PersistentPlayerID,BetterServerPlayer> players = new Dictionary<PersistentPlayerID, BetterServerPlayer>();

        /// <summary>
        /// SERIALIZATION ONLY
        /// </summary>
        public BetterServerPlayer()
        { 
        }
        internal BetterServerPlayer(NetPlayer player)
        {
            Player = player;
            this.name = player.name;
        }

        internal static BetterServerPlayer GetPlayer(NetPlayer player)
        {
            PersistentPlayerID id = ManNetworkLobby.inst.LobbySystem.GetPersistentPlayerID(player.GetPlayerIDInLobby());
            BetterServerPlayer found;
            if (players.TryGetValue(id, out found))
            {
                found.name = player.name;
                return found;
            }
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
