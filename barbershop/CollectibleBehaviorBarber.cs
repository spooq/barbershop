using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Barbershop
{
    public class BarberTransform
    {
        public string from;
        public string to;
        public double overrideGrowthTime = 0.0;
    }

    public class BarberProperties
    {
        public string target = ""; // TODO: remove this in favour of left/right click
        public List<BarberTransform> hairbase = new();
        public List<BarberTransform> hairextra = new();
        public List<BarberTransform> beard = new();
        public List<BarberTransform> mustache = new();
        public List<BarberTransform> haircolor = new();
    }

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

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            Entity targetEntity = entitySel?.Entity ?? byEntity; //both is default
            if (barberProperties.target == "self")
                targetEntity = byEntity;
            else if (barberProperties.target == "other")
                targetEntity = entitySel?.Entity;

            if (targetEntity != null && targetEntity is EntityPlayer)
            {
                BarbershopModSystem.Channel.SendPacket(new BarberPacket
                {
                    targetUid = (targetEntity as EntityPlayer).PlayerUID,
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