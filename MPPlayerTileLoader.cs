using System;
using System.Collections.Generic;
using TerraTechETCUtil;
using UnityEngine;

namespace Better_Servers
{
    public class MPPlayerTileLoader : ITileLoader
    {
        internal readonly NetPlayer player;
        internal WorldPosition lastTileWP { get; private set; }
        public MPPlayerTileLoader(NetPlayer setPlayer, IntVector2 lastTile)
        {
            player = setPlayer;
            float tileCenter = ManWorld.inst.TileSize / 2f;
            lastTileWP = new WorldPosition(lastTile, new Vector3(tileCenter, 0, tileCenter));
        }
        public bool Valid() => lastTileWP.TileCoord != IntVector2.invalid;//player != null;
        public void GetActiveTiles(List<IntVector2> tiles)
        {
            if (player?.CurTech?.tech?.visible != null)
                lastTileWP = WorldPosition.FromScenePosition(player.CurTech.tech.boundsCentreWorldNoCheck);
            ManWorldTileExt.GetActiveTilesAround(tiles, lastTileWP, MPKingdomsTest.PlayerTileLoadDistOuter);
        }
    }
}
