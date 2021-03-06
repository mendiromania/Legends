﻿using Legends.Core;
using Legends.Core.CSharp;
using Legends.Core.DesignPattern;
using Legends.Core.Geometry;
using Legends.Protocol.GameClient.Enum;
using Legends.Protocol.GameClient.Messages.Game;
using Legends.Protocol.GameClient.Types;
using Legends.Records;
using Legends.World;
using Legends.World.Entities;
using Legends.World.Entities.AI;
using Legends.World.Spells;
using Legends.World.Spells.Projectiles;
using Legends.World.Spells.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Legends.Scripts.Spells
{
    public abstract class SpellScript : Script, IUpdatable
    {
        protected AIUnit Owner
        {
            get;
            private set;
        }
        protected SpellRecord SpellRecord
        {
            get;
            private set;
        }
        protected Spell Spell
        {
            get;
            private set;
        }
        public virtual bool DestroyProjectileOnHit
        {
            get
            {
                return false;
            }
        }
        public abstract SpellFlags Flags
        {
            get;
        }
        protected float OwnerBonusAD
        {
            get
            {
                return Owner.Stats.AttackDamage.Total - Owner.Stats.AttackDamage.BaseValue;
            }
        }
        protected float OwnerADTotal
        {
            get
            {
                return Owner.Stats.AttackDamage.TotalSafe;
            }
        }
        protected float OwnerBonusAP
        {
            get
            {
                return Owner.Stats.AbilityPower.Total - Owner.Stats.AbilityPower.BaseValue;
            }
        }
        protected float OwnerAPTotal
        {
            get
            {
                return Owner.Stats.AbilityPower.TotalSafe;
            }
        }
        public SpellScript(AIUnit owner, SpellRecord spellRecord)
        {
            this.Owner = owner;
            this.SpellRecord = spellRecord;
        }
        public void Bind(Spell spell)
        {
            this.Spell = spell;
        }

        public virtual void Update(float deltaTime)
        {

        }
        public abstract void OnStartCasting(Vector2 position, Vector2 endPosition, AttackableUnit target);

        public abstract void OnFinishCasting(Vector2 position, Vector2 endPosition, AttackableUnit target);

        protected void AddCone(Vector2 coneEnd, float angleDeg)
        {
            Cone cone = new Cone(Owner.Position, coneEnd, angleDeg);

            foreach (var unit in GetTargets())
            {
                if (cone.TargetIsInCone(unit))
                {
                    ApplyEffects(unit, cone);
                }
            }
        }
        protected void AddSkillShot(string name, Vector2 toPosition, Vector2 endPosition, float range, bool serverOnly = false)
        {
            var record = SpellRecord.GetSpell(name);
            var position = new Vector3(Owner.Position.X, Owner.Position.Y, 100);
            var casterPosition = new Vector3(Owner.Position.X, Owner.Position.Y, 100);
            var angle = Geo.GetAngle(Owner.Position, endPosition);
            var direction = Geo.GetDirection(angle);
            var velocity = new Vector3(1, 1, 1) * 100;
            var startPoint = new Vector3(Owner.Position.X, Owner.Position.Y, 100);
            var endPoint = new Vector3(endPosition.X, endPosition.Y, 100);


            var skillShot = new SkillShot(Owner.Game.NetIdProvider.PopNextNetId(),
                Owner, position.ToVector2(), record.MissileSpeed, record.LineWidth,
                OnProjectileReach, direction, range, OnSkillShotRangeReached);


            Owner.Game.AddUnitToTeam(skillShot, Owner.Team.Id);
            Owner.Game.Map.AddUnit(skillShot);

            if (!serverOnly)
            {
                var castInfo = Spell.GetCastInformations(position, endPoint, name, skillShot.NetId);

                Owner.Game.Send(new SpawnProjectileMessage(skillShot.NetId, position, casterPosition,
                    direction.ToVector3(), velocity, startPoint, endPoint, casterPosition,
                    0, record.MissileSpeed, 1f, 1f, 1f, false, castInfo));
            }

        }
        protected void AddTargetedProjectile(string name, AttackableUnit target, bool serverOnly = false)
        {
            var record = SpellRecord.GetSpell(name);
            var position = new Vector3(Owner.Position.X, Owner.Position.Y, 100);
            var casterPosition = new Vector3(Owner.Position.X, Owner.Position.Y, 100);
            var angle = Geo.GetAngle(Owner.Position, target.Position);
            var direction = Geo.GetDirection(angle);
            var velocity = new Vector3(1, 1, 1) * 100;
            var startPoint = new Vector3(Owner.Position.X, Owner.Position.Y, 100);
            var endPoint = new Vector3(target.Position.X, target.Position.Y, 100);


            var targetedProjectile = new TargetedProjectile(Owner.Game.NetIdProvider.PopNextNetId(),
                Owner, target, startPoint.ToVector2(), SpellRecord.MissileSpeed, OnProjectileReach);

            Owner.Game.AddUnitToTeam(targetedProjectile, Owner.Team.Id);
            Owner.Game.Map.AddUnit(targetedProjectile);

            if (!serverOnly)
            {
                var castInfo = Spell.GetCastInformations(position, endPoint, name, targetedProjectile.NetId, new AttackableUnit[] { target });

                Owner.Game.Send(new SpawnProjectileMessage(targetedProjectile.NetId, position, casterPosition,
                    direction.ToVector3(), velocity, startPoint, endPoint, casterPosition,
                    0, record.MissileSpeed, 1f, 1f, 1f, false, castInfo));
            }

        }

        public void DestroyProjectile(Projectile projectile, bool notify)
        {
            Owner.Game.DestroyUnit(projectile);
            if (notify)
                Owner.Game.Send(new DestroyClientMissile(projectile.NetId));
        }
        public void AddParticle(string effectName, string bonesName, float size)
        {
            Owner.SendVision(new FXCreateGroupMessage(Owner.NetId, new FXCreateGroupData[]
            {
                new FXCreateGroupData()
                {
                    BoneNameHash= bonesName.HashString(),
                    PackageHash = Owner.Record.Name.HashString(),
                    EffectNameHash=effectName.HashString(),
                    Flags= 0,
                    TargetBoneNameHash=  0,
                    FXCreateData=  new List<FXCreateData>()
                    {
                        new FXCreateData()
                        {
                            BindNetId= Owner.NetId,
                            CasterNetId=  Owner.NetId,
                            KeywordNetId= Owner.NetId,
                            NetAssignedNetId =Owner.Game.NetIdProvider.PopNextNetId(),
                            OrientationVector= new Vector3(),
                            OwnerPositionX=Owner.Cell.X,
                            OwnerPositionY=  Owner.Position.Y,
                            OwnerPositionZ=  0,
                            PositionX=  Owner.Cell.X,
                            PositionY= Owner.Position.Y,
                            PositionZ= 0,
                            ScriptScale= size,// taille
                            TargetNetId=Owner.NetId,
                            TargetPositionX= (short)Owner.Position.X,
                            TargetPositionY=  Owner.Position.Y,
                            TargetPositionZ= 0,
                            TimeSpent= 0.0f, // ou on en est?

                        }
                    }

                }

            }));
        }
        protected AttackableUnit[] GetTargets()
        {
            return Owner.Game.Map.Units.OfType<AttackableUnit>().Where(x => IsAffected(x)).ToArray();
        }
        [InDevelopment]
        private bool IsAffected(AttackableUnit unit)
        {
            var flags = Flags; // SpellRecord.Flags?

            if (!flags.HasFlag(SpellFlags.AffectAllSides))
            {
                if (!unit.IsFriendly(Owner) && !flags.HasFlag(SpellFlags.AffectEnemies))
                {
                    return false;
                }
                if (unit.IsFriendly(Owner) && !flags.HasFlag(SpellFlags.AffectFriends))
                {
                    return false;
                }
                if (unit.Team.Id == TeamId.NEUTRAL && flags.HasFlag(SpellFlags.AffectNeutral))
                {
                    return true;
                }
            }
            if (flags.HasFlag(SpellFlags.AffectAllUnitTypes))
            {
                return true;
            }
            if (unit is AITurret)
            {
                return flags.HasFlag(SpellFlags.AffectTurrets);
            }
            if (unit is AIMinion)
            {
                return flags.HasFlag(SpellFlags.AffectMinions);
            }
            if (unit is AIHero)
            {
                return flags.HasFlag(SpellFlags.AffectHeroes);
            }
            return false;
        }
        public virtual void OnProjectileReach(AttackableUnit target, Projectile projectile)
        {
            if (IsAffected(target))
            {
                ApplyEffects(target, projectile);

                if (DestroyProjectileOnHit)
                {
                    DestroyProjectile(projectile, true);
                }
            }
        }
        public abstract void ApplyEffects(AttackableUnit target, IMissile projectile);

        public virtual void OnSkillShotRangeReached(SkillShot skillShot)
        {
            DestroyProjectile(skillShot, false);
        }
        protected void Teleport(Vector2 position)
        {
            Owner.Teleport(position);
        }
    }
}
