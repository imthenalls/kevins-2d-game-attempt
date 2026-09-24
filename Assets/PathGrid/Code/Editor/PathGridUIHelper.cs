#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PathGrid.Editor
{
    public static class PathGridUIHelper
    {
        public static void SetBorder(this VisualElement _target, int _leftWidth, int _rightWidth, int _topWidth, int _bottomWidth, Color _borderColor)
        {
            _target.style.borderLeftWidth = _leftWidth;
            _target.style.borderRightWidth = _leftWidth;
            _target.style.borderTopWidth = _leftWidth;
            _target.style.borderBottomWidth = _leftWidth;

            _target.style.borderBottomColor = _borderColor;
            _target.style.borderTopColor = _borderColor;
            _target.style.borderLeftColor = _borderColor;
            _target.style.borderRightColor = _borderColor;
        }

        public static void SetBorderRadius(this VisualElement _target, int _topLeftRadius, int _topRightRadius, int _bottomLeftRadius, int _bottomRightRadius)
        {
            _target.style.borderTopLeftRadius= _topLeftRadius;
            _target.style.borderTopRightRadius = _topRightRadius;
            _target.style.borderBottomLeftRadius = _bottomLeftRadius;
            _target.style.borderBottomRightRadius = _bottomRightRadius;
        }

        public static void SetMargin(this VisualElement _target, int _left, int _right, int _top, int _bottom)
        {
            _target.style.marginLeft = _left;
            _target.style.marginRight = _right;
            _target.style.marginTop = _top;
            _target.style.marginBottom = _bottom;
        }

        public static void SetPadding(this VisualElement _target, int _left, int _right, int _top, int _bottom)
        {
            _target.style.paddingLeft = _left;
            _target.style.paddingRight = _right;
            _target.style.paddingTop = _top;
            _target.style.paddingBottom = _bottom;
        }




        public static Texture2D LoadTexture(string _fileName, string _rootFile)
		{
			var _res = Directory.EnumerateFiles("Assets/", _rootFile, SearchOption.AllDirectories);
			var _found = _res.FirstOrDefault();
			var _path = "";
			if (!string.IsNullOrEmpty(_found))
			{
				_path = _found.Replace(_rootFile, "").Replace("\\", "/");
			}

            return (Texture2D)(AssetDatabase.LoadAssetAtPath(_path + "/" + _fileName, typeof(Texture2D)));
        }
    }
}
#endif