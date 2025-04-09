using System;
using System.Linq;
using Vintagestory.API.Common;

namespace PanAndProspect;

public static class BlockEntityExtensions
{
    public static void AddBehavior<T>(this BlockEntity blockEntity) where T : BlockEntityBehavior
    {
        var existingBehavior = blockEntity.Behaviors.FirstOrDefault(b => b.GetType() == typeof(T));
        blockEntity.Behaviors.Remove(existingBehavior);
        var behavior = (T) Activator.CreateInstance(typeof(T), blockEntity);
        blockEntity.Behaviors.Add(behavior);
    }
    
    public static void AddBehavior<T>(this BlockEntity blockEntity, T behavior) where T : BlockEntityBehavior
    {
        if (blockEntity is null) throw new ArgumentNullException(nameof(blockEntity));
        if (behavior is null) throw new ArgumentNullException(nameof(behavior));
        var existingBehavior = blockEntity.Behaviors.FirstOrDefault(b => b.GetType() == typeof(T));
        blockEntity.Behaviors.Remove(existingBehavior);
        blockEntity.Behaviors.Add(behavior);
    }
}