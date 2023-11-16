using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Barbershop
{
    public class CollectibleBehaviorBarber : CollectibleBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        public BarberProperties barberProperties = new BarberProperties();

        public CollectibleBehaviorBarber(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(Vintagestory.API.Datastructures.JsonObject properties)
        {
            base.Initialize(properties);

            barberProperties = properties.AsObject<BarberProperties>();
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandHandling handling)
        {
            if (entitySel != null && entitySel != null && entitySel.Entity is EntityPlayer)
            {
                BarbershopModSystem.ItemChannel.SendPacket(new BarberPacket
                {
                    targetUid = (byEntity as EntityPlayer).PlayerUID,
                    code = collObj.Code.ToString()
                });
            }
            else
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (byEntity != null && byEntity is EntityPlayer)
            {
                BarbershopModSystem.ItemChannel.SendPacket(new BarberPacket
                {
                    targetUid = (byEntity as EntityPlayer).PlayerUID,
                    code = collObj.Code.ToString()
                });
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            }
        }
    }
}