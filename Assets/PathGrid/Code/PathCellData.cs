/*
______     _   _     _____      _     _ 
| ___ \   | | | |   |  __ \    (_)   | |
| |_/ /_ _| |_| |__ | |  \/_ __ _  __| |
|  __/ _` | __| '_ \| | __| '__| |/ _` |
| | | (_| | |_| | | | |_\ \ |  | | (_| |
\_|  \__,_|\__|_| |_|\____/_|  |_|\__,_|

*/
using UnityEngine;

namespace PathGrid
{
    [System.Serializable]
    public struct PathCellData
    {
        public bool isAssigned;

        public enum TileType
        {
            empty,
            straight,
            curve,
            threeway,
            crossway,
            deadEnd
        }

        [System.Serializable]
        public struct NeighboursLocation
        {
            public bool north;
            public bool west;
            public bool east;
            public bool south;
            public bool northWest;
            public bool northEast;
            public bool southWest;
            public bool southEast;
        }



        public TileType type;
        public NeighboursLocation location;
        public int neighbourCount;
        public float yRotation;
        public Vector2Int mapPosition;
        public GameObject tileObject;
        public PathPreset pathPreset;
    }
}