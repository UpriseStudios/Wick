namespace Wick.Sim
{
    /// <summary>Tile byte values as stored in the vault JSON "tiles" field.</summary>
    public enum TileType : byte
    {
        Floor = 0,
        Wall = 1,
        Door = 2,
        Pit = 3,
        Ice = 4,
    }
}
