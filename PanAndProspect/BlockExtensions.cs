using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PanAndProspect;

public static class BlockExtensions
{
    public static void AddBehavior<T>(this Block block) where T : BlockBehavior
    {
        PanAndProspectCore.Logger.Warning("Adding behavior {0} to collectible {1}", typeof(T).Name, block.Code);
        var existingBehavior =  block.CollectibleBehaviors.FirstOrDefault(b => b.GetType() == typeof(T));
        block.CollectibleBehaviors.Remove(existingBehavior);
        var behavior = (T) Activator.CreateInstance(typeof(T), block);
        block.CollectibleBehaviors = block.CollectibleBehaviors.Append(behavior);
    }
}