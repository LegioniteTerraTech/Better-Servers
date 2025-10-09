using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Better_Servers
{   // This is for separate teams in campaign.  Unlikely to ever be finished.
    /*
     * Here's how it works:
     *   Every client and the server gets a copy of all of the teams data
     *   Each client has their own respective team data assigned to their "main" information which the game reads off of
     *   This somehow has to manage missions for EACH SEPERATE TEAM (impossible due to U-Script?)
     *   Team data is updated infrequently by host
     * 
     * 
     * 
     */
    /// <summary>
    /// NOT STARTED DEVELOPMENT YET
    /// Manages Kingdoms teams for multiple teams on the same server with their own BB and resources
    /// </summary>
    public class ManMPTeams
    {
    }

    public abstract class MPTeamSaveData
    {
        public int BuildBucks { get; }

        public IInventory<BlockTypes> Inventory { get; }
    }
    public class MPTeamSaveDataHost : MPTeamSaveData
    {
        public int BuildBucks { get => ManPlayer.inst.GetCurrentMoney(); }

        public IInventory<BlockTypes> Inventory { get => ManPurchases.inst.GetInventory(); }
    }
    public class MPTeamSaveDataNonHost : MPTeamSaveData
    {
        public int BuildBucks { get => buildBucks; }
        public int buildBucks = 0;

        public IInventory<BlockTypes> Inventory { get => inventory; }
        public IInventory<BlockTypes> inventory;
    }
}
