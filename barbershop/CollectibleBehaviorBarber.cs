using Microsoft.Win32.SafeHandles;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

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
            //handling = EnumHandling.PreventSubsequent;
            handHandling = EnumHandHandling.PreventDefault;
            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            var ent = entitySel?.Entity ?? byEntity;
            if (api.Side == EnumAppSide.Server)
            {
                var a = 1;
            }
            if (ent != null && ent is EntityPlayer && api.Side == EnumAppSide.Server)
            {
                var plr = ent as EntityPlayer;
                var type = BarbershopModSystem.HairBase; // TODO: select which one gets changed
                BarbershopModSystem?.NextStyleForPart(plr.PlayerUID, type);
            }

            handling = EnumHandling.PreventSubsequent;
            return true;
        }
    }
}