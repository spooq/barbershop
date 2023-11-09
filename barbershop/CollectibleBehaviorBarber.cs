using Vintagestory.API.Common;

namespace Barbershop
{
    public class CollectibleBehaviorBarber : CollectibleBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        public static ICoreAPI api;

        public CollectibleBehaviorBarber(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            CollectibleBehaviorBarber.api = api;

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            var ent = entitySel?.Entity ?? byEntity;
            if (ent != null && ent is EntityPlayer)
            {
                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    var plr = ent as EntityPlayer;
                    var type = BarbershopModSystem.HairBase; // TODO: select which one gets changed
                    BarbershopModSystem?.NextStyleForPart(plr.PlayerUID, type);
                }

                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault;
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            }
        }
    }
}