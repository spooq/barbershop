using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Barbershop
{
    public class BlockBehaviorBarberLiquidContainer : BlockBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        public BlockBehaviorBarberLiquidContainer(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandHandling handling)
        {
            if (entitySel != null && entitySel.Entity is EntityPlayer && TryApplyHairDye(entitySel.Entity as EntityPlayer, slot.Itemstack))
            {
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
            else
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (TryApplyHairDye(byEntity as EntityPlayer, slot.Itemstack))
            {
                handHandling = EnumHandHandling.PreventDefaultAction;
                handling = EnumHandling.PreventSubsequent;
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            }
        }

        public bool TryApplyHairDye(EntityPlayer targetPlayer, ItemStack itemStack)
        {
            if (BarbershopModSystem.sapi != null)
                return false;

            var containerBlock = block as BlockLiquidContainerTopOpened;
            if (containerBlock == null)
                return false;

            var itemStackContents = containerBlock.GetContent(itemStack);
            if (itemStackContents == null)
                return false;

            var item = itemStackContents.Item;
            if (item == null || item.FirstCodePart() != "dye")
                return false;

            var variant = item.Variant["color"];
            if (variant == null)
                return false;

            if (!(targetPlayer is EntityPlayer))
                return false;

            BarbershopModSystem.BarberChannel.SendPacket(new BarberDyePacket
            {
                targetUid = targetPlayer.Player.Entity.PlayerUID,
                dyeVariant = variant
            });
            return true;
        }
    }
}