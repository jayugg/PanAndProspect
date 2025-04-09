using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PanAndProspect;

public static class CollectibleExtensions
{
    public static void AddBehavior<T>(this CollectibleObject collectible) where T : CollectibleBehavior
    {
        PanAndProspectCore.Logger.Warning("Adding behavior {0} to collectible {1}", typeof(T).Name, collectible.Code);
        var existingBehavior = collectible.CollectibleBehaviors.FirstOrDefault(b => b.GetType() == typeof(T));
        collectible.CollectibleBehaviors.Remove(existingBehavior);
        var behavior = (T) Activator.CreateInstance(typeof(T), collectible);
        collectible.CollectibleBehaviors = collectible.CollectibleBehaviors.Append(behavior);
    }
}