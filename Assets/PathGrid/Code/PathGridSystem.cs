/*
______     _   _     _____      _     _ 
| ___ \   | | | |   |  __ \    (_)   | |
| |_/ /_ _| |_| |__ | |  \/_ __ _  __| |
|  __/ _` | __| '_ \| | __| '__| |/ _` |
| | | (_| | |_| | | | |_\ \ |  | | (_| |
\_|  \__,_|\__|_| |_|\____/_|  |_|\__,_|

*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PathGrid
{
    /// <summary>
    /// The PathGridSystem acts as "bridge" between selected PathNetwork and the player input (PathGridInputSystem).
    /// It contains all available PathNetwork as well as PathPresets.
    /// </summary>
    public class PathGridSystem : MonoBehaviour
    {

#region PUBLIC_FIELDS

        /// <summary>
        /// Get the selected path network index
        /// </summary>
        public int pathNetworkIndex
        {
            get
            {
                return _pathNetworkIndex;
            }
        }

        /// <summary>
        /// Get selected path preset index
        /// </summary>
        public int pathPresetIndex
        {
            get
            {
                return _pathPresetIndex;
            }
        }

        /// <summary>
        /// Assign all available path networks
        /// </summary>
        public List<PathNetwork> pathNetworks;

        /// <summary>
        /// Assign all available path presets
        /// </summary>
        public List<PathPreset> pathPresets = new List<PathPreset>();

#endregion



#region PRIVATE_FIELDS
        private int _pathNetworkIndex;
        private int _pathPresetIndex;
        private Action onCompleteCallback;
#endregion


#region PUBLIC_METHODS
        /// <summary>
        /// Returns current selected path network
        /// </summary>
        /// <returns></returns>
        public PathNetwork GetCurrentPathNetwork()
        {
            return pathNetworks[_pathNetworkIndex];
        }
        
        /// <summary>
        /// Returns current selected path preset
        /// </summary>
        /// <returns></returns>
        public PathPreset GetCurrentPathPreset()
        {
            return pathPresets[_pathPresetIndex];
        }

        public int GetCurrentPathPresetIndex()
        {
            return _pathPresetIndex;
        }

        /// <summary>
        /// Set new path network
        /// </summary>
        /// <param name="_index"></param>
        public void SetPathNetworkIndex(int _index)
        {
            _pathNetworkIndex = _index;
        }

        /// <summary>
        /// Set new path preset
        /// </summary>
        /// <param name="_index"></param>
        public void SetPathPresetIndex(int _index)
        {
            _pathPresetIndex = _index;
        }

        /// <summary>
        /// Add new path tiles at positions
        /// </summary>
        /// <param name="_positions"></param>
        /// <param name="_onCompleteCallback"></param>
        public void AddPathTiles(List<Vector3> _positions, Action _onCompleteCallback = null)
        {
            onCompleteCallback = _onCompleteCallback;

            AddTiles(_positions);
        }

        /// <summary>
        /// Add new path tiles at positions with selected network and path preset
        /// </summary>
        /// <param name="_positions"></param>
        /// <param name="_pathNetworkIndex"></param>
        /// <param name="_pathPresetIndex"></param>
        /// <param name="_onCompleteCallback"></param>
        public void AddPathTiles(List<Vector3> _positions, int _pathNetworkIndex, int _pathPresetIndex, Action _onCompleteCallback = null)
        {
            SetPathNetworkIndex( _pathNetworkIndex );
            SetPathPresetIndex( _pathPresetIndex);

            onCompleteCallback = _onCompleteCallback;

            AddTiles(_positions);
        }


        /// <summary>
        /// Remove path tiles at positions
        /// </summary>
        /// <param name="_positions"></param>
        public void RemovePathTiles(List<Vector3> _positions)
        {
            RemoveTiles(_positions);
        }

        /// <summary>
        /// Remove path tiles on path network
        /// </summary>
        /// <param name="_positions"></param>
        /// <param name="_pathNetworkIndex"></param>
        public void RemovePathTiles(List<Vector3> _positions, int _pathNetworkIndex)
        {
            SetPathNetworkIndex( _pathNetworkIndex );

            RemoveTiles(_positions);
        }
#endregion


#region PRIVATE_METHODS
        private void AddTiles(List<Vector3> _positions)
        {
            // remove duplicated positions
            var _cleanedPositions = _positions.Distinct().ToList();
            pathNetworks[_pathNetworkIndex].AddPathCells(_cleanedPositions, pathPresets[_pathPresetIndex], onCompleteCallback);
        }

        private void RemoveTiles(List<Vector3> _positions)
        {
            // remove duplicated positions
            var _cleanedPositions = _positions.Distinct().ToList();
            pathNetworks[_pathNetworkIndex].RemovePathCells(_cleanedPositions);
        }
#endregion


    }
}