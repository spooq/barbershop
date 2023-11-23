using Vintagestory.API.Common;

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
            if (BarbershopModSystem.sapi != null)
                return;

            if (entitySel != null && entitySel.Entity is EntityPlayer)
            {
                BarbershopModSystem.BarberChannel.SendPacket(new BarberItemPacket
                {
                    targetUid = (byEntity as EntityPlayer).PlayerUID,
                    code = collObj.Code.ToString()
                });

                handHandling = EnumHandHandling.PreventDefaultAction;
                handling = EnumHandHandling.PreventDefault;
            }
            else
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (BarbershopModSystem.sapi != null)
                return;

            if (byEntity != null && byEntity is EntityPlayer)
            {
                BarbershopModSystem.BarberChannel.SendPacket(new BarberItemPacket
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