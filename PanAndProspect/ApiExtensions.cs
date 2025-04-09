using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace PanAndProspect;

public static class ApiExtensions
{
    public static void RegisterEntityBehaviorClass<T>(this ICoreAPI api, string modId) where T : EntityBehavior
    {
        api.RegisterEntityBehaviorClass($"{modId}:{typeof(T).Name}", typeof(T));
    }
    
    public static void RegisterBlockEntityBehaviorClass<T>(this ICoreAPI api, string modId) where T : BlockEntityBehavior
    {
        api.RegisterBlockEntityBehaviorClass($"{modId}:{typeof(T).Name}", typeof(T));
    }
    
    public static void RegisterBlockBehaviorClass<T>(this ICoreAPI api, string modId) where T : BlockBehavior
    {
        api.RegisterBlockBehaviorClass($"{modId}:{typeof(T).Name}", typeof(T));
    }
}