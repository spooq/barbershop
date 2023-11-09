using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Barbershop
{
    public class ItemBarberComb : Item
    {
        public BarbershopModSystem BarbershopModSystem;

        public static SimpleParticleProperties particles = new SimpleParticleProperties(
            1, 1,
            ColorUtil.ToRgba(50, 220, 220, 220),
            new Vec3d(),
            new Vec3d(),
            new Vec3f(-0.25f, 0.1f, -0.25f),
            new Vec3f(0.25f, 0.1f, 0.25f),
            1.5f,
            -0.075f,
            0.25f,
            0.25f,
            EnumParticleModel.Quad
        );

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BarbershopModSystem = api.ModLoader.GetModSystem<BarbershopModSystem>();
        }

        /*
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var ent = entitySel?.Entity ?? byEntity;
            if (ent is EntityPlayer)
                handling = EnumHandHandling.PreventDefaultAction;
            else
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            var ent = entitySel?.Entity ?? byEntity;

            if (ent != null && ent is EntityPlayer && api.Side == EnumAppSide.Server)
            {
                var plr = ent as EntityPlayer;
                var type = BarbershopModSystem.HairBase; // TODO: select which one gets changed
                BarbershopModSystem?.NextStyleForPart(plr.PlayerUID, type);
            }

            return true;
        }*/
    }
}
