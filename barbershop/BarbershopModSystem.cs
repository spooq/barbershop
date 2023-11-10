using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Barbershop
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BarberPacket
    {
        public string targetUid;
        public string code;
    }

    public class BarbershopModSystem : ModSystem
    {
        public IClientNetworkChannel Channel;

        public ICoreServerAPI sapi;

        public const string HairBase = "hairbase";
        public const string HairExtra = "hairextra";
        public const string HairColor = "haircolor";
        public const string Moustache = "mustache";
        public const string Beard = "beard";

        // Server-side only, holds each part a player has edited. Stored in the world save file.
        // Player name -> part growtime
        public Dictionary<string, Dictionary<string, int>> PlayerEditedParts = new();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterCollectibleBehaviorClass("Barbershop", typeof(CollectibleBehaviorBarber));

            api.Network
                .RegisterChannel(Mod.Info.ModID)
                .RegisterMessageType<BarberPacket>();
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            Channel = api.Network.GetChannel(Mod.Info.ModID);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;

            sapi.Event.SaveGameLoaded += OnSaveGameLoading;
            sapi.Event.GameWorldSave += OnSaveGameSaving;

            api.Network.GetChannel(Mod.Info.ModID)
                .SetMessageHandler<BarberPacket>(onTransformPart);
        }

        private void onTransformPart(IServerPlayer fromPlayer, BarberPacket packet)
        {
            var item = sapi.World.GetItem(new AssetLocation(packet.code));
            if (item != null)
            {
                var targetPlayer = sapi.World.PlayerByUid(fromPlayer.PlayerUID) as IServerPlayer;
                if (targetPlayer == null)
                    return;

                var appliedParts = new List<string>();
                var itemBehaviour = item.GetCollectibleBehavior<CollectibleBehaviorBarber>(true);
                foreach (var tf in itemBehaviour.barberProperties.transforms)
                {
                    if (!appliedParts.Contains(tf.part) && TransformPart(targetPlayer, tf.part, tf.from, tf.to))
                    {
                        appliedParts.Add(tf.part);

                        // Remember what parts players have edited and reset their growtime
                        if (!PlayerEditedParts.ContainsKey(fromPlayer.PlayerUID))
                            PlayerEditedParts[fromPlayer.PlayerUID] = new();
                        PlayerEditedParts[fromPlayer.PlayerUID][tf.part] = 0;
                    }
                }

                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                targetPlayer.BroadcastPlayerData(true);
            }
        }

        public bool TransformPart(IServerPlayer targetPlayer, string part, string from, string to)
        {
            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return false;

            // Find current pieces
            string currentVariant = null;
            foreach (var appliedPart in playerBehaviour.AppliedSkinParts)
            {
                if (appliedPart.PartCode == part)
                {
                    currentVariant = appliedPart.Code;
                    break;
                }
            }
            if (string.IsNullOrEmpty(currentVariant))
                return false;

            if (!WildcardUtil.Match(from, currentVariant))
                return false;

            // Change style
            foreach (var asp in playerBehaviour.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    playerBehaviour.selectSkinPart(asp.Code, to);
                    return true;
                }
            }

            return false;
        }

        public void NextStyleForPart(IServerPlayer targetPlayer, string part)
        {
            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return;

            // Find current pieces
            string currentVariant = null;
            foreach (var appliedPart in playerBehaviour.AppliedSkinParts)
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
            foreach (var asp in playerBehaviour.AvailableSkinParts)
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

                            playerBehaviour.selectSkinPart(asp.Code, variants[nxt]);
                            targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                            targetPlayer.BroadcastPlayerData(false);
                            return;
                        }
                    }
                }
            }
        }

        private void OnSaveGameSaving()
        {
            sapi.WorldManager.SaveGame.StoreData(Mod.Info.ModID, SerializerUtil.Serialize(PlayerEditedParts));
        }

        private void OnSaveGameLoading()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData(Mod.Info.ModID);
            PlayerEditedParts = data == null
                ? new()
                : SerializerUtil.Deserialize<Dictionary<string, Dictionary<string, int>>>(data);
        }
    }
}
