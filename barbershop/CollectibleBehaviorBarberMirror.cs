using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Barbershop
{
    public class CollectibleBehaviorBarberMirror : CollectibleBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        public BarberProperties barberProperties = new BarberProperties();

        public CollectibleBehaviorBarberMirror(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandHandling handling)
        {
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (BarbershopModSystem.sapi != null && byEntity is EntityPlayer)
            {
                IServerPlayer player = (IServerPlayer)(byEntity as EntityPlayer).Player;
                if (player == null)
                    return;
            }

            handHandling = EnumHandHandling.PreventDefaultAction;
            handling = EnumHandling.Handled;
        }
    }
}