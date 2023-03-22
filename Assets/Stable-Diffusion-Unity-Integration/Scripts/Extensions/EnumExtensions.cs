using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class EnumExtensions
{
    public static IEnumerable<string> ToStrings(Type enumType)
    {
        if (!enumType.IsEnum)
        {
            Debug.LogError($"{enumType} is not an Enum");
            return null;
        }
        var names = enumType.GetFields().Skip(1)
            .Select(selector: e =>
                e.GetCustomAttribute<DescriptionAttribute>()?.Description ?? e.Name
            );
        return names;
    }

    public static IEnumerable<string> ToStrings<T>() where T:Enum
    {
        var names = typeof(T).GetFields().Skip(1)
            .Select(selector: e =>
                e.GetCustomAttribute<DescriptionAttribute>()?.Description ?? e.Name
            );
        return names;
    }
}
