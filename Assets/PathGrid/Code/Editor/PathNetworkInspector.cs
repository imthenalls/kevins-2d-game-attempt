#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace PathGrid.Editor
{
    [CustomEditor(typeof(PathNetwork))]
    public class PathNetworkInspector : UnityEditor.Editor
    {
        VisualElement root;
        VisualElement adaptToTerrainHeightContainer;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();


            var _header = new VisualElement();
            _header.style.flexGrow = 1;
            _header.style.height = 40;
            _header.style.backgroundImage = PathGridUIHelper.LoadTexture("header.png", "PathGridResPath.cs");
            _header.style.marginBottom = 10;

            var _headerLabel = new Label();
            _headerLabel.text = "PATH NETWORK";
            _headerLabel.style.fontSize = 18;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.SetMargin(10, 10, 8, 0);
            _header.Add(_headerLabel);

            var _gridCellSize = new PropertyField();
            _gridCellSize.BindProperty(serializedObject.FindProperty("gridCellSize"));

            var _yOffset = new PropertyField();
            _yOffset.BindProperty(serializedObject.FindProperty("yOffset"));

            var _adaptToHeight = new Toggle();
            _adaptToHeight.label = "Adapt to height";
            _adaptToHeight.BindProperty(serializedObject.FindProperty("adaptToTerrainHeight"));
            _adaptToHeight.RegisterValueChangedCallback(change => 
            {
                adaptToTerrainHeightContainer.SetEnabled(change.newValue);
            });

            adaptToTerrainHeightContainer = BuildAdaptToTerrainUI();

            root.Add(_header);
            root.Add(_gridCellSize);
            root.Add(_yOffset);
            root.Add(_adaptToHeight);
            root.Add(adaptToTerrainHeightContainer);
            

            return root;
        }

        VisualElement BuildAdaptToTerrainUI()
        {
            var _container = new VisualElement();
            _container.SetBorder(1, 1, 1, 1, Color.grey);
            _container.SetPadding(5, 5, 5, 5);
            
            var _terrainLayer = new PropertyField();
            _terrainLayer.BindProperty(serializedObject.FindProperty("terrainLayer"));

            _container.Add(_terrainLayer);

            return _container;
        }
    }
}
#endif