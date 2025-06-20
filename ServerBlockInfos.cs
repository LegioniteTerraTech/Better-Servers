using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Better_Servers
{
    public class ServerBlockInfos
    {
        public bool AutoBlock = true;       // Enough Infractions and we block as well as kick.
        public bool KickOnBlock = true;     // Kick immedeately on block
        public bool IWannaBeUnjust = false; // You agree to ignoring teh rules u made yerselfs
        public bool ShutUpKaren = false;    // Ignore errors and quite possibly frequent warnings

        // WORLD
        public int maxInfractionLimit = 6;
        public int maxTechLimit = 16;

        // PLAYER
        public int maxBlockLimit = 4096;
        public int BlockLimitInfractionSeverity = 1;
        public int maxBoltLimit = 5;
        public int BoltInfractionSeverity = 1;
        public int maxCabLimit = 12;
        public int CabInfractionSeverity = 1;

        // Stores Steam IDs that normally don't change with account name changes.  Does not block by IP.
        public Dictionary<string, PlayerHistory> userInfractionsHost = new Dictionary<string, PlayerHistory>();
    }

    public class PlayerHistory
    {
        string name;
        int infractionCount;
    }
}
