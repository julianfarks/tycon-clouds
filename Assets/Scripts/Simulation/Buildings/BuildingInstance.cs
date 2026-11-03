namespace CloudGarden.Buildings
{
    /// <summary>A building placed in the world.</summary>
    public sealed class BuildingInstance
    {
        public int Uid;
        public string DefId;
        public int Region;
        public int X;
        public int Y;
        public int Rotation; // 0..3
        public int Level = 1;
        public double PaidCoins;

        // Runtime only (not saved).
        public BuildingDef Def;
        public BuildingStatus Status = BuildingStatus.Idle;
        /// <summary>Fraction (0..1) of the maximum cycles actually executed on the last tick.</summary>
        public double Activity;

        public int FootprintWidth { get { return (Rotation & 1) == 0 ? Def.Width : Def.Depth; } }
        public int FootprintDepth { get { return (Rotation & 1) == 0 ? Def.Depth : Def.Width; } }
    }
}
