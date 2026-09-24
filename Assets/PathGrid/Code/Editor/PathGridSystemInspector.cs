#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PathGrid.Editor
{
    [CustomEditor(typeof(PathGridSystem))]
    public class PathGridSystemInspector : UnityEditor.Editor
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
            _headerLabel.text = "PATHGRID - SYSTEM";
            _headerLabel.style.fontSize = 18;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.SetMargin(10, 10, 8, 0);
            _header.Add(_headerLabel);
            
            var _titlePathNetworks = new Label()
            {
                text = "Path Networks",
            };
            _titlePathNetworks.style.fontSize = 14;
            _titlePathNetworks.style.unityFontStyleAndWeight = FontStyle.Bold;

            var _pathNetworks = new PropertyField();
            _pathNetworks.BindProperty(serializedObject.FindProperty("pathNetworks"));

            var _titlePathPresets = new Label()
            {
                text = "Path Presets"
            };
            _titlePathPresets.style.fontSize = 14;
            _titlePathPresets.style.unityFontStyleAndWeight = FontStyle.Bold;

            var _pathPresets = new PropertyField();
            _pathPresets.BindProperty(serializedObject.FindProperty("pathPresets"));

            _root.Add(_header);
            _root.Add(_titlePathNetworks);
            _root.Add(_pathNetworks);
            _root.Add(_titlePathPresets);
            _root.Add(_pathPresets);

            return _root;
        }
    }
}
#endif