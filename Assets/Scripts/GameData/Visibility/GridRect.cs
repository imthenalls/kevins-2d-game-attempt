namespace Game.Core
{
    /// <summary>
    /// A rectangle on a whole-number XZ grid: origin cell (X, Z) and size (W, D) in cells. It is a
    /// value type with no UnityEngine dependency, so zone geometry can live in <c>Game.Data</c>.
    ///
    /// Unity setup: none (pure C#). Runtime API: <see cref="Contains"/> and the bounds properties.
    /// </summary>
    public readonly struct GridRect
    {
        public readonly int X;
        public readonly int Z;
        public readonly int W;
        public readonly int D;

        public GridRect(int x, int z, int w, int d)
        {
            X = x;
            Z = z;
            W = w;
            D = d;
        }

        /// <summary>Exclusive upper X bound (first cell outside the rectangle).</summary>
        public int MaxX => X + W;

        /// <summary>Exclusive upper Z bound (first cell outside the rectangle).</summary>
        public int MaxZ => Z + D;

        public bool Contains(int x, int z) => x >= X && x < X + W && z >= Z && z < Z + D;

        public override string ToString() => "(" + X + "," + Z + " " + W + "x" + D + ")";
    }
}
