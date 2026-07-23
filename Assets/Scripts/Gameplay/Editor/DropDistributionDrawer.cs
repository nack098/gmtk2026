#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TrashCount.Gameplay.Distributions;

namespace TrashCount.Gameplay.Editor
{
    [CustomPropertyDrawer(typeof(IDropDistribution), useForChildren: true)]
    public class DropDistributionDrawer : PropertyDrawer
    {
        private static Dictionary<string, Type> _types;
        private static string[] _typeNames;

        private static void InitializeTypes()
        {
            if (_types != null) return;

            var allTypes = TypeCache.GetTypesDerivedFrom<IDropDistribution>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name);

            _types = new Dictionary<string, Type>();
            foreach (var type in allTypes)
            {
                string name = type.Name.Replace("Distribution", "");
                _types[name] = type;
            }

            _typeNames = _types.Keys.ToArray();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InitializeTypes();
            EditorGUI.BeginProperty(position, label, property);

            Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            Type currentType = property.managedReferenceValue?.GetType();
            string currentDisplayName = currentType != null ? currentType.Name.Replace("Distribution", "") : _typeNames[0];

            int currentIndex = Math.Max(0, Array.IndexOf(_typeNames, currentDisplayName));

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, _typeNames);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Change Drop Distribution");
                property.managedReferenceValue = Activator.CreateInstance(_types[_typeNames[newIndex]]);
                EditorGUI.EndProperty();
                return;
            }

            if (property.managedReferenceValue != null)
            {
                float y = position.y + EditorGUIUtility.singleLineHeight + 2f;
                EditorGUI.indentLevel++;

                SerializedProperty copy = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                bool enterChildren = true;

                while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
                {
                    enterChildren = false;
                    float h = EditorGUI.GetPropertyHeight(copy, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), copy, true);
                    y += h + 2f;
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
                total += 2f;
                SerializedProperty copy = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                bool enterChildren = true;

                while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
                {
                    enterChildren = false;
                    total += EditorGUI.GetPropertyHeight(copy, true) + 2f;
                }
            }

            return total;
        }
    }
}
#endif