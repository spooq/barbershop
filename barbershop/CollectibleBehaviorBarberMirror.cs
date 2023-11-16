using Vintagestory.API.Common;

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

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (BarbershopModSystem.sapi != null)
            {
                //BarbershopModSystem.sapi.SendMessage(byEntity as EntityPlayer, worldconst)
            }

            /*
            BarbershopModSystem.Channel.SendPacket(new BarberPacket
            {
                targetUid = (byEntity as EntityPlayer).PlayerUID,
                code = collObj.Code.ToString()
            });
            */

            handHandling = EnumHandHandling.PreventDefaultAction;
            handling = EnumHandling.Handled;
        }
    }
}