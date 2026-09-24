#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using PathGrid.Input;

namespace PathGrid.Editor
{
    [CustomEditor(typeof(PathGridInputSystem))]
    public class PathGridInputSystemInspector : UnityEditor.Editor
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
            _headerLabel.text = "PATHGRID - INPUT";
            _headerLabel.style.fontSize = 18;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.SetMargin(10, 10, 8, 0);
            _header.Add(_headerLabel);

            // References
            var _referencesContainer = new VisualElement();
            _referencesContainer.SetBorder(1,1,1,1, Color.grey);
            _referencesContainer.SetPadding(5, 5, 5, 5);
            _referencesContainer.SetMargin(0, 0, 0, 5);

            var _referencesLabel = new Label()
            {
                text = "References",
            };
            _referencesLabel.style.fontSize = 14;
            _referencesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;


            var _gridPathSystem = new PropertyField();
            _gridPathSystem.BindProperty(serializedObject.FindProperty("pathGridSystem"));

            var _mainCamera = new PropertyField();
            _mainCamera.BindProperty(serializedObject.FindProperty("mainCamera"));

            var _pointer = new PropertyField();
            _pointer.BindProperty(serializedObject.FindProperty("pointer"));

            var _previewBlockSuccess = new PropertyField();
            _previewBlockSuccess.BindProperty(serializedObject.FindProperty("pathVisualizationPrefabSuccess"));

            var _previewBlockFailed = new PropertyField();
            _previewBlockFailed.BindProperty(serializedObject.FindProperty("pathVisualizationPrefabFailed"));

            
            _referencesContainer.Add(_referencesLabel);
            _referencesContainer.Add(_gridPathSystem);
            _referencesContainer.Add(_mainCamera);
            _referencesContainer.Add(_pointer);
            _referencesContainer.Add(_previewBlockSuccess);
            _referencesContainer.Add(_previewBlockFailed);
            

            // Input
            #if ENABLE_INPUT_SYSTEM
            var _inputContainer = new VisualElement();
            _inputContainer.SetBorder(1,1,1,1, Color.grey);
            _inputContainer.SetPadding(5, 5, 5, 5);
            _inputContainer.SetMargin(0, 0, 0, 5);

            var _inputLabel = new Label()
            {
                text = "Input",
            };
            _inputLabel.style.fontSize = 14;
            _inputLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var _leftInput = new PropertyField();
            _leftInput.BindProperty(serializedObject.FindProperty("mouseLeftClickInput"));
            
            var _rightInput = new PropertyField();
            _rightInput.BindProperty(serializedObject.FindProperty("mouseRightClickInput"));
            
            _inputContainer.Add(_inputLabel);
            _inputContainer.Add(_leftInput);
            _inputContainer.Add(_rightInput);
            #endif

            // Settings
            var _settingsContainer = new VisualElement();
            _settingsContainer.SetBorder(1,1,1,1, Color.grey);
            _settingsContainer.SetPadding(5, 5, 5, 5);
            _settingsContainer.SetMargin(0, 0, 0, 5);

            var _settingsLabel = new Label()
            {
                text = "Settings",
            };

            _settingsLabel.style.fontSize = 14;
            _settingsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;


            var _generationType = new PropertyField();
            _generationType.BindProperty(serializedObject.FindProperty("generationType"));

            var _layer = new PropertyField();
            _layer.BindProperty(serializedObject.FindProperty("terrainLayer"));

            var _checkForType = new PropertyField();
            _checkForType.BindProperty(serializedObject.FindProperty("checkForCellTypesOnPathNetwork"));

            var _directionLockDistance = new PropertyField();
            _directionLockDistance.BindProperty(serializedObject.FindProperty("directionLockDistance"));

            _settingsContainer.Add(_settingsLabel);
            _settingsContainer.Add(_generationType);
            _settingsContainer.Add(_layer);
            _settingsContainer.Add(_checkForType);
            _settingsContainer.Add(_directionLockDistance);


            _root.Add(_header);
            _root.Add(_referencesContainer);
            #if ENABLE_INPUT_SYSTEM
            _root.Add(_inputContainer);
            #endif
            _root.Add(_settingsContainer);


            return _root;
        }
    }
}
#endif