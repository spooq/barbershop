using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace Barbershop
{
    public class BlockBehaviorBarberLiquidContainer : BlockBehavior
    {
        public BarbershopModSystem BarbershopModSystem;

        public BlockBehaviorBarberLiquidContainer(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public string GetHairDyeColourCode(ItemStack itemStack)
        {
            var containerBlock = block as BlockLiquidContainerTopOpened;
            if (containerBlock == null)
                return null;

            var itemStackContents = containerBlock.GetContent(itemStack);
            if (itemStackContents == null)
                return null;

            var item = itemStackContents.Item;
            if (item == null || item.FirstCodePart() != "dye")
                return null;

            return item.Variant["color"];
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (entitySel != null && entitySel.Entity is EntityPlayer)
            {
                var variantCode = GetHairDyeColourCode(slot.Itemstack);
                if (!string.IsNullOrEmpty(variantCode))
                {
                    if (byEntity.Api.Side == EnumAppSide.Server)
                    {

                        var ep = (EntityPlayer)entitySel.Entity;
                        BarbershopModSystem.ApplyBarberPropertiesToPlayer(ep.Player as ServerPlayer, BarbershopModSystem.DyeProperties[variantCode]);
                    }

                    handHandling = EnumHandHandling.PreventDefaultAction;
                    handling = EnumHandling.PreventSubsequent;
                    return;
                }
            }
            
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            var variantCode = GetHairDyeColourCode(slot.Itemstack);
            if (!string.IsNullOrEmpty(variantCode))
            {
                if (byEntity.Api.Side == EnumAppSide.Server)
                {
                    var ep = (EntityPlayer)byEntity;
                    BarbershopModSystem.ApplyBarberPropertiesToPlayer(ep.Player as ServerPlayer, BarbershopModSystem.DyeProperties[variantCode]);
                }

                handHandling = EnumHandHandling.PreventDefaultAction;
                handling = EnumHandling.PreventDefault;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }
    }
}