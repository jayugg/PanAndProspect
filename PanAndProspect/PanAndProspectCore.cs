using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PanAndProspect;

[UsedImplicitly]
public partial class PanAndProspectCore : ModSystem
{
    public static ILogger Logger { get; private set; }
    public const string ModId = "panandprospect";
    public static ICoreAPI Api { get; private set; }
    private static Harmony HarmonyInstance { get; set; }
    public static PanAndProspectCore GetInstance(ICoreAPI api) => api.ModLoader.GetModSystem<PanAndProspectCore>();
    
    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        Api = api;
        Logger = Mod.Logger;
        HarmonyInstance = new Harmony(ModId);
        HarmonyInstance.PatchAll();
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockBehaviorClass<StoreProspectsBehavior>(ModId);
        api.RegisterBlockClass<BlockBetterPan>(ModId);
        GlobalConstants.IgnoredStackAttributes = GlobalConstants.IgnoredStackAttributes.AddToArray(Const.Attr.PanningContents);
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        if (!api.Side.IsServer()) return;
        foreach (var block in api.World.Blocks.Where(b=> b?.Code != null))
        {
            if (!block.CanContainProspectInfo()) continue;
            block.AddBehavior<StoreProspectsBehavior>();
        }
    }

    public override void Dispose()
    {
        HarmonyInstance?.UnpatchAll(ModId);
        HarmonyInstance = null;
        Logger = null;
        Api = null;
        base.Dispose();
    }
}