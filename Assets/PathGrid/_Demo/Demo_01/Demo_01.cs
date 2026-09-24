using UnityEngine;
using PathGrid.Input;

namespace PathGrid.Example
{
    public class Demo_01 : MonoBehaviour
    {
        public PathGridInputSystem gridSystemInput;
        public PathGridSystem gridSystem;
        public GameObject buildingPrefab;
        public GameObject grassLayer;
        public GameObject earthLayer;
        public GameObject waterLayer;
        public GameObject pathNetwork;
        public GameObject pipeNetwork;
        public GameObject buildings;


        public void HideUpperWorld()
        {
            grassLayer.SetActive(false);
            waterLayer.SetActive(false);
            buildings.SetActive(false);
            pathNetwork.SetActive(false);          
            pipeNetwork.SetActive(true);


            var _earthRenderer = earthLayer.GetComponent<MeshRenderer>();
            var _earthColor = _earthRenderer.material.color;
            _earthColor.a = 10f/255f;
            _earthRenderer.material.color = _earthColor;
        }

        public void ShowUpperWorld()
        {
            grassLayer.SetActive(true);
            waterLayer.SetActive(true);
            pathNetwork.SetActive(true);      
            buildings.SetActive(true);        
            pipeNetwork.SetActive(false);

            var _earthRenderer = earthLayer.GetComponent<MeshRenderer>();
            var _earthColor = _earthRenderer.material.color;
            _earthColor.a = 255f/255f;
            _earthRenderer.material.color = _earthColor;
            
        }

        public void SetPreset(int index)
        {
            gridSystem.SetPathPresetIndex(index);
        }

        public void InstantiateBuilding()
        {
            var _tmp = Instantiate(buildingPrefab, Vector3.zero, Quaternion.identity);
            _tmp.transform.SetParent(buildings.transform);
        }
    }
}