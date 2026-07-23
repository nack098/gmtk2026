#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TrashCount.Data.Models;

namespace TrashCount.Data.Editor
{
    [CustomPropertyDrawer(typeof(ItemModel))]
    public class ItemModelDrawer : PropertyDrawer
    {
        private const string NoneLabel = "None (Null)";
        private const float Spacing = 2f;

        private static Dictionary<string, Type> _types;
        private static string[] _names;

        private static void InitializeTypes()
        {
            if (_types != null) return;

            var allTypes = TypeCache.GetTypesDerivedFrom<ItemBaseModel>().ToList();
            if (!typeof(ItemBaseModel).IsAbstract && !typeof(ItemBaseModel).IsInterface)
            {
                allTypes.Add(typeof(ItemBaseModel));
            }

            _types = allTypes
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.FullName)
                // Use FullName with slashes to prevent name collisions and enable submenus
                .ToDictionary(t => t.FullName, t => t);

            _names = new[] { NoneLabel }.Concat(_types.Keys).ToArray();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InitializeTypes();
            EditorGUI.BeginProperty(position, label, property);
            
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            if (valueProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Field 'Value' not found. Check case sensitivity.");
                EditorGUI.EndProperty();
                return;
            }

            Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string currentName = valueProp.managedReferenceValue?.GetType().FullName;
            int currentIndex = Math.Max(0, Array.IndexOf(_names, currentName));

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, _names);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Change Item Model Type");

                valueProp.managedReferenceValue = newIndex == 0
                    ? null
                    : Activator.CreateInstance(_types[_names[newIndex]]);
                
                valueProp.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(property.serializedObject.targetObject);

                EditorGUI.EndProperty();
                return; 
            }

            if (valueProp.managedReferenceValue != null)
            {
                float y = position.y + EditorGUIUtility.singleLineHeight + Spacing;
                EditorGUI.indentLevel++;

                SerializedProperty copy = valueProp.Copy();
                SerializedProperty end = valueProp.GetEndProperty();
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
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            float total = EditorGUIUtility.singleLineHeight;

            if (valueProp != null && valueProp.managedReferenceValue != null)
            {
                total += Spacing;
                SerializedProperty copy = valueProp.Copy();
                SerializedProperty end = valueProp.GetEndProperty();
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