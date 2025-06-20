using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Better_Servers
{
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
    /// <summary>
    /// NOT STARTED DEVELOPMENT YET
    /// Manages Kingdoms teams for multiple teams on the same server with their own BB and resources
    /// </summary>
    public class ManMPTeams
    {
    }
}
