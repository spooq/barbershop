using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Barbershop
{
    public class CollectibleBehaviorBarber : CollectibleBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        ICoreServerAPI sapi;

        public CollectibleBehaviorBarber(CollectibleObject collObj) : base(collObj)
        {
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreServerAPI)
                sapi = (ICoreServerAPI)api;
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandHandling handling)
        {
            if (entitySel != null && entitySel.Entity is EntityPlayer)
            {
                handHandling = EnumHandHandling.Handled;

                if (sapi != null)
                {
                    var plr = entitySel.Entity as EntityPlayer;
                    var type = BarbershopModSystem.HairBase; // TODO: select which one gets changed
                    BarbershopModSystem.NextStyleForPart(plr.PlayerUID, type);
                }
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        }
    }
}