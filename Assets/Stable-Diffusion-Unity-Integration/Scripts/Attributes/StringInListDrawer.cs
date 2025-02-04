using System;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StringInEnumDesc : PropertyAttribute
{
    public delegate string[] GetStringList();

    public StringInEnumDesc(Type enumType) => List = EnumExtensions.ToStrings(enumType).ToArray();

    public StringInEnumDesc(Type type, string methodName)
    {
        var method = type.GetMethod(methodName);
        if (method != null)
        {
            List = method.Invoke(null, null) as string[];
        }
        else
        {
            Debug.LogError("NO SUCH METHOD " + methodName + " FOR " + type);
        }
    }

    public string[] List
    {
        get;
        private set;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(StringInEnumDesc))]
public class StringInListDrawer : PropertyDrawer
{
    // Draw the property inside the given rect
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var stringInList = attribute as StringInEnumDesc;
        var list = stringInList.List;
        if (property.propertyType == SerializedPropertyType.String)
        {
            int index = Mathf.Max(0, Array.IndexOf(list, property.stringValue));
            index = EditorGUI.Popup(position, label.text, index, list);

            property.stringValue = list[index];
        }
        else if (property.propertyType == SerializedPropertyType.Integer)
        {
            property.intValue = EditorGUI.Popup(position, label.text, property.intValue, list);
        }
        else
        {
            base.OnGUI(position, property, label);
        }
    }
}
#endif
