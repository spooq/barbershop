using Newtonsoft.Json;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
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

    public class PlayerBarbershopData
    {
        public bool HasHair = false;
        public bool HasFacialHair = false;
        public Dictionary<string, double> timeSinceEdited;
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

        // Server-side only
        BarberProperties hairGrowth;
        double lastCheckOfElapsedDays;

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

            var hairgrowAsset = sapi.Assets.Get("barbershop:config/hairgrowth.json");
            hairGrowth = JsonConvert.DeserializeObject<BarberProperties>(hairgrowAsset.ToText());

            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnServerRunGame);

            api.Network.GetChannel(Mod.Info.ModID)
                .SetMessageHandler<BarberPacket>(onApplyTransformsFromItem);
        }

        private void OnServerRunGame()
        {
            lastCheckOfElapsedDays = sapi.World.Calendar.ElapsedDays;
            sapi.World.RegisterCallback(OnTimePassed, 1000);
        }

        public void OnTimePassed(float obj)
        {
            var diff = sapi.World.Calendar.ElapsedDays - lastCheckOfElapsedDays;
            if (diff != 0)
            {
                lastCheckOfElapsedDays = sapi.World.Calendar.ElapsedDays;

                foreach (var targetPlayer in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
                {
                    if (targetPlayer.ConnectionState != EnumClientState.Playing)
                        continue;

                    var savedData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
                    if (savedData == null)
                        OnCharacterReset(targetPlayer);

                    bool dirty = false;
                    if (savedData.timeSinceEdited[HairBase] > 0)
                        dirty |= applyOneStepToPart(targetPlayer, HairBase, hairGrowth.hairbase);
                    if (savedData.timeSinceEdited[HairExtra] > 0)
                        dirty |= applyOneStepToPart(targetPlayer, HairExtra, hairGrowth.hairextra);
                    if (savedData.timeSinceEdited[HairColor] > 0)
                        dirty |= applyOneStepToPart(targetPlayer, HairColor, hairGrowth.haircolor);
                    if (savedData.timeSinceEdited[Moustache] > 0 && savedData.HasFacialHair)
                        dirty |= applyOneStepToPart(targetPlayer, Moustache, hairGrowth.moustache);
                    if (savedData.timeSinceEdited[Beard] > 0 && savedData.HasFacialHair)
                        dirty |= applyOneStepToPart(targetPlayer, Beard, hairGrowth.beard);

                    if (dirty)
                    {
                        targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                        targetPlayer.BroadcastPlayerData(false);
                    }
                }
            }

            sapi.World.RegisterCallback(OnTimePassed, 1000);
        }

        public void OnCharacterReset(IServerPlayer byPlayer)
        {
            var playerBehaviour = byPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return;

            var savedata = new PlayerBarbershopData
            {
                timeSinceEdited = new Dictionary<string, double>
            {
                { HairBase, 0 },
                { HairExtra, 0 },
                { HairColor, 0},
                { Moustache, 0 },
                { Beard, 0}
            }
            };

            savedata.HasHair |= GetCurrentStyle(playerBehaviour, HairBase) != "none";
            savedata.HasHair |= GetCurrentStyle(playerBehaviour, HairExtra) != "none";

            savedata.HasFacialHair |= GetCurrentStyle(playerBehaviour, Moustache) != "none";
            savedata.HasFacialHair |= GetCurrentStyle(playerBehaviour, Beard) != "none";

            byPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(savedata));
        }

        public void onApplyTransformsFromItem(IServerPlayer fromPlayer, BarberPacket packet)
        {
            var item = sapi.World?.GetItem(new AssetLocation(packet.code));
            if (item == null)
                return;

            var targetPlayer = sapi.World.PlayerByUid(packet.targetUid) as IServerPlayer;
            if (targetPlayer == null)
                return;

            var itemBehaviour = item.GetCollectibleBehavior<CollectibleBehaviorBarber>(true);
            if (itemBehaviour == null)
                return;

            ApplyBarberProperties(targetPlayer, itemBehaviour.barberProperties);
        }

        private void ApplyBarberProperties(IServerPlayer targetPlayer, BarberProperties barberProperties)
        {
            bool dirty = false;
            dirty |= applyOneStepToPart(targetPlayer, HairBase, barberProperties.hairbase);
            dirty |= applyOneStepToPart(targetPlayer, HairExtra, barberProperties.hairextra);
            dirty |= applyOneStepToPart(targetPlayer, HairColor, barberProperties.haircolor);
            dirty |= applyOneStepToPart(targetPlayer, Moustache, barberProperties.moustache);
            dirty |= applyOneStepToPart(targetPlayer, Beard, barberProperties.beard);
            if (dirty)
            {
                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                targetPlayer.BroadcastPlayerData(false);
            }
        }

        public bool applyOneStepToPart(IServerPlayer targetPlayer, string part, List<BarberTransform> barberTransforms)
        {
            if (barberTransforms == null)
                return false;

            foreach (var tf in barberTransforms)
                if (TransformPart(targetPlayer, part, tf.from, tf.to))
                    return true;

            return false;
        }

        public bool TransformPart(IServerPlayer targetPlayer, string part, string from, string to)
        {
            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return false;

            string currentStyle = GetCurrentStyle(playerBehaviour, part);
            if (string.IsNullOrEmpty(currentStyle) || !WildcardUtil.Match(from, currentStyle))
                return false;

            // Change style
            foreach (var asp in playerBehaviour.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    playerBehaviour.selectSkinPart(asp.Code, to);

                    var savedData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
                    savedData.timeSinceEdited[part] = 0;
                    // Player wants facial hair.
                    if (part == Beard || part == Moustache)
                        savedData.HasFacialHair = true;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(savedData));

                    return true;
                }
            }

            return false;
        }

        public string GetCurrentStyle(EntityBehaviorExtraSkinnable playerBehaviour, string part)
        {
            foreach (var appliedPart in playerBehaviour.AppliedSkinParts)
                if (appliedPart.PartCode == part)
                    return appliedPart.Code;

            return null;
        }
    }
}
