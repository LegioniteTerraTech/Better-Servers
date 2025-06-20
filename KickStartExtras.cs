using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Better_Servers
{
    internal class KickStartExtras
    {
        //public static OptionToggle enemyBaseCulling;
        public static void InitExtras()
        {
            /*
            enemyBaseCulling = new OptionToggle("<b>Enable Advanced NPTs</b>", TACAIEnemies, KickStart.enablePainMode);
            enemyBaseCulling.onValueSaved.AddListener(() => {
                if (KickStart.enablePainMode != enemyBaseCulling.SavedValue)
                {
                    foreach (var item in ManTechs.inst.IterateTechs())
                    {
                        if (item != null && item.IsEnemy())
                        {
                            TankAIHelper help = item.GetHelperInsured();
                            help.ForceRebuildAlignment();
                        }
                    }
                }
                KickStart.enablePainMode = enemyBaseCulling.SavedValue;
                //DebugRawTechSpawner.CanOpenDebugSpawnMenu = DebugRawTechSpawner.CheckValidMode();
            });
            */
        }
    }
}
