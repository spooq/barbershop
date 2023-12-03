using Vintagestory.API.Common;
using Vintagestory.Server;

namespace Barbershop
{
    public class CollectibleBehaviorBarber : CollectibleBehavior
    {
        public BarbershopModSystem BarbershopModSystem;

        public BarberProperties barberProperties = new BarberProperties();

        public CollectibleBehaviorBarber(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void Initialize(Vintagestory.API.Datastructures.JsonObject properties)
        {
            base.Initialize(properties);

            barberProperties = properties.AsObject<BarberProperties>();
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (entitySel is not null && entitySel.Entity is EntityPlayer)
            {
                if (byEntity.Api.Side == EnumAppSide.Server)
                {
                    var ep = (EntityPlayer)entitySel.Entity;
                    BarbershopModSystem.ApplyBarberPropertiesToPlayer(ep.Player as ServerPlayer, barberProperties);
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
                var ep = (EntityPlayer)byEntity;
                BarbershopModSystem.ApplyBarberPropertiesToPlayer(ep.Player as ServerPlayer, barberProperties);
            }

            handHandling = EnumHandHandling.PreventDefaultAction;
            handling = EnumHandling.PreventDefault;
        }
    }
}