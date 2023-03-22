using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;


public class ToggleLeft : PropertyAttribute
{

}

[CustomPropertyDrawer(typeof(ToggleLeft))]
public class ToggleLeftPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Shift the label to the left of the toggle
        Rect togglePosition = position;
        //togglePosition.x += EditorGUIUtility.labelWidth;
        //togglePosition.width -= EditorGUIUtility.labelWidth;

        //EditorGUI.BeginProperty(position, label, property);
        property.boolValue = EditorGUI.ToggleLeft(togglePosition, label, property.boolValue);
        //EditorGUI.EndProperty();
    }
}

