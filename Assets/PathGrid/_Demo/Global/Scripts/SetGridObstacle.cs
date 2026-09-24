using UnityEngine;

namespace PathGrid.Example
{
    /// <summary>
    /// Simple example script which modifies the cell type of a PathNetwork grid.
    /// Based on the cell type we can block path placement if it is blocked by a building or if the cell type is not supported for selected path preset.
    /// </summary>
    public class SetGridObstacle : MonoBehaviour
    {
        public Vector2Int size;
        public int gridCellSize;
        public bool setAtStart;
        public PathNetwork.CellType cellType;

        private PathGridSystem pathGridSystem;
        

        public void Start()
        {
            pathGridSystem = GameObject.FindObjectOfType<PathGridSystem>();
            
            // Set cell type at start
            if (setAtStart)
            {
                SetObstacle();
            }
        }

        /// <summary>
        /// Set for each cell in size the cell type. 
        /// </summary>
        public void SetObstacle()
        {
            for (int x = 0 ; x < size.x; x ++)
            {
                for (int y = 0; y < size.y; y ++)
                {
                    var _position = new Vector3(this.transform.position.x - (size.x * 0.5f) + x + (gridCellSize * 0.5f), this.transform.position.y + 10, this.transform.position.z - (size.y * 0.5f) + y + (gridCellSize * 0.5f));
                    pathGridSystem.GetCurrentPathNetwork().SetCellTypeAtPosition(_position, cellType);
                }
            }
        }

        /// <summary>
        /// Simple check which returns true or false if a cell has CellType.blocked
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {  
            var _isValid = true;
            for (int x = 0; x < size.x; x ++)
            {
                for (int y = 0; y < size.y; y ++)
                {

                    var _position = new Vector3(this.transform.position.x - 0.5f + x, this.transform.position.y, this.transform.position.z - 0.5f+ y);
                    var _type = pathGridSystem.GetCurrentPathNetwork().GetGridCellTypeAtPosition(_position);
                    if (_type == PathNetwork.CellType.blocked)
                    {
                        _isValid = false;
                    }
                }
            }

            return _isValid;
        }
        


        void OnDrawGizmos()
        {
            Gizmos.DrawWireCube(this.transform.position, new Vector3(size.x, 1, size.y));
            for (int x = 0 ; x < size.x; x ++)
            {
                for (int y = 0; y < size.y; y ++)
                {
                    var _positionA = new Vector3(this.transform.position.x - (size.x * 0.5f) + x + (gridCellSize * 0.5f), this.transform.position.y + 10, this.transform.position.z - (size.y * 0.5f) + y + (gridCellSize * 0.5f));
                    var _positionB= new Vector3(this.transform.position.x - (size.x * 0.5f) + x + (gridCellSize * 0.5f), this.transform.position.y, this.transform.position.z - (size.y * 0.5f) + y + (gridCellSize * 0.5f));
                    Gizmos.DrawLine(_positionA, _positionB);
                }
            }
        }
    }
}