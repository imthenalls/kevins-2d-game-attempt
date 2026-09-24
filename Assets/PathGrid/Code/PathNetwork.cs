/*
______     _   _     _____      _     _ 
| ___ \   | | | |   |  __ \    (_)   | |
| |_/ /_ _| |_| |__ | |  \/_ __ _  __| |
|  __/ _` | __| '_ \| | __| '__| |/ _` |
| | | (_| | |_| | | | |_\ \ |  | | (_| |
\_|  \__,_|\__|_| |_|\____/_|  |_|\__,_|

*/

using System.Collections.Generic;
using UnityEngine;

namespace PathGrid
{
    /// <summary>
    /// A PathNetwork defines a "closed" path network which can have different path prefabs.
    /// Each PathNetwork also has a "global" grid which can be used to define the cell type (none, terrain, water, blocked).
    /// There's no grid size that must be defined as cells are being added dynamically.
    /// 
    /// </summary>
    public class PathNetwork : MonoBehaviour
    {
        public int gridCellSize = 1;

        public bool adaptToTerrainHeight;
        public float yOffset;
        public LayerMask terrainLayer;

        public enum CellType
        {
            none = 0,
            terrain = 1,
            water = 2,
            blocked = 3
        }

        [System.Serializable]
        public struct GridCellData
        {
            public CellType cellType;

            public GridCellData (CellType _cellType)
            {
                cellType = _cellType;
            }
        }

        private Dictionary<int, Dictionary<Vector2Int, PathCellData>> quadrantGridDictionary = new Dictionary<int, Dictionary<Vector2Int, PathCellData>>();
        private Dictionary<int, Dictionary<Vector2Int, GridCellData>> quadrantGlobalGridDictionary = new Dictionary<int, Dictionary<Vector2Int, GridCellData>>();

        private List<PathCellData> refreshCells = new List<PathCellData>();

        private int quadrantCellSize = 20;
        private int quadrantYMultiplier = 1000;
        

    #region PUBLIC_API
        public CellType GetGridCellTypeAtPosition(Vector3 _position)
        {
            int _hashMapKey = GetPositionHashMapKey(new Vector2(_position.x, _position.z));
            Vector2Int _cellPosition = GetMapPosition(new Vector2(_position.x, _position.z));
            
            if (quadrantGlobalGridDictionary.ContainsKey(_hashMapKey))
            {
                if (quadrantGlobalGridDictionary[_hashMapKey].ContainsKey(_cellPosition))
                {
                    var _data = quadrantGlobalGridDictionary[_hashMapKey][_cellPosition];

                    return _data.cellType;
                }
            }
            
            return CellType.none;
        }

        public void SetCellTypeAtPosition(Vector3 _position, CellType _cellType)
        {
            int _hashMapKey = GetPositionHashMapKey(new Vector2(_position.x, _position.z));
            Vector2Int _cellPosition = GetMapPosition(new Vector2(_position.x, _position.z));
            
            if (quadrantGlobalGridDictionary.ContainsKey(_hashMapKey))
            {
                if (quadrantGlobalGridDictionary[_hashMapKey].ContainsKey(_cellPosition))
                {
                    var _data = quadrantGlobalGridDictionary[_hashMapKey][_cellPosition];

                    _data.cellType = _cellType;
                }
                else
                {
                    quadrantGlobalGridDictionary[_hashMapKey].Add(_cellPosition, new GridCellData(_cellType));
                }
            }
            else
            {
                Dictionary<Vector2Int, GridCellData> _dictionary = new Dictionary<Vector2Int, GridCellData>();
                _dictionary.Add(_cellPosition, new GridCellData(_cellType));

                quadrantGlobalGridDictionary.Add(_hashMapKey, _dictionary);
            }
        }


        public PathCellData GetExistingPathCell(Vector3 _position)
        {
            int _hashMapKey = GetPositionHashMapKey(new Vector2(_position.x, _position.z));
            Vector2Int _cellPosition = GetMapPosition(new Vector2(_position.x, _position.z));

        
            if (quadrantGridDictionary.ContainsKey(_hashMapKey))
            {
                if (quadrantGridDictionary[_hashMapKey].ContainsKey(_cellPosition))
                {
                    var _data = quadrantGridDictionary[_hashMapKey][_cellPosition];

                    return _data;
                }
            }

            return default;
        }

        
        
