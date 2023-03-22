
using System.Collections.Generic;
using UnityEngine;

static class FindExtensions
{
    /// <summary>
    /// Find all the game objects that contains a certain component type.
    /// </summary>
    /// <typeparam name="T">Type of component to search for</typeparam>
    /// <param name="gameObject">Game object for which to search it's children</param>
    /// <param name="includeInactive">Will also include and search inactive objects.</param>
    /// <returns>Array of game object found, all of which containing a component of the specified type</returns>
    public static List<T> FindComponentsInDescendants<T>(this GameObject gameObject, bool includeInactive = false) where T : class
    {
        List<T> list = new List<T>();

        // Search in all the children of the specified game object
        foreach (Transform t in gameObject.transform)
        {
            if (!includeInactive && !t.gameObject.activeSelf)
                continue;

            // Found one, check component
            T comp = t.GetComponent<T>();
            if (comp is not null)
                list.Add(comp);

            // Recursively search into the children of this game object
            list.AddRange(FindComponentsInDescendants<T>(t.gameObject));
        }
        return list;
    }


    /// <summary>
    /// Find all the game objects that contains a certain component type.
    /// </summary>
    /// <typeparam name="T">Type of component to search for</typeparam>
    /// <param name="gameObject">Game object for which to search it's children</param>
    /// <param name="includeInactive">Will also include and search inactive objects.</param>
    /// <returns>Array of game object found, all of which containing a component of the specified type</returns>
    public static T FindComponentInDescendants<T>(this GameObject gameObject, bool includeInactive = false) where T : class
    {
        // Search in all the children of the specified game object
        foreach (Transform t in gameObject.transform)
        {
            if (!includeInactive && !t.gameObject.activeSelf)
                continue;

            T comp = t.GetComponent<T>();
            comp ??= FindComponentInDescendants<T>(t.gameObject);
            
            if (comp is not null)
                return comp;
        }
        return null;
    }

}

