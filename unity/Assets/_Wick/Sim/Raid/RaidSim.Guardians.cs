namespace Wick.Sim
{
    // Step 4: guardians, sorted by entity id.
    public sealed partial class RaidSim
    {
        private void UpdateGuardians()
        {
            // Night Owls and Wax Moths arrive in the next commit.
        }

        /// <summary>Whether the guardian at object index i damages Wick on tile at this tick.</summary>
        private bool GuardianHits(int objectIndex, TilePos at)
        {
            return false;
        }
    }
}
