using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Barbershop
{
    public class BarberTransforms
    {
        public string part;
        public string from;
        public string to;
    }

    public class BarberProperties
    {
        public List<BarberTransforms> transforms = new List<BarberTransforms>();
    }

    public class CollectibleBehaviorBarber : CollectibleBehavior
    {
        public static BarbershopModSystem BarbershopModSystem;

        public static ICoreAPI api;

        BarberProperties barberProperties = new BarberProperties();

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

            CollectibleBehaviorBarber.api = api;

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            var ent = entitySel?.Entity ?? byEntity;
            if (ent is EntityPlayer)
            {
                handHandling = EnumHandHandling.PreventDefaultAction;

                if (api.Side == EnumAppSide.Server)
                {
                    var plr = ent as EntityPlayer;
                    foreach (var style in barberProperties.transforms)
                        BarbershopModSystem?.TransformPart(plr.PlayerUID, style.part, style.from, style.to);
                    // BarbershopModSystem?.NextStyleForPart(plr.PlayerUID, type);
                }
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            }
        }
        /*
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            var ent = entitySel?.Entity ?? byEntity;

            if (ent != null && ent is EntityPlayer && api.Side == EnumAppSide.Server)
            {
                var plr = ent as EntityPlayer;
                var type = BarbershopModSystem.HairBase; // TODO: select which one gets changed
                BarbershopModSystem?.NextStyleForPart(plr.PlayerUID, type);
            }

            handling = EnumHandling.PreventSubsequent;
            return true;
        }
        */
    }
}