using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// Walls cost gold (M10, slowing progression): publishing pays for every wall tile that was open
    /// in the previously published version. The first publish is free (templates come with their
    /// walls), and so is the solid wall an expansion adds: after an expansion the two versions are
    /// lined up the way VaultDraft.Expand moved the layout (columns split either side, new rows on
    /// the side away from the entrance).
    /// </summary>
    public static class WallCost
    {
        public static int WallsAdded(VaultDef before, VaultDef after)
        {
            if (before == null)
            {
                return 0;
            }

            int dx = (after.Width - before.Width) / 2;
            int dy = after.Height != before.Height && before.Entrance.Y >= before.Height / 2 ? after.Height - before.Height : 0;
            int added = 0;
            for (int y = 0; y < after.Height; y++)
            {
                for (int x = 0; x < after.Width; x++)
                {
                    var was = new TilePos(x - dx, y - dy);
                    if (after.TileAt(new TilePos(x, y)) == TileType.Wall && before.InBounds(was) && before.TileAt(was) != TileType.Wall)
                    {
                        added++;
                    }
                }
            }

            return added;
        }
    }
}
