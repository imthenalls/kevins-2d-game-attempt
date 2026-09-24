#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PathGrid.Editor
{
[CustomEditor(typeof(PathPreset))]
    public class PathGridPresetInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var _root = new VisualElement();

            var _header = new VisualElement();
            _header.style.flexGrow = 1;
            _header.style.height = 40;
            _header.style.backgroundImage = PathGridUIHelper.LoadTexture("header.png", "PathGridResPath.cs");
            // _header.style.backgroundColor = Color.black;
            _header.style.marginBottom = 10;

            var _headerLabel = new Label();
            _headerLabel.text = "PATHGRID - PRESET";
            _headerLabel.style.fontSize = 18;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.SetMargin(10, 10, 8, 0);
            _header.Add(_headerLabel);

            var _type = new PropertyField();
            _type.BindProperty(serializedObject.FindProperty("pathType"));
            
            var _yOffset = new PropertyField();
            _yOffset.BindProperty(serializedObject.FindProperty("yPosition"));
            _yOffset.label = "Y position offset";

            var _labelPrefabs = new Label()
            {
                text = "Prefabs",
            };
            _labelPrefabs.SetMargin(0, 0, 10, 5);
            _labelPrefabs.style.fontSize = 14;
            _labelPrefabs.style.unityFontStyleAndWeight = FontStyle.Bold;

            var _labelSettings = new Label()
            {
                text = "Settings",
            };
            _labelSettings.SetMargin(0, 0, 10, 5);
            _labelSettings.style.fontSize = 14;
            _labelSettings.style.unityFontStyleAndWeight = FontStyle.Bold;

            _root.Add(_header);           
            _root.Add(_labelSettings);
            _root.Add(_type);
            _root.Add(_yOffset);
            _root.Add(_labelPrefabs);
            _root.Add(BuildTileUI("single_tile.png", "single", ""));
            _root.Add(BuildTileUI("straight_tile.png", "straight", "straightRotationOffset"));
            _root.Add(BuildTileUI("curve_tile.png", "curve", "curveRotationOffset"));
            _root.Add(BuildTileUI("threeway_tile.png", "threeway", "threewayRotationOffset"));
            _root.Add(BuildTileUI("crossway_tile.png", "crossway", ""));
            _root.Add(BuildTileUI("deadEnd_tile.png", "deadEnd", "deadEndRotationOffset"));

            return _root;
        }

        VisualElement BuildTileUI(string _tileName, string _prefabName, string _offsetProperty)
        {
            var _container = new VisualElement();
            _container.style.flexGrow = 1;
            _container.style.flexDirection = FlexDirection.Row;
            // _container.style.backgroundColor = Color.grey;
            _container.SetBorder(2, 2, 2, 2, Color.grey);
            _container.SetMargin(0, 0, 0, 2);
            _container.SetPadding(4, 4, 4 ,4);
            _container.style.alignItems = new StyleEnum<Align>(Align.Center);

            var _icon = new VisualElement();
            _icon.style.backgroundImage = PathGridUIHelper.LoadTexture(_tileName, "PathGridResPath.cs");
            _icon.style.width = 50;
            _icon.style.height = 50;

            var _properties = new VisualElement();
            
            var _prefab = new PropertyField();
            _prefab.BindProperty(serializedObject.FindProperty(_prefabName));

            _properties.Add(_prefab);

            if (!string.IsNullOrEmpty(_offsetProperty))
            {
                var _offset = new PropertyField();
                _offset.BindProperty(serializedObject.FindProperty(_offsetProperty));
                _offset.label = "Y rotation offset";
                _properties.Add(_offset);
            }
            


            _container.Add(_icon);
            _container.Add(_properties);

            return _container;

        }
    }
}
#endif