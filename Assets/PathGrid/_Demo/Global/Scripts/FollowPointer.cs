using UnityEngine;
using UnityEngine.EventSystems;

namespace PathGrid.Example
{
    /// <summary>
    /// Simple script which snaps the game objects in a grid.
    /// This script also handles the placement of the building/game object
    /// </summary>
    public class FollowPointer : MonoBehaviour
    {
        public LayerMask groundLayerMask;
        public int gridCellSize = 1;
        
        
        private SetGridObstacle setGridObstacle;
        private Vector3 mousePosition;

        void Start()
        {
            setGridObstacle = GetComponent<SetGridObstacle>();

        }

        // Update is called once per frame
        void Update()
        {

            var ray = Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
            RaycastHit hit;
        
            if (Physics.Raycast(ray, out hit, 1000, groundLayerMask))
            {
                mousePosition = new Vector3(hit.point.x, hit.point.y, hit.point.z);
            }

            // Snap to grid
            Vector3 pos = mousePosition;
            pos.x = Mathf.RoundToInt(pos.x / gridCellSize) * gridCellSize;
            pos.z = Mathf.RoundToInt(pos.z / gridCellSize) * gridCellSize;
            this.transform.position = new Vector3 (pos.x + 0.5f, pos.y, pos.z + 0.5f);

            // Place building
            if (UnityEngine.Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject() && setGridObstacle.IsValid())
            {
                setGridObstacle.SetObstacle();
                this.enabled = false;
            }

            // Right click destroy building
            if (UnityEngine.Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject())
            {
                Destroy(this.gameObject);
            }
        }
    }
}