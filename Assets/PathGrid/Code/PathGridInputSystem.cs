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
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PathGrid.Input
{
    /// <summary>
    /// The PathGridInputSystem can be seen as an example on how to collect path cells positions from the users
    /// mouse position and then passing them on to the PathGridSystem (AddPathCells).
    /// 
    /// // What this script do:
    /// 1. When system is enabled, we wait for the users first click (clickCount)
    /// 
    /// 2. When user has clicked once we start previewing the mouse grid position by instantiating preview blocks.(PreviewPaths)
    /// 
    /// 2a. The directionLockDistance defines how many cells we should "lock" a direction for the path preview (+x,-x,+y,-y). 
    /// Every further movement of the mouse is then being previewed in perpendicular direction. like an "L"
    /// 
    /// 2b. If checkForCellTypesOnPathNetwork is enabled, we further check if the currently selected path prefabs cell type matches the path networks
    /// cell type. A path network cell type can be defined by using: SetCellTypeAtPosition. Following types can be set:
    ///     * None
    ///     * Terrain
    ///     * Water
    ///     * Blocked
    ///     
    /// 3. When user clicks the second time we clear the preview blocks and send the collected positions to the PathGridSystem which takes
    /// care of building the path.
    /// 
    /// 4. Right mouse click aborts the preview and resets the click count.
    /// </summary>
    public class PathGridInputSystem : MonoBehaviour
    {

    #region PUBLIC_FIELDS
    
        public PathGridSystem pathGridSystem;
        public Camera mainCamera;
        public GameObject pointer;
        public GameObject pathVisualizationPrefabFailed;
        public GameObject pathVisualizationPrefabSuccess;
        
#if ENABLE_INPUT_SYSTEM
        public InputActionReference mouseLeftClickInput;
        public InputActionReference mouseRightClickInput;
#endif
        
        public enum GenerationType
        {
            add,
            remove
        }

        public GenerationType generationType;
        public LayerMask terrainLayer;
        public bool checkForCellTypesOnPathNetwork;
        public int directionLockDistance = 4;

        private bool _isInputSystemEnabled;
        public bool isInputSystemEnabled
        {
            get 
            {
                return _isInputSystemEnabled;
            }
        }

    #endregion

    #region PRIVATE_FIELDS
        private Vector2Int startPosition;
        private Vector2Int lastStartPosition;
        private Vector2Int lastCurrentPosition;
        private Vector3 mousePosition;
        private Vector2Int currentMouseGridPosition;

        private bool directionCheck;
        private bool startWithX;
        private int clickCount = 0;

        private List<Vector3> pathCellsPositions = new List<Vector3>();
        private List<GameObject> previewBlocks = new List<GameObject>();
        private PathNetwork currentPathNetwork;
        private PathPreset currentPathPresetData;

    #endregion

    #region PUBLIC_API

        /// <summary>
        /// Enable the RoadGrid Input system
        /// </summary>
        public void EnableInputSystem()
        {
            clickCount = 0;
            _isInputSystemEnabled = true;
            pointer.SetActive(true);
            // default is add
            SetToAdd();
        }

        /// <summary>
        /// Disable the RoadGrid Input system
        /// </summary>
        public void DisableInputSystem()
        {
            _isInputSystemEnabled = false;
            clickCount = 0;
            pointer.SetActive(false);
            ClearPreviewBlocks();
        }

        /// <summary>
        /// Return current mouse grid position
        /// </summary>
        /// <returns></returns>
        public Vector2Int GetMouseGridPosition()
        {
            return currentMouseGridPosition;
        }

        /// <summary>
        /// Set road generation type (Add or Remove)
        /// </summary>
        /// <param name="_type"></param>
        public void SetGenerationType(GenerationType _type)
        {
            generationType = _type;
        }

        public void SetToAdd()
        {
            generationType = GenerationType.add;
        }

        public void SetToRemove()
        {
            generationType = GenerationType.remove;
        }

    #endregion

    #region PRIVATE_METHODS

        void Awake()
        {
            #if ENABLE_INPUT_SYSTEM
            if (mouseLeftClickInput == null || mouseRightClickInput == null)
                return;
            
            mouseLeftClickInput.action.started += MouseLeftButtonStarted;
            mouseRightClickInput.action.started += MouseRightButtonStarted;
            #endif
        }

        void Start()
        {
            if (pathGridSystem == null)
            {
                pathGridSystem = GameObject.FindObjectOfType(typeof(PathGridSystem)) as PathGridSystem;
            }
            currentPathNetwork = pathGridSystem.GetCurrentPathNetwork();
            currentPathPresetData = pathGridSystem.GetCurrentPathPreset();
        }

        void OnEnable()
        {
            #if ENABLE_INPUT_SYSTEM
            if (mouseLeftClickInput == null || mouseRightClickInput == null)
                return;
                
            mouseLeftClickInput.action.Enable();
            mouseRightClickInput.action.Enable();
            #endif
        }

        void OnDisable()
        {
            #if ENABLE_INPUT_SYSTEM
            if (mouseLeftClickInput == null || mouseRightClickInput == null)
                return;

            mouseLeftClickInput.action.started -= MouseLeftButtonStarted;
            mouseRightClickInput.action.started -= MouseRightButtonStarted;
            #endif
        }

        #if ENABLE_INPUT_SYSTEM
        void MouseLeftButtonStarted(InputAction.CallbackContext context)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!_isInputSystemEnabled)
                return;

            clickCount ++;
            startPosition = currentMouseGridPosition;
        }

        void MouseRightButtonStarted(InputAction.CallbackContext context)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!_isInputSystemEnabled)
                return;

            clickCount = 0;
            ClearPreviewBlocks();
            DisableInputSystem();
        }
        #endif


        void Update()
        {
            if (!_isInputSystemEnabled)
                return;

            CalculateMouseGridPosition();   

            #if !ENABLE_INPUT_SYSTEM
            LegacyInputHandling();
            #endif

            if (clickCount == 1)
            {
                PreviewPaths();   
            }
            if (clickCount == 2)
            {
                switch(generationType)
                {
                    case GenerationType.add:
                        AddPathCells();
                        break;
                    case GenerationType.remove:
                        RemovePathCells();
                        break;
                }

                ClearPreviewBlocks();
                clickCount = 0;
            } 
        }

        void LegacyInputHandling()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!_isInputSystemEnabled)
                return;

            // left click, legacy input system
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                clickCount ++;
                startPosition = currentMouseGridPosition;
            }

            // right click
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                clickCount = 0;
                ClearPreviewBlocks();
                DisableInputSystem();
            }
        }

        void AddPathCells()
        {
            pathGridSystem.AddPathTiles(pathCellsPositions);
        }

        void RemovePathCells()
        {
            pathGridSystem.RemovePathTiles(pathCellsPositions);
        }


        void CalculateMouseGridPosition()
        {
            #if ENABLE_INPUT_SYSTEM
            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            #else
            var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            #endif
            RaycastHit hit;
        
            if (Physics.Raycast(ray, out hit, 1000, terrainLayer))
            {
                mousePosition = new Vector3(hit.point.x, hit.point.y, hit.point.z);
            }

            Vector3 pos = mousePosition;
            pos.x = Mathf.RoundToInt(pos.x / currentPathNetwork.gridCellSize) * currentPathNetwork.gridCellSize;
            pos.z = Mathf.RoundToInt(pos.z / currentPathNetwork.gridCellSize) * currentPathNetwork.gridCellSize;
            currentMouseGridPosition = new Vector2Int ((int)pos.x, (int)pos.z);

            // Only for debug purpose
            if (currentPathNetwork != null)
            {
                currentPathNetwork.DebugDrawQuadrant(mousePosition);
            }

            pointer.transform.position = new Vector3(currentMouseGridPosition.x, mousePosition.y, currentMouseGridPosition.y);
        }

    #region PREVIEW_PATHS
        void PreviewPaths()
        {

            var _xCount = currentMouseGridPosition.x - startPosition.x;
            var _yCount = currentMouseGridPosition.y - startPosition.y;

            // Make sure we always have the correct road network and road prefabs
            currentPathNetwork = pathGridSystem.GetCurrentPathNetwork();
            currentPathPresetData = pathGridSystem.GetCurrentPathPreset();

            if (currentMouseGridPosition.x != lastCurrentPosition.x || currentMouseGridPosition.y != lastCurrentPosition.y)
            {
                if (directionCheck)
                {
                    if (Mathf.Abs(_xCount) > directionLockDistance)
                    {
                        directionCheck = false;
                        startWithX = true;
                    }
                    if (Mathf.Abs(_yCount) > directionLockDistance)
                    {
                        directionCheck = false;
                        startWithX = false;
                    }
                }
                else
                {
                    var _t = new Vector2 (currentMouseGridPosition.x, currentMouseGridPosition.y);
                    var _s = new Vector2(startPosition.x, startPosition.y);
                    var _dist = Vector2.Distance(_t, _s);
                    if (_dist < directionLockDistance)
                    {
                        directionCheck = true;
                    }
                }

                if (startWithX)
                {
                    ClearPreviewBlocks();
                    var _nextPosition = IterateX(startPosition);
                    IterateY (_nextPosition);
                }
                else
                {
                    ClearPreviewBlocks();
                    var _nextPosition = IterateY(startPosition);
                    IterateX(_nextPosition);
                }

                lastStartPosition = startPosition;
                lastCurrentPosition = currentMouseGridPosition;
            }
        }

        Vector2Int IterateX(Vector2Int _startPosition)
        {
            if (currentMouseGridPosition.x - _startPosition.x >= 0)
            {
                for (int x = _startPosition.x; x <= currentMouseGridPosition.x; x += currentPathNetwork.gridCellSize)
                {

                    bool _isBlocked = false;
                    if (checkForCellTypesOnPathNetwork)
                    {
                        var _cellType = currentPathNetwork.GetGridCellTypeAtPosition(new Vector3(x, 0, _startPosition.y));
                        if (_cellType == PathNetwork.CellType.blocked || _cellType != currentPathPresetData.pathType)
                        {
                            _isBlocked = true;
                        }
                    }

                    Quaternion rotation = Quaternion.identity;
                    var _tmp = Instantiate(_isBlocked || generationType == GenerationType.remove ? pathVisualizationPrefabFailed : pathVisualizationPrefabSuccess, new Vector3(x, GetHeightPosition(x, _startPosition.y, out rotation), _startPosition.y), Quaternion.identity);
                    _tmp.transform.rotation = rotation;

                    if (!_isBlocked || generationType == GenerationType.remove)
                    {
                        pathCellsPositions.Add(_tmp.transform.position);
                    }
                    
                    previewBlocks.Add(_tmp);
                }
            }
            else
            {
                for (int x = _startPosition.x; x >= currentMouseGridPosition.x; x -= currentPathNetwork.gridCellSize)
                {
                    bool _isBlocked = false;
                    if (checkForCellTypesOnPathNetwork)
                    {
                        var _cellType = currentPathNetwork.GetGridCellTypeAtPosition(new Vector3(x, 0, _startPosition.y));
                        if (_cellType == PathNetwork.CellType.blocked || _cellType != currentPathPresetData.pathType)
                        {
                            _isBlocked = true;
                        }
                    }

                    Quaternion rotation = Quaternion.identity;
                    var _tmp = Instantiate(_isBlocked || generationType == GenerationType.remove ? pathVisualizationPrefabFailed : pathVisualizationPrefabSuccess, new Vector3(x, GetHeightPosition(x, _startPosition.y, out rotation), _startPosition.y), Quaternion.identity);
                    _tmp.transform.rotation = rotation;

                    if (!_isBlocked || generationType == GenerationType.remove)
                    {
                        pathCellsPositions.Add(_tmp.transform.position);
                    }

                    previewBlocks.Add(_tmp);
                }
            }

            return new Vector2Int(currentMouseGridPosition.x, _startPosition.y);
        }

        Vector2Int IterateY(Vector2Int _startPosition)
        {
            if (currentMouseGridPosition.y - _startPosition.y >= 0)
            {
                for (int y = _startPosition.y; y <= currentMouseGridPosition.y; y += currentPathNetwork.gridCellSize)
                {
                    bool _isBlocked = false;
                    if (checkForCellTypesOnPathNetwork)
                    {
                        var _cellType = currentPathNetwork.GetGridCellTypeAtPosition(new Vector3(_startPosition.x, 0, y));
                        if (_cellType == PathNetwork.CellType.blocked || _cellType != currentPathPresetData.pathType)
                        {
                            _isBlocked = true;
                        }
                    }

                    Quaternion rotation = Quaternion.identity;
                    var _tmp = Instantiate(_isBlocked || generationType == GenerationType.remove ? pathVisualizationPrefabFailed : pathVisualizationPrefabSuccess, new Vector3(_startPosition.x, GetHeightPosition(_startPosition.x, y, out rotation), y), Quaternion.identity);
                    _tmp.transform.rotation = rotation;
                    if (!_isBlocked || generationType == GenerationType.remove)
                    {
                        pathCellsPositions.Add(_tmp.transform.position);
                    }

                    previewBlocks.Add(_tmp);
                }
            }
            else
            {
                for (int y = _startPosition.y; y >= currentMouseGridPosition.y; y -= currentPathNetwork.gridCellSize)
                {
                    bool _isBlocked = false;
                    if (checkForCellTypesOnPathNetwork)
                    {
                        var _cellType = currentPathNetwork.GetGridCellTypeAtPosition(new Vector3(_startPosition.x, 0, y));
                        if (_cellType == PathNetwork.CellType.blocked || _cellType != currentPathPresetData.pathType)
                        {
                            _isBlocked = true;
                        }
                    }

                    Quaternion rotation = Quaternion.identity;
                    var _tmp = Instantiate(_isBlocked || generationType == GenerationType.remove ? pathVisualizationPrefabFailed : pathVisualizationPrefabSuccess, new Vector3(_startPosition.x, GetHeightPosition(_startPosition.x, y, out rotation), y), Quaternion.identity);
                    _tmp.transform.rotation = rotation;
                    if (!_isBlocked || generationType == GenerationType.remove)
                    {
                        pathCellsPositions.Add(_tmp.transform.position);
                    }

                    previewBlocks.Add(_tmp);
                }
            }
            return new Vector2Int(_startPosition.x, currentMouseGridPosition.y);
        }

        float GetHeightPosition(int _x, int _y, out Quaternion _rotation)
        {
            RaycastHit _raycastHit;
            var _startPoint = new Vector3 (_x, 50, _y);
            var _endPoint = new Vector3 (_x, -100, _y);

            _rotation = Quaternion.identity;

            if (Physics.Raycast(_startPoint, -Vector3.up, out _raycastHit, 100, terrainLayer))
            {
                _rotation = Quaternion.FromToRotation (transform.up, _raycastHit.normal);
                return _raycastHit.point.y;
            } 

            return 0f;
        }

        void ClearPreviewBlocks()
        {
            for (int i = 0; i < previewBlocks.Count; i ++)
            {
                Destroy(previewBlocks[i]);
            }

            pathCellsPositions = new List<Vector3>();
            previewBlocks = new List<GameObject>();
        }
        #endregion
    #endregion
    }
}