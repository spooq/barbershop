using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Server;

namespace Barbershop
{
    public class CollectibleBehaviorBarberMirror : CollectibleBehavior
    {
        public BarbershopModSystem BarbershopModSystem;

        public BarberProperties barberProperties = new BarberProperties();

        public CollectibleBehaviorBarberMirror(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (entitySel is not null && entitySel.Entity is EntityPlayer)
            {
                if (byEntity.Api.Side == EnumAppSide.Server)
                {
                    var ep = entitySel.Entity as EntityPlayer;
                    var sp = ep.Player as ServerPlayer;
                    sp.SendMessage(GlobalConstants.CurrentChatGroup, BarbershopModSystem.GetScalpString(sp), EnumChatType.OwnMessage);
                    sp.SendMessage(GlobalConstants.CurrentChatGroup, BarbershopModSystem.GetFacialString(sp), EnumChatType.OwnMessage);
                }

                handHandling = EnumHandHandling.PreventDefaultAction;
                handling = EnumHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (byEntity.Api.Side == EnumAppSide.Server)
            {
                var ep = byEntity as EntityPlayer;
                var sp = ep.Player as ServerPlayer;
                sp.SendMessage(GlobalConstants.CurrentChatGroup, BarbershopModSystem.GetScalpString(sp), EnumChatType.OwnMessage);
                sp.SendMessage(GlobalConstants.CurrentChatGroup, BarbershopModSystem.GetFacialString(sp), EnumChatType.OwnMessage);
            }

            handHandling = EnumHandHandling.PreventDefaultAction;
            handling = EnumHandling.PreventDefault;
        }
    }
}