using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using System.Linq;
using Object = UnityEngine.Object;

namespace Recstazy.SerializedInterface
{
    [CustomPropertyDrawer(typeof(IFieldAttribute))]
    public class IFieldDrawer : PropertyDrawer
    {
        #region Fields

        private const float LabelRatio = 0.36667f;
        private const string ObjectPickerSelectCommand = "ObjectSelectorUpdated";
        private const string ObjectPickerCloseCommand = "ObjectSelectorClosed";
        private static readonly float s_buttonWidth = EditorGUIUtility.singleLineHeight;
        private static GUIStyle s_normalStyle = new GUIStyle(EditorStyles.objectField);
        private static GUIStyle s_buttonStyle = "ObjectFieldButton";

        private SerializedProperty _property;
        private Type _interfaceType;
        private Type _fieldType;
        private Rect _rect;
        private int _currentPickerId;
        private bool _isSceneObject;

        #endregion

        #region Properties

        #endregion

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            _property = property;
            _rect = position;
            _interfaceType = (attribute as IFieldAttribute)?.InterfaceType;
            _fieldType = fieldInfo.FieldType;
            _isSceneObject = property.serializedObject.targetObject is Component;

            if (_interfaceType != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                DrawInterfaceField(property, label);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }

            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                    UpdateDrag();
                    break;
                case EventType.DragPerform:
                    DragPerformed();
                    break;
                case EventType.ExecuteCommand:
                    PrecessObjectPickerCommands();
                    break;
            }
        }

        private void DrawInterfaceField(SerializedProperty property, GUIContent label)
        {
            var value = property.objectReferenceValue;
            var rect = _rect;
            rect.width = _rect.width * LabelRatio;
            rect.width = Mathf.Max(rect.width, EditorGUIUtility.labelWidth + EditorGUIUtility.standardVerticalSpacing);
            GUI.Label(rect, label);

            rect.x += rect.width;
            rect.width = _rect.width - rect.width;
            GUIContent content = EditorGUIUtility.ObjectContent(value, typeof(GameObject));
            content.text = value == null ? $"None ({_fieldType.Name} : {_interfaceType.Name})" : $"{value.name} ({value.GetType().Name})";
            GUI.Box(rect, content, s_normalStyle);

            rect.x = _rect.x + _rect.width - s_buttonWidth - 1f;
            rect.width = s_buttonWidth;
            rect.height -= 2f;
            rect.y += 1f;

            if (GUI.Button(rect, GUIContent.none, s_buttonStyle))
            {
                ShowObjectPicker(value);
            }
        }

        private void UpdateDrag()
        {
            if (_rect.Contains(Event.current.mousePosition))
            {
                bool acceptable = TryGetInterfaceObjectFromDragDrop(out var obj);
                DragAndDrop.visualMode = acceptable ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                Event.current.Use();
            }
        }

        private void DragPerformed()
        {
            if (_rect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.AcceptDrag();
                Event.current.Use();

                if (TryGetInterfaceObjectFromDragDrop(out var objectValue))
                {
                    _property.objectReferenceValue = objectValue;
                }
            }
        }

        private bool TryGetInterfaceObjectFromDragDrop(out Object value)
        {
            if (DragAndDrop.objectReferences.Length == 1)
            {
                return TryGetTypedInterfacedObject(DragAndDrop.objectReferences[0], out value);
            }

            value = null;
            return false;
        }

        private bool TryGetTypedInterfacedObject(Object reference, out Object result)
        {
            if (reference == null)
            {
                result = null;
                return true;
            }

            if (_isSceneObject && typeof(GameObject).IsAssignableFrom(reference.GetType()))
            {
                bool hasComponent = (reference as GameObject).TryGetComponent(_interfaceType, out var component);
                result = component as Object;
                return hasComponent;
            }
            else
            {
                if (!_isSceneObject && reference is Component)
                {
                    result = null;
                    return false;
                }

                var referenceType = reference.GetType();

                if (referenceType.IsSubclassOf(_fieldType) && _interfaceType.IsAssignableFrom(referenceType))
                {
                    result = reference;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private void ShowObjectPicker(Object currentValue)
        {
            IEnumerable<Type> compatableTypes = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var a in assemblies)
            {
                var typesInAssemblies = a.GetTypes().Where(t => t.IsSubclassOf(typeof(Object)) && t.GetInterface(_interfaceType.Name) != null).ToArray();
                compatableTypes = compatableTypes.Concat(typesInAssemblies);
            }

            var typesArray = compatableTypes.ToArray();
            string filter = string.Empty;

            for (int i = 0; i < typesArray.Length; i++)
            {
                filter += $"t:{typesArray[i].Name}";

                if (i < typesArray.Length - 1)
                {
                    filter += ", ";
                }
            }

            _currentPickerId = GUIUtility.GetControlID(FocusType.Passive) + 100;
            ShowObjectPickerByType(_fieldType, currentValue, _isSceneObject, filter, _currentPickerId);
        }

        private void PrecessObjectPickerCommands()
        {
            if (Event.current.commandName == ObjectPickerSelectCommand)
            {
                if (_currentPickerId == EditorGUIUtility.GetObjectPickerControlID())
                {
                    var pickerObject = EditorGUIUtility.GetObjectPickerObject();

                    if (TryGetTypedInterfacedObject(pickerObject, out var interfacedObject))
                    {
                        _property.objectReferenceValue = interfacedObject;
                    }
                    
                    Event.current.Use();
                }
            }
        }

        private void ShowObjectPickerByType(Type type, Object currentValue, bool allowSceneObjects, string filter, int controlId)
        {
            typeof(EditorGUIUtility)
                .GetMethod("ShowObjectPicker")
                .MakeGenericMethod(type)
                .Invoke(null, new object[] { currentValue, allowSceneObjects, filter, controlId });
        }
    }
}