        public void AddPathCells(List<Vector3> _positions, PathPreset _pathPresetData, System.Action _onCompleteCallback)
        {
            refreshCells = new List<PathCellData>();

            // Clean up existing tile objects on same position
            for (int i = 0; i < _positions.Count; i ++)
            {
                var _existingTile = GetExistingPathCell(new Vector3(_positions[i].x, 0, _positions[i].z));
                if (_existingTile.tileObject != null)
                {
                    Destroy(_existingTile.tileObject);
                }
            }

            for (int i = 0; i < _positions.Count; i ++)
            {
                int _hashMapKey = GetPositionHashMapKey(new Vector2(_positions[i].x, _positions[i].z));
                Vector2Int _cellPosition = GetMapPosition(new Vector2(_positions[i].x, _positions[i].z));
                PathCellData _newPathCell = new PathCellData()
                {
                    isAssigned = true,
                    mapPosition = _cellPosition,
                    pathPreset = _pathPresetData
                };

                if (quadrantGridDictionary.ContainsKey(_hashMapKey))
                {
                    if (quadrantGridDictionary[_hashMapKey].ContainsKey(_cellPosition))
                    {
                        quadrantGridDictionary[_hashMapKey][_cellPosition] = _newPathCell;
                    }
                    else
                    {
                        quadrantGridDictionary[_hashMapKey].Add(_cellPosition, _newPathCell);
                    }
                }
                else
                {
                    Dictionary<Vector2Int, PathCellData> _dictionary = new Dictionary<Vector2Int, PathCellData>();
                    _dictionary.Add(_cellPosition, _newPathCell);

                    quadrantGridDictionary.Add(_hashMapKey, _dictionary);
                }

                refreshCells.Add(_newPathCell);
            }   

            // Update existing neighbouring path cells
            List<PathCellData> neighbouringPathTiles = new List<PathCellData>();
            for (int i = 0; i < _positions.Count; i++)
            {
                // check neighbouring tiles
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        if (x != 0 || y != 0)
                        {
                            var _neighbouringTile = GetExistingPathCell(new Vector3(_positions[i].x + (x * gridCellSize), 0, _positions[i].z + (y * gridCellSize)));
                            if (_neighbouringTile.isAssigned) // != ) // != null)
                            {
                                if (!refreshCells.Contains(_neighbouringTile))
                                {
                                    neighbouringPathTiles.Add(_neighbouringTile);                                  
                                }
                            }
                        }
                    }
                }
            }

            neighbouringPathTiles = RefreshPathCells(neighbouringPathTiles);

            for (int i = 0; i < neighbouringPathTiles.Count;  i++)
            {
                if (neighbouringPathTiles[i].tileObject != null)
                {
                    Destroy(neighbouringPathTiles[i].tileObject);
                }

                InstantiateSinglePathTile(neighbouringPathTiles[i]);
            }
    

            // Refresh and update new path tiles
            refreshCells = RefreshPathCells(refreshCells);


