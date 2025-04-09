using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace PanAndProspect;

public static class Extensions
{
    public static ITreeAttribute ToTreeAttribute(this Dictionary<string, double> dictionary)
    {
        var tree = new TreeAttribute();
        foreach (var kvp in dictionary)
        {
            tree.SetDouble(kvp.Key, kvp.Value);
        }
        return tree;
    }
    
    public static ITreeAttribute ToTreeAttribute(this Dictionary<string, float> dictionary)
    {
        var tree = new TreeAttribute();
        foreach (var kvp in dictionary)
        {
            tree.SetFloat(kvp.Key, kvp.Value);
        }
        return tree;
    }
}