using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PanAndProspect;

public class BlockBetterPan : Block, ITexPositionSource
{
    private const string FilledBlockFirstCodePart = "pan-filled-";
    public Size2i AtlasSize { get; set; }
    private ITexPositionSource _ownTextureSource;
    private TextureAtlasPosition _matTexPosition;
    private ILoadedSound _sound;
    private Dictionary<string, PanningDrop[]> _dropsBySourceMat;
    private WorldInteraction[] _interactions;

    public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
    {
        var blockMatCode = GetBlockMaterialCode(activeHotbarSlot.Itemstack);
        return blockMatCode == null ? null : base.GetHeldTpUseAnimation(activeHotbarSlot, forEntity);
    }


    public override void OnLoaded(ICoreAPI coreApi)
    {
        base.OnLoaded(coreApi);

        _dropsBySourceMat = Attributes["panningDrops"].AsObject<Dictionary<string, PanningDrop[]>>();
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var drops in _dropsBySourceMat.Values)
        {
            foreach (var drop in drops)
            {
                if (drop.Code.Path.Contains("{rocktype}")) continue;
                drop.Resolve(coreApi.World, "panningdrop");
            }
        }
        if (coreApi is not ICoreClientAPI capi) return;
        _interactions = ObjectCacheUtil.GetOrCreate(coreApi, "panInteractions", () =>
        {
            var stacks = (from block in coreApi.World.Blocks
                where !block.IsMissing
                where block.CreativeInventoryTabs != null && block.CreativeInventoryTabs.Length != 0
                where IsPannableMaterial(block)
                select new ItemStack(block))
                .ToList();

            var stacksArray = stacks.ToArray();
            return new[]
            {
                new()
                {
                    ActionLangCode = "heldhelp-addmaterialtopan",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = stacks.ToArray(),
                    GetMatchingStacks = (wi, bs, es) => {
                        var stack = capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack;
                        return GetBlockMaterialCode(stack) == null ? stacksArray : null;
                    },
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-pan",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        var stack = capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack;
                        return GetBlockMaterialCode(stack) != null;
                    }
                }
            };
        });
    }
    
    private ItemStack Resolve(EnumItemClass type, string code)
    {
        if (type == EnumItemClass.Block)
        {
            var block = api.World.GetBlock(new AssetLocation(code));
            if (block != null) return new ItemStack(block);
            api.World.Logger.Error("Failed resolving panning block drop with code {0}. Will skip.", code);
            return null;
        }
        var item = api.World.GetItem(new AssetLocation(code));
        if (item != null) return new ItemStack(item);
        api.World.Logger.Error("Failed resolving panning item drop with code {0}. Will skip.", code);
        return null;
    }

    public TextureAtlasPosition this[string textureCode] => 
        textureCode == "material" ? _matTexPosition : _ownTextureSource[textureCode];

    private static string GetBlockMaterialCode(ItemStack stack)
    {
        return stack?.Attributes?.GetString("materialBlockCode", null);
    }

    private static void SetMaterial(ItemSlot slot, Block block)
    {
        slot.Itemstack.Attributes.SetString("materialBlockCode", block.Code.ToShortString());
    }

    private static void RemoveMaterial(ItemSlot slot)
    {
        slot.Itemstack.Attributes.RemoveAttribute("materialBlockCode");
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        var blockMaterialCode = GetBlockMaterialCode(itemstack);
        if (blockMaterialCode == null) return;
        var key = FilledBlockFirstCodePart + blockMaterialCode + target;
        renderinfo.ModelRef = ObjectCacheUtil.GetOrCreate(capi, key, () =>
        {
            var shapeloc = new AssetLocation("shapes/block/wood/pan/filled.json");
            var shape = Vintagestory.API.Common.Shape.TryGet(capi, shapeloc);
            MeshData meshdata;
            var block = capi.World.GetBlock(new AssetLocation(blockMaterialCode));
            AtlasSize = capi.BlockTextureAtlas.Size;
            _matTexPosition = capi.BlockTextureAtlas.GetPosition(block, "up");
            _ownTextureSource = capi.Tesselator.GetTextureSource(this);
            capi.Tesselator.TesselateShape("filledpan", shape, out meshdata, this);
            return capi.Render.UploadMultiTextureMesh(meshdata);
        });
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
        if (!firstEvent)
            return;
        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null) return;
        if (blockSel != null &&
            !byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            return;
        var blockMatCode = GetBlockMaterialCode(slot.Itemstack);
        if (!byEntity.FeetInLiquid && api is ICoreClientAPI capi && blockMatCode != null)
        {
            capi.TriggerIngameError(this, "notinwater", Lang.Get("ingameerror-panning-notinwater"));
            return;
        }
        if (blockMatCode == null && blockSel != null)
        {
            TryTakeMaterial(slot, byEntity, blockSel.Position);
            slot.Itemstack.TempAttributes.SetBool("canpan", false);
            return;
        }
        if (blockMatCode != null)
            slot.Itemstack.TempAttributes.SetBool("canpan", true);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        // Cancel if the player begins walking
        if ((byEntity.Controls.TriesToMove ||
             byEntity.Controls.Jump) && !byEntity.Controls.Sneak)
            return false; 
        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null) return false;
        if (blockSel != null &&
            !byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            return false;
        var blockMaterialCode = GetBlockMaterialCode(slot.Itemstack);
        if (blockMaterialCode == null || !slot.Itemstack.TempAttributes.GetBool("canpan")) return false;
        var pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
        pos.Y += byEntity.LocalEyePos.Y - 0.4f;
        if (secondsUsed > 0.5f && api.World.Rand.NextDouble() > 0.5)
        {
            var block = api.World.GetBlock(new AssetLocation(blockMaterialCode));
            var particlePos = pos.Clone();
            particlePos.X += GameMath.Sin(-secondsUsed * 20) / 5f;
            particlePos.Z += GameMath.Cos(-secondsUsed * 20) / 5f;
            particlePos.Y -= 0.07f;
            byEntity.World.SpawnCubeParticles(particlePos, new ItemStack(block), 0.3f, (int)(1.5f + (float)api.World.Rand.NextDouble()), 0.3f + (float)api.World.Rand.NextDouble()/6f, (byEntity as EntityPlayer)?.Player);
        }
        if (byEntity.World is not IClientWorldAccessor) return true;
        var tf = new ModelTransform();
        tf.EnsureDefaultValues();
        tf.Origin.Set(0f, 0, 0f);
        if (secondsUsed > 0.5f)
        {
            tf.Translation.X = Math.Min(0.25f, GameMath.Cos(10 * secondsUsed) / 4f);
            tf.Translation.Y = Math.Min(0.15f, GameMath.Sin(10 * secondsUsed) / 6.666f);
            if (_sound == null)
            {
                _sound = (api as ICoreClientAPI)?.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/player/panning.ogg"),
                    ShouldLoop = false,
                    RelativePosition = true,
                    Position = new Vec3f(),
                    DisposeOnFinish = true,
                    Volume = 0.5f,
                    Range = 8
                });
                _sound?.Start();
            }
        }
        tf.Translation.X -= Math.Min(1.6f, secondsUsed * 4 * 1.57f);
        tf.Translation.Y -= Math.Min(0.1f, secondsUsed * 2);
        tf.Translation.Z -= Math.Min(1f, secondsUsed * 180);
        tf.Scale = 1 + Math.Min(0.6f, 2 * secondsUsed);
        byEntity.Controls.UsingHeldItemTransformAfter = tf;
        return secondsUsed <= 4f;
    }


    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        if (cancelReason == EnumItemUseCancelReason.ReleasedMouse)
            return false;
        if (api.Side != EnumAppSide.Client) return true;
        _sound?.Stop();
        _sound = null;
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        _sound?.Stop();
        _sound = null;
        if (!(secondsUsed >= 3.4f)) return;
        var code = GetBlockMaterialCode(slot.Itemstack);
        if (api.Side == EnumAppSide.Server && code != null)
        {
            CreateDrop(byEntity, code);
        }
        RemoveMaterial(slot);
        slot.MarkDirty();
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(4f);
    }

    private void CreateDrop(EntityAgent byEntity, string fromBlockCode)
    {
        var player = (byEntity as EntityPlayer)?.Player;
        PanningDrop[] drops = null;
        foreach (var val in _dropsBySourceMat.Keys)
        {
            if (WildcardUtil.Match(val, fromBlockCode)) {
                drops = _dropsBySourceMat[val];
            }
        }
        if (drops == null)
            throw new InvalidOperationException("Coding error, no drops defined for source mat " + fromBlockCode);
        var rocktype = api.World.GetBlock(new AssetLocation(fromBlockCode))?.Variant["rock"];
        drops.Shuffle(api.World.Rand);
        foreach (var drop in drops)
        {
            var rnd = api.World.Rand.NextDouble();
            var extraMul = 1f;
            if (drop.DropModbyStat != null)
            {
                // If the stat does not exist, then GetBlended returns 1 \o/
                extraMul = byEntity.Stats.GetBlended(drop.DropModbyStat);
            }
            var val = drop.Chance.nextFloat() * extraMul;
            var stack = drop.ResolvedItemstack;
            if (drop.Code.Path.Contains("{rocktype}"))
                stack = Resolve(drop.Type, drop.Code.Path.Replace("{rocktype}", rocktype));
            if (!(rnd < val) || stack == null) continue;
            stack = stack.Clone();
            if (player == null || !player.InventoryManager.TryGiveItemstack(stack, true))
                api.World.SpawnItemEntity(stack, byEntity.ServerPos.XYZ);
            break;
        }
    }
    
    protected virtual bool IsPannableMaterial(Block block)
    {
        return block.Attributes?.IsTrue("pannable") == true;
    }
    
    protected virtual void TryTakeMaterial(ItemSlot slot, EntityAgent byEntity, BlockPos position)
    {
        var block = api.World.BlockAccessor.GetBlock(position);
        if (!IsPannableMaterial(block)) return;
        if (api.World.BlockAccessor.GetBlock(position.UpCopy()).Id != 0)
        {
            if (api is ICoreClientAPI capi)
            {
                capi.TriggerIngameError(this, "noair", Lang.Get("ingameerror-panning-requireairabove"));
            }
            return;
        }
        var layer = block.Variant["layer"];
        if (layer != null)
        {
            var baseCode = block.FirstCodePart() + "-" + block.FirstCodePart(1);
            var origblock = api.World.GetBlock(new AssetLocation(baseCode));
            SetMaterial(slot, origblock);
            if (layer == "1")
            {
                api.World.BlockAccessor.SetBlock(0, position);
            } else
            {
                var code = block.CodeWithVariant("layer", "" + (int.Parse(layer) - 1));
                var reducedBlock = api.World.GetBlock(code);
                api.World.BlockAccessor.SetBlock(reducedBlock.BlockId, position);
            }
            api.World.BlockAccessor.TriggerNeighbourBlockUpdate(position);

        }
        else
        {
            Block reducedBlock;
            var pannedBlock = block.Attributes["pannedBlock"].AsString();
            if (pannedBlock != null)
                reducedBlock = api.World.GetBlock(AssetLocation.Create(pannedBlock, block.Code.Domain));
            else
                reducedBlock = api.World.GetBlock(block.CodeWithVariant("layer", "7"));
            if (reducedBlock != null)
            {
                SetMaterial(slot, block);
                api.World.BlockAccessor.SetBlock(reducedBlock.BlockId, position);
                api.World.BlockAccessor.TriggerNeighbourBlockUpdate(position);
            }
            else
                PanAndProspectCore.Logger.Warning("Missing \"pannedBlock\" attribute for pannable block " + block.Code.ToShortString());
        }
        slot.MarkDirty();
    }


    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return _interactions.Append(base.GetHeldInteractionHelp(inSlot));
    }
}