using System;
using System.Reflection;

namespace PanAndProspect;

public static class ReflectionExtensions
{
    public static T GetField<T>(this object obj, string fieldName)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var fi = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (fi == null) return default;

        return (T)fi.GetValue(obj);
    }

    public static void SetField<T>(this T obj, string fieldName, object value)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var fi = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (fi == null) throw new InvalidOperationException($"Field '{fieldName}' not found.");

        fi.SetValue(obj, value);
    }
    
    public static T GetBaseField<T>(this object obj, string fieldName)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var fi = obj.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (fi == null) return default;

        return (T)fi.GetValue(obj);
    }

    public static void SetBaseField<T>(this T obj, string fieldName, object value)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var fi = obj.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (fi == null) throw new InvalidOperationException($"Field '{fieldName}' not found.");

        fi.SetValue(obj, value);
    }
}