#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TrashCount.Data.Models;

namespace TrashCount.Data.Editor
{
    [CustomPropertyDrawer(typeof(IItemCapability), useForChildren: true)]
    public class ItemCapabilityDrawer : PropertyDrawer
    {
        private const string NoneLabel = "<Select Capability Type>";
        private const float Spacing = 2f;

        private static Dictionary<string, Type> _types;
        private static string[] _typeNames;

        [InitializeOnLoadMethod]
        private static void ClearTypeCache()
        {
            _types = null;
            _typeNames = null;
        }

        private static void InitializeTypes()
        {
            if (_types != null) return;

            var allTypes = TypeCache.GetTypesDerivedFrom<IItemCapability>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name);

            _types = new Dictionary<string, Type>();
            foreach (var type in allTypes)
            {
                string displayName = type.Name.Replace("Capability", "");
                _types[displayName] = type;
            }

            _typeNames = new[] { NoneLabel }.Concat(_types.Keys).ToArray();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InitializeTypes();
            EditorGUI.BeginProperty(position, label, property);

            Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            Type currentType = property.managedReferenceValue?.GetType();
            string currentDisplayName = currentType != null ? currentType.Name.Replace("Capability", "") : NoneLabel;
            
            int currentIndex = Math.Max(0, Array.IndexOf(_typeNames, currentDisplayName));

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, _typeNames);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Change Item Capability Type");

                property.managedReferenceValue = newIndex == 0
                    ? null
                    : Activator.CreateInstance(_types[_typeNames[newIndex]]);

                EditorGUI.EndProperty();
                return;
            }

            if (property.managedReferenceValue != null)
            {
                float y = position.y + EditorGUIUtility.singleLineHeight + Spacing;
                EditorGUI.indentLevel++;

                SerializedProperty copy = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                bool enterChildren = true;

                while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
                {
                    enterChildren = false;
                    float h = EditorGUI.GetPropertyHeight(copy, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), copy, true);
                    y += h + Spacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float total = EditorGUIUtility.singleLineHeight;

            if (property.managedReferenceValue != null)
            {
                total += Spacing;
                SerializedProperty copy = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                bool enterChildren = true;

                while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
                {
                    enterChildren = false;
                    total += EditorGUI.GetPropertyHeight(copy, true) + Spacing;
                }
            }

            return total;
        }
    }
}
#endif