            // Instantiate new path tiles
            for (int r = 0; r < refreshCells.Count; r ++)
            {
                InstantiateSinglePathTile(refreshCells[r]);
            }

        }

        public void RemovePathCells(List<Vector3> _positions)
        {
            for (int i = 0; i < _positions.Count; i ++)
            {
                var _existingTile = GetExistingPathCell(new Vector3(_positions[i].x, 0, _positions[i].z));
                if (_existingTile.isAssigned) // != null)
                {
                    _existingTile.pathPreset = null;
                    _existingTile.isAssigned = false;
                    Destroy(_existingTile.tileObject);
                }
            }


            for (int i = 0; i < _positions.Count; i ++)
            {
                int _hashMapKey = GetPositionHashMapKey(new Vector2(_positions[i].x, _positions[i].z));
                Vector2Int _cellPosition = GetMapPosition(new Vector2(_positions[i].x, _positions[i].z));

                if (quadrantGridDictionary.ContainsKey(_hashMapKey))
                {
                    if (quadrantGridDictionary[_hashMapKey].ContainsKey(_cellPosition))
                    {
                        quadrantGridDictionary[_hashMapKey].Remove(_cellPosition);
                    }
                }
            }

            List<PathCellData> neighbouringPathCells = new List<PathCellData>();
            for (int i = 0; i < _positions.Count; i++)
            {
                // check neighbouring tiles
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        if (x != 0 || y != 0)
                        {
                            var _neighbouringTile = GetExistingPathCell(new Vector3(_positions[i].x + (x * gridCellSize), 0, _positions[i].z + (y * gridCellSize)));
                            if (_neighbouringTile.isAssigned) // != null)
                            {
                                if (!neighbouringPathCells.Contains(_neighbouringTile))
                                {
                                    neighbouringPathCells.Add(_neighbouringTile);
                                } 
                            }
                        }
                    }
                }
            }

            neighbouringPathCells = RefreshPathCells(neighbouringPathCells);
            // Update neighbouring tiles
            for (int i = 0; i < neighbouringPathCells.Count;  i++)
            {
                if (neighbouringPathCells[i].pathPreset != null)
                {
                    InstantiateSinglePathTile(neighbouringPathCells[i]);
                }
            }

        }
    #endregion

    #region PRIVATE_METHODS

        private int GetPositionHashMapKey(Vector2 _position)
        {
            return (int)(Mathf.Floor(_position.x / quadrantCellSize) + (quadrantYMultiplier * Mathf.Floor(_position.y / quadrantCellSize)));
        }

        // Simply convert to int vector2
        private Vector2Int GetMapPosition(Vector2 _position)
        {
            return new Vector2Int((int)_position.x, (int)_position.y);
        }

        private void InstantiateSinglePathTile(PathCellData _pathCellData)
        {
            var _offset = 0.0f;
            GameObject _prefab = null;
            float _rotationOffset = 0;

            var _existingTile = GetExistingPathCell(new Vector3(_pathCellData.mapPosition.x, 0, _pathCellData.mapPosition.y));
            
            if (_existingTile.isAssigned) //!= null)
            {
                Destroy(_existingTile.tileObject);
            }

            switch (_pathCellData.type)
            {
                case PathCellData.TileType.deadEnd:
                    _prefab = _pathCellData.pathPreset.deadEnd;
                    _rotationOffset = _pathCellData.pathPreset.deadEndRotationOffset;
                    break;
                case PathCellData.TileType.empty:
                    _prefab = _pathCellData.pathPreset.single;
                    break;
                case PathCellData.TileType.straight:
                    _prefab = _pathCellData.pathPreset.straight;
                    _rotationOffset = _pathCellData.pathPreset.straightRotationOffset;
                    break;
                case PathCellData.TileType.curve:
                    _prefab = _pathCellData.pathPreset.curve;
                    _rotationOffset = _pathCellData.pathPreset.curveRotationOffset;
                    break;
                case PathCellData.TileType.threeway:
                    _prefab = _pathCellData.pathPreset.threeway;
                    _rotationOffset = _pathCellData.pathPreset.threewayRotationOffset;
                    break;
                case PathCellData.TileType.crossway:
                    _prefab = _pathCellData.pathPreset.crossway;
                    break;
            }

            
            var _newTile = Instantiate
            (
                _prefab, new Vector3(_pathCellData.mapPosition.x + _offset, 
                _pathCellData.pathPreset.yPosition + yOffset, 
                _pathCellData.mapPosition.y + _offset),                         
                Quaternion.Euler(new Vector3(0, _pathCellData.yRotation + _rotationOffset, 0))
            );

            _newTile.transform.SetParent(this.transform, false);

            _pathCellData.tileObject = _newTile;

            if (adaptToTerrainHeight)
            {
                DeformMesh(_newTile);
            }

            AddPathCellToDictionary(_pathCellData);
        }

        private void AddPathCellToDictionary (PathCellData _pathCellData)
        {
            int _hashMapKey = GetPositionHashMapKey(new Vector2(_pathCellData.mapPosition.x, _pathCellData.mapPosition.y));
            
            
            if (quadrantGridDictionary.ContainsKey(_hashMapKey))
            {
                if (quadrantGridDictionary[_hashMapKey].ContainsKey(_pathCellData.mapPosition))
                {
                    quadrantGridDictionary[_hashMapKey][_pathCellData.mapPosition] = _pathCellData;
                }
            }
        }

      

        // Refresh all path tiles which must be refreshed
        private List<PathCellData> RefreshPathCells(List<PathCellData> _refreshPathCells)
        {
            for (int i = 0; i < _refreshPathCells.Count; i++)
            {
                var _cell = _refreshPathCells[i];

                var _neighbourCount = 0;
                _cell.location.south = false;
                _cell.location.southWest = false;
                _cell.location.southEast = false;
                _cell.location.west = false;
                _cell.location.east = false;
                _cell.location.northWest = false;
                _cell.location.north = false;
                _cell.location.northEast = false;

                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        if (x != 0 || y != 0)
                        {
                            var _neighbour = GetExistingPathCell(new Vector3(_cell.mapPosition.x + (x * gridCellSize), 0, _cell.mapPosition.y + (y * gridCellSize)));
                            if (_neighbour.isAssigned) // != null)
                            {

                                _neighbourCount++;

                                // Set neighbour locations
                                // South West
                                if (x == -1 && y == -1)
                                {
                                    _cell.location.southWest = true;
                                }
                                // South
                                if (x == 0 && y == -1)
                                {
                                    _cell.location.south = true;
                                }
                                // South East
                                if (x == 1 && y == -1)
                                {
                                    _cell.location.southEast = true;
                                }
                                // West
                                if (x == -1 && y == 0)
                                {
                                    _cell.location.west = true;
                                }
                                // East
                                if (x == 1 && y == 0)
                                {
                                    _cell.location.east = true;
                                }
                                // North West
                                if (x == -1 && y == 1)
                                {
                                    _cell.location.northWest = true;
                                }
                                // North
                                if (x == 0 && y == 1)
                                {
                                    _cell.location.north = true;
                                }
                                // North East
                                if (x == 1 && y == 1)
                                {
                                    _cell.location.northEast = true;
                                }
                            }

                        }
                    }
                }

                _cell = SetTileRotation(_cell);

                /*
                NONE
                !north && !_south && !_west && !_east

                Dead end
                _north && !_south && !_west && !_east
                !_north && _south && !_west && !_east
                !_north && !_south && _west && !_east
                !_north && !_south && !_west && _east

                Straight
                _north && _south
                _east && _west

                Corner
                _north && _east
                _north && _west
                _south && _east
                _south && _west

                Threeway
                _north && _south && _east
                _north && _south && _west
                _south && _east && _west
                _north && _west && _east

                Crossway
                _north && _south && _west && _east

                */

                

                if (!_cell.location.north &&
                    !_cell.location.south &&
                    !_cell.location.west &&
                    !_cell.location.east)
                {
                    _cell.type = PathCellData.TileType.empty;
                }
                // Dead end
                if ((_cell.location.north &&
                    !_cell.location.south &&
                    !_cell.location.west &&
                    !_cell.location.east) ||
                    (!_cell.location.north &&
                    _cell.location.south &&
                    !_cell.location.west &&
                    !_cell.location.east) ||
                    (!_cell.location.north &&
                    !_cell.location.south &&
                    _cell.location.west &&
                    !_cell.location.east) ||
                    (!_cell.location.north &&
                    !_cell.location.south &&
                    !_cell.location.west &&
                    _cell.location.east))
                {
                    _cell.type = PathCellData.TileType.deadEnd;
                }
                // Straight
                if ((_cell.location.north && _cell.location.south) ||
                    (_cell.location.east && _cell.location.west))
                {
                    _cell.type = PathCellData.TileType.straight;
                }
                // corner
                if ((_cell.location.north && _cell.location.east) ||
                    (_cell.location.north && _cell.location.west) ||
                    (_cell.location.south && _cell.location.east) ||
                    (_cell.location.south && _cell.location.west))
                {
                    _cell.type = PathCellData.TileType.curve;
                }
                // Threeway
                if ((_cell.location.north && _cell.location.south && _cell.location.east) ||
                    (_cell.location.north && _cell.location.south && _cell.location.west) ||
                    (_cell.location.south && _cell.location.east && _cell.location.west) ||
                    (_cell.location.north && _cell.location.west && _cell.location.east))
                {
                    _cell.type = PathCellData.TileType.threeway;
                }
                // Crossway
                if (_cell.location.north && _cell.location.south && _cell.location.west && _cell.location.east)
                {
                    _cell.type = PathCellData.TileType.crossway;
                }

                _refreshPathCells[i] = _cell;
            }

        

            return _refreshPathCells;   
        }

        private PathCellData SetTileRotation(PathCellData _cellData)
        {
            float _yRotation = 0f;

            // Dead end
            if (_cellData.location.north &&
                !_cellData.location.south &&
                !_cellData.location.east &&
                !_cellData.location.west)
            {
                _yRotation = 0;
            }
            else if (!_cellData.location.north &&
                _cellData.location.south &&
                !_cellData.location.east &&
                !_cellData.location.west)
            {
                _yRotation = 180f;
            }
            else if (!_cellData.location.north &&
                !_cellData.location.south &&
                _cellData.location.east &&
                !_cellData.location.west)
            {
                _yRotation = 90;
            }
            else if (!_cellData.location.north &&
                !_cellData.location.south &&
                !_cellData.location.east &&
                _cellData.location.west)
            {
                _yRotation = -90;
            }
            // Straight
            else if (!_cellData.location.north &&
                !_cellData.location.south &&
                _cellData.location.east &&
                _cellData.location.west)
            {
                _yRotation = 90;
            }
            else if (_cellData.location.north &&
                _cellData.location.south &&
                !_cellData.location.east &&
                !_cellData.location.west)
            {
                _yRotation = 0f;
            }
            // Corner
            else if (_cellData.location.north &&
                _cellData.location.west &&
                !_cellData.location.south &&
                !_cellData.location.east)
            {
                _yRotation = 0;
            }
            else if (_cellData.location.north &&
                !_cellData.location.west &&
                !_cellData.location.south &&
                _cellData.location.east)
            {
                _yRotation = 90;
            }
            else if (!_cellData.location.north &&
                !_cellData.location.west &&
                _cellData.location.south &&
                _cellData.location.east)
            {
                _yRotation = 180;
            }
            else if (!_cellData.location.north &&
                _cellData.location.west &&
                _cellData.location.south &&
                !_cellData.location.east)
            {
                _yRotation = -90;
            }
            // Threeway
            else if (!_cellData.location.north &&
                _cellData.location.west &&
                _cellData.location.south &&
                _cellData.location.east)
            {
                _yRotation = -90;
            }
            else if (_cellData.location.north &&
                !_cellData.location.west &&
                _cellData.location.south &&
                _cellData.location.east)
            {
                _yRotation = 180;
            }
            else if (_cellData.location.north &&
                _cellData.location.west &&
                !_cellData.location.south &&
                _cellData.location.east)
            {
                _yRotation = 90;
            }
            else if (_cellData.location.north &&
                _cellData.location.west &&
                _cellData.location.south &&
                !_cellData.location.east)
            {
                _yRotation = 0;
            }

            _cellData.yRotation = _yRotation;

            return _cellData;
        }

        private void DeformMesh(GameObject _tile)
        {
            MeshFilter _meshFilter = _tile.GetComponent<MeshFilter>();
            var _mesh = _meshFilter.mesh;
            var _vertices = _mesh.vertices;
            var _originalVertices = _vertices;
            RaycastHit raycastHit;

            if (!_mesh.isReadable)
            {
                Debug.LogWarning("Mesh Read/Write in the import settings is not enabled, please enable it");
                return;
            }

            for (int i = 0; i < _vertices.Length; i ++)
            {
                var _endPoint = _tile.transform.TransformPoint(_vertices[i]);
                var _startPoint = new Vector3 (_endPoint.x, 50, _endPoint.z);

                if (Physics.Raycast(_startPoint, _tile.transform.TransformDirection(- Vector3.up), out raycastHit, 100, terrainLayer))
                {
                    var _localPosition = _tile.transform.InverseTransformPoint(raycastHit.point);
                    _vertices[i] = new Vector3(_vertices[i].x, _originalVertices[i].y + _localPosition.y + yOffset, _vertices[i].z);
                }
            }

            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
        }


        public void DebugDrawQuadrant(Vector3 _position)
        {
            Vector3 _lowerLeft = new Vector3(Mathf.Floor(_position.x / quadrantCellSize) * quadrantCellSize, 0, Mathf.Floor(_position.z / quadrantCellSize) * quadrantCellSize);
            Debug.DrawLine(_lowerLeft, _lowerLeft + new Vector3(1, 0, 0) * quadrantCellSize);
            Debug.DrawLine(_lowerLeft, _lowerLeft + new Vector3(0, 0, 1) * quadrantCellSize);
            Debug.DrawLine(_lowerLeft + new Vector3(1, 0, 0) * quadrantCellSize, _lowerLeft + new Vector3(1, 0, 1) * quadrantCellSize);
            Debug.DrawLine(_lowerLeft + new Vector3(0, 0, 1) * quadrantCellSize, _lowerLeft + new Vector3(1, 0, 1) * quadrantCellSize);
        }
    #endregion
    }
}