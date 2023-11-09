using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Barbershop
{
    public class CollectibleBehaviorBarber : CollectibleBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        ICoreAPI api;

        public CollectibleBehaviorBarber(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            this.api = api;

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandHandling handling)
        {
            if (entitySel != null && entitySel.Entity is EntityPlayer)
            {
                handHandling = EnumHandHandling.Handled;

                if (api.Side == EnumAppSide.Server)
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