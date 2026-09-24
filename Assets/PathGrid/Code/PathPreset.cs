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
        [CreateAssetMenu(menuName = "PathGrid / Path Preset")]
        public class PathPreset : ScriptableObject
        {
                public PathNetwork.CellType pathType = PathNetwork.CellType.none;
                
                public GameObject single;
                public GameObject straight;
                public GameObject curve;
                public GameObject threeway;
                public GameObject crossway;
                public GameObject deadEnd;

                public float yPosition; // normally terrain = 0, water = -0.6
                public float straightRotationOffset;
                public float curveRotationOffset;
                public float threewayRotationOffset;
                public float deadEndRotationOffset;
        }
}