using System.Collections.Generic;
using UnityEngine;


namespace PathGrid.Example
{
    /// <summary>
    /// This is a very simple example script to show how easy it is to add/generate a path based on a list of positions.
    /// </summary>
    public class Demo_00 : MonoBehaviour
    {
        public PathGridSystem pathGridSystem;

        public List<Vector3> path1 = new List<Vector3>();
        public List<Vector3> path2 = new List<Vector3>();
        
        List<Vector3> lastPath = new List<Vector3>();
        int selected = 0;
        public void GeneratePath()
        {
            // switch between both paths
            if (selected == 0)
            {
                selected = 1;
                // Remove last created path
                pathGridSystem.RemovePathTiles(lastPath);
                lastPath = path1;

                // Create new path
                pathGridSystem.AddPathTiles(path1);
            }
            else
            {
                selected = 0;
                pathGridSystem.RemovePathTiles(lastPath);
                lastPath = path2;
                pathGridSystem.AddPathTiles(path2);
            }
        }
    }
}