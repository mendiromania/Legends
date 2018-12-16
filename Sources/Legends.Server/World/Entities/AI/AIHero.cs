﻿using Legends.Configurations;
using Legends.Protocol.GameClient.Enum;
using Legends.Protocol.GameClient.Messages.Game;
using Legends.Network;
using Legends.Records;
using Legends.World.Champions;
using Legends.World.Games;
using Legends.World.Games.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Legends.Core.Protocol;
using Legends.Core.Utils;
using ENet;
using Legends.World.Entities.Statistics;
using Legends.Core.DesignPattern;
using Legends.World.Entities.Statistics.Replication;
using Legends.Protocol.GameClient.Other;
using Legends.World.Entities.AI.Deaths;
using Legends.World.Items;
using Legends.World.Spells;

namespace Legends.World.Entities.AI
{
    public class AIHero : AIUnit
    {
        public const float DEFAULT_START_GOLD = 475;
        public const float DEFAULT_COOLDOWN_REDUCTION = 0f;

        public LoLClient Client
        {
            get;
            private set;
        }

        public int PlayerNo
        {
            get;
            set;
        }
        public PlayerData Data
        {
            get;
            private set;
        }

        public bool ReadyToSpawn
        {
            get;
            set;
        }
        public bool ReadyToStart
        {
            get;
            set;
        }
        public Champion Champion
        {
            get;
            private set;
        }
        public Score Score
        {
            get;
            private set;
        }
        public bool Disconnected
        {
            get;
            private set;
        }
        public override string Name => Data.Name;

        public override float PerceptionBubbleRadius => ((HeroStats)Stats).PerceptionBubbleRadius.TotalSafe;

        public override bool DefaultAutoattackActivated => false;

        public override bool AddFogUpdate => false;

        private HeroDeath Death
        {
            get;
            set;
        }

        public AIHero(LoLClient client, PlayerData data, AIUnitRecord record) : base(0, record)
        {
            Client = client;
            Data = data;
            Disconnected = false;
        }

        public override void Initialize()
        {
            Champion = ChampionProvider.Instance.GetChampion(this, (ChampionEnum)Enum.Parse(typeof(ChampionEnum), Data.ChampionName));
            Stats = new HeroStats(Record, Data.SkinId);
            Model = Data.ChampionName;
            Death = new HeroDeath(this);
            SkinId = Data.SkinId;
            Score = new Score();
            base.Initialize();
        }
        [InDevelopment(InDevelopmentState.TODO, "Skill points")]
        public void AddExperience(float value)
        {
            int oldLevel = Stats.Level;
            Stats.AddExperience(value);

            if (Stats.Level > oldLevel)
            {
                int offset = Stats.Level - oldLevel;
                Stats.SkillPoints += (byte)offset;
                Stats.Health.BaseBonus += (float)Record.HpPerLevel * offset;
                Stats.Mana.BaseBonus += (float)Record.MpPerLevel * offset;
                Stats.HealthRegeneration.BaseBonus += (float)Record.HpRegenPerLevel * offset;
                Stats.ManaRegeneration.BaseBonus += (float)Record.MPRegenPerLevel * offset;
                Stats.AttackDamage.BaseBonus += (float)Record.DamagePerLevel * offset;
                Stats.Armor.BaseBonus += (float)Record.ArmorPerLevel * offset;
                Stats.AbilityPower.BaseBonus += (float)Record.AbilityPowerIncPerLevel * offset;
                Stats.AttackSpeed.BaseBonus += (float)(Record.AttackSpeedPerLevel / 100) * offset;
                Stats.CriticalHit.BaseBonus += (float)Record.CritPerLevel * offset;
                Stats.MagicResistance.BaseBonus += (float)Record.MagicResistPerLevel * offset;
            }

            Game.Send(new LevelUpMessage(NetId, (byte)Stats.Level, Stats.SkillPoints)); // tdoo
            UpdateStats();
        }

        public override void OnItemAdded(Item item)
        {
            Game.Send(new BuyItemAnswerMessage(NetId, item.Id, item.Slot, item.Stacks, (byte)0x29));
        }
        public override void OnItemRemoved(Item item)
        {
            Game.Send(new InventoryRemoveItemMessage(NetId, item.Slot, 0));
        }
        public override void OnDead(AttackableUnit source) // we override base
        {
            Stats.Health.Current = 0;
            Stats.Mana.Current = 0;
            UpdateStats();
            Alive = false;
            Score.DeathCount++;
            Death.OnDead();
            Game.Send(new ChampionDieMessage(500, NetId, source.NetId, Death.TimeLeftSeconds));
            Game.UnitAnnounce(UnitAnnounceEnum.Death, NetId, source.NetId, new uint[0]);
            Client.Send(new ChampionDeathTimerMessage(NetId, Death.TimeLeftSeconds));
            base.OnDead(source);
        }
        public override void OnSpellUpgraded(byte spellId, Spell targetSpell)
        {
            Client.Send(new SkillUpResponseMessage(NetId, spellId, targetSpell.Level, Stats.SkillPoints));
        }
        public override void OnRevive(AttackableUnit source)
        {
            base.OnRevive(source);
            Stats.Health.Current = Stats.Health.TotalSafe;
            Stats.Mana.Current = Stats.Mana.TotalSafe;

            Position = SpawnPosition;
            Game.Send(new ChampionRespawnMessage(NetId, Position));
            UpdateStats();
        }

        public void DebugMessage(string content)
        {
            Client.Send(new DebugMessage(NetId, content));
        }
        public override void Update(long deltaTime)
        {
            base.Update(deltaTime);
            Death.Update(deltaTime);
        }

        public override void OnMove()
        {
            base.OnMove();
        }
        public override void OnUnitEnterVision(Unit unit)
        {

        }
        public override void OnUnitLeaveVision(Unit unit)
        {

        }
        public void UpdateInfos()
        {
            Game.Send(new PlayerInfoMessage(NetId, Data.Summoner1Spell, Data.Summoner2Spell));
        }
        public void AttentionPing(Vector2 position, uint targetNetId, PingTypeEnum pingType)
        {
            Team.Send(new AttentionPingAnswerMessage(position, targetNetId, NetId, pingType));
        }

        public void SetAutoattackOption(bool automatic)
        {
            AttackManager.SetAutoattackActivated(automatic);
        }
        [InDevelopment(InDevelopmentState.STARTED)]
        public void OnDisconnect()
        {
            Disconnected = true;
            Game.RemoveUnit(this); // maybe depend of reconnect system
            Game.UnitAnnounce(UnitAnnounceEnum.SummonerLeft, NetId, 0, new uint[0]);
        }


    }
}