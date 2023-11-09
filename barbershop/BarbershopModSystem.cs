using ProtoBuf;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Barbershop
{
    public class BarbershopModSystem : ModSystem
    {
        public IClientNetworkChannel Channel;

        public ICoreClientAPI capi;
        public ICoreServerAPI sapi;

        public const string HairBase = "hairbase";
        public const string HairExtra = "hairextra";
        public const string HairColor = "haircolor";
        public const string Moustache = "mustache";
        public const string Beard = "beard";

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterCollectibleBehaviorClass("Barbershop", typeof(CollectibleBehaviorBarber));
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            Channel = api.Network.GetChannel(Mod.Info.ModID);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;
        }

        public void TransformPart(string targetUid, string part, string from, string to)
        {
            var targetPlayer = sapi.World.PlayerByUid(targetUid) as IServerPlayer;
            if (targetPlayer == null)
                return;

            var bh = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (bh == null)
                return;

            // Find current pieces
            string currentVariant = null;
            foreach (var appliedPart in bh.AppliedSkinParts)
            {
                if (appliedPart.PartCode == part)
                {
                    currentVariant = appliedPart.Code;
                    break;
                }
            }
            if (string.IsNullOrEmpty(currentVariant))
                return;

            if (!WildcardUtil.Match(currentVariant, from))
                return;

            // Change style
            foreach (var asp in bh.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    bh.selectSkinPart(asp.Code, to);
                    targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                    targetPlayer.BroadcastPlayerData(false);
                    return;
                }
            }
        }

        public void NextStyleForPart(string targetUid, string part)
        {
            var targetPlayer = sapi.World.PlayerByUid(targetUid) as IServerPlayer;
            if (targetPlayer == null)
                return;

            var bh = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (bh == null)
                return;

            // Find current pieces
            string currentVariant = null;
            foreach (var appliedPart in bh.AppliedSkinParts)
            {
                if (appliedPart.PartCode == part)
                {
                    currentVariant = appliedPart.Code;
                    break;
                }
            }
            if (string.IsNullOrEmpty(currentVariant))
                return;

            // Increment style
            foreach (var asp in bh.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    var variants = asp.VariantsByCode.Keys.ToArray();
                    for (int i = 0; i < variants.Length; i++)
                    {
                        if (variants[i] == currentVariant)
                        {
                            int nxt = i + 1;
                            if (nxt == variants.Length)
                                nxt = 0;

                            bh.selectSkinPart(asp.Code, variants[nxt]);
                            targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                            targetPlayer.BroadcastPlayerData(false);
                            return;
                        }
                    }
                }
            }
        }
    }
}
