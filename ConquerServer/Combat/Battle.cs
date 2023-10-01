﻿using ConquerServer.Client;
using ConquerServer.Database.Models;
using ConquerServer.Network;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConquerServer.Combat
{
    public partial class Battle
    {
        private static MagicDispatcher c_Magic;
        static Battle()
        {
            c_Magic = new MagicDispatcher();
        }
        //source, target, skill/magic/lackof
        public GameClient Source { get; set; }
        public HashSet<GameClient> Targets { get; set; }

        public MagicTypeModel? Spell { get; set; }
        public int CastX { get; private set; } //coordinate clicked to cast skill
        public int CastY { get; private set; }

        public Battle(GameClient source, GameClient target, int castX=0, int castY=0, MagicTypeModel? spell = null)
        {
            Source = source;
            Targets = new HashSet<GameClient>();
            if (target != null)
                Targets.Add(target);
            Spell = spell;
            CastX = castX;
            CastY = castY;
        }

        public async Task Start()
        {
            if (Spell == null)
            {
                await StartMelee();
            }
            else
            {
                await StartMagic();
            }
        }

        private async Task StartMelee()
        {

        }

        private async Task StartMagic()
        {
            if (Spell == null)
                return;

            if (CastX == 0 && CastY == 0 && Targets.Count > 0)
            {
                var firstTarget = Targets.First();
                CastX = firstTarget.X;
                CastY = firstTarget.Y;
            }

            var findTargets = c_Magic[Spell.Sort];
            if (findTargets == null)
            {
                Source.SendSystemMessage("Spell requested failed, magic type not handled");
                return;
            }
            
            // find elligible targets to be hit
            findTargets(this);
            // filter these targets
            FilterTargets();
            // process doing the damage now to them

            ProcessTargets();
        }

        private bool IsPotentialTarget(GameClient target)
        {
            if (target.Id == Source.Id) return false; // cannot hit yourself

            if (target.Health <= 0) return false; // cannot hit dead

            if (Source.PKMode == PKMode.Peace) 
            {
                return false; //hitting no one
            }
            else if(Source.PKMode == PKMode.Capture)
            {
                //TO-DO: implement nameflashing(datetime??), black name(PK POINT SYSTEM)
                return false;
            }
            else if(Source.PKMode == PKMode.Revenge)
            {
                //TO-DO: implement revenge list, auto add ppl who just killed you.. only hit those ppl
                return false;
            }
            else if(Source.PKMode == PKMode.Team)
            {
                //TO-DO: implrement Team like literally the team of 4 u are in RN
                return false;
            }
            else if(Source.PKMode == PKMode.Guild)
            {
                //TO-DO: everyone EXCEPT, guild, team, guild's allies
                return false;
            }
            /*else if(Source.PKMode == PKMode.Kill)
            {
                return true;
            }*/

            return true; // is potential target
        }

        private void FilterTargets()
        {
            Targets.RemoveWhere(t => !IsPotentialTarget(t));
        }

        private void BroadcastDeath(GameClient target)
        {
            int data0 = MathHelper.BitFold32(Spell != null ? 3 : 1, 0);
            using (var p = GameClient.CreateInteraction(Source.Id, target.Id, 0, target.X, target.Y, InteractAction.Kill, data0, 0, 0, 0))
                target.FieldOfView.Send(p, true);
        }

        private void ProcessTargets()
        {
            if (Spell == null) return;

            // calculate damage done to each target, and send the damage packet to each target
            using (var p = new MagicEffectPacket()
                            .Begin(Source.Id, CastX, CastY, Spell.Type, Spell.Level, 0))
            {
                foreach (var entity in Targets)
                {
                    int damage = 123;
                    bool miss = false;
                    if (!miss)
                    {
                        entity.Health -= damage;
                        if (entity.IsDead)
                        {
                            entity.Health = 0;
                        }
                    }
                    p.Add(entity.Id, damage, miss ? 0 : 1, 0);
                }


                Source.FieldOfView.Send(p.End(), true);
            }

            // sync all entites involved including Source
            foreach (var entity in Targets.Concat(new[] { Source }))
            {
                if (entity.IsDead)
                {
                    // add the death status flag
                    entity.Status += StatusFlag.Death;

                    // issue the death broadcast, and change to ghost
                    Utility.Delay(TimeSpan.FromSeconds(0.15), async () =>
                    {
                        if (!entity.IsDead) return; // revaldiate our assumption
                        BroadcastDeath(entity);

                        await Task.Delay(TimeSpan.FromSeconds(1.5));

                        if (!entity.IsDead) return; // revaldiate our assumption
                        entity.Status += StatusFlag.Ghost;
                        entity.Lookface = entity.Lookface.ToGhost();
                        entity.SendSynchronize(true);
                    });
                }

                entity.SendSynchronize(true);
            }
        }
    }
}
