﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MSLHM
{
    // Token: 0x02000003 RID: 3
    public class HediffComp_HealPermanentWounds : HediffComp
    {
        // Token: 0x04000002 RID: 2
        private readonly HashSet<string> chronicConditions = new HashSet<string>
        {
            "Blindness",
            "TraumaSavant",
            "Cirrhosis",
            "ChemicalDamageSevere",
            "ChemicalDamageModerate",
            "HepatitisK"
        };

        // Token: 0x04000001 RID: 1
        private int ticksToHeal;

        // Token: 0x06000003 RID: 3 RVA: 0x00002078 File Offset: 0x00000278
        public HediffComp_HealPermanentWounds()
        {
            foreach (var hediffGiverSetDef in DefDatabase<HediffGiverSetDef>.AllDefsListForReading)
            {
                hediffGiverSetDef.hediffGivers.FindAll(hg => hg.GetType() == typeof(HediffGiver_Birthday))
                    .ForEach(delegate(HediffGiver hg) { chronicConditions.Add(hg.hediff.defName); });
            }

            Log.Message(string.Join(", ", chronicConditions.ToArray()));
        }

        // Token: 0x17000001 RID: 1
        // (get) Token: 0x06000002 RID: 2 RVA: 0x00002068 File Offset: 0x00000268
        public HediffCompProperties_HealPermanentWounds Props => (HediffCompProperties_HealPermanentWounds) props;

        // Token: 0x06000004 RID: 4 RVA: 0x00002170 File Offset: 0x00000370
        public override void CompPostMake()
        {
            base.CompPostMake();
            ResetTicksToHeal();
        }

        // Token: 0x06000005 RID: 5 RVA: 0x0000217E File Offset: 0x0000037E
        public void ResetTicksToHeal()
        {
            if (Settings.Get().debugHealingSpeed)
            {
                ticksToHeal = 3000;
                return;
            }

            ticksToHeal = Rand.Range(240000, 360000);
        }

        // Token: 0x06000006 RID: 6 RVA: 0x000021AD File Offset: 0x000003AD
        public override void CompPostTick(ref float severityAdjustment)
        {
            ticksToHeal--;
            if (ticksToHeal >= 240000)
            {
                ResetTicksToHeal();
                return;
            }

            if (ticksToHeal <= 0)
            {
                TryHealRandomPermanentWound();
                AffectPawnsAge();
                ResetTicksToHeal();
            }
        }

        // Token: 0x06000007 RID: 7 RVA: 0x000021EC File Offset: 0x000003EC
        public void TryHealRandomPermanentWound()
        {
            var selectHediffsQuery = from hd in Pawn.health.hediffSet.hediffs
                where hd.IsPermanent() || chronicConditions.Contains(hd.def.defName)
                select hd;
            if (selectHediffsQuery.Any())
            {
                selectHediffsQuery.TryRandomElement(out var hediff);
                var Hlabel = "condition";
                if (hediff != null)
                {
                    Hlabel = hediff.Label;
                    var meanHeal = 0.2f;
                    var rndHealPercent = meanHeal + (Rand.Gaussian() * meanHeal / 2f);
                    var bodyPartMaxHP = 1f;
                    if (hediff?.Part != null)
                    {
                        bodyPartMaxHP = hediff.Part.def.GetMaxHealth(hediff.pawn);
                    }

                    var healAmount = bodyPartMaxHP * rndHealPercent;
                    if (healAmount < 0.1f)
                    {
                        healAmount = 0.1f;
                    }

                    if (hediff.Severity - healAmount < 0.1f)
                    {
                        Pawn.health.hediffSet.hediffs.Remove(hediff);
                    }
                    else
                    {
                        hediff.Severity -= healAmount;
                    }
                }

                if (PawnUtility.ShouldSendNotificationAbout(Pawn))
                {
                    Messages.Message(Pawn.Label + "'s " + Hlabel + " was healed by Metasis.", Pawn,
                        MessageTypeDefOf.PositiveEvent);
                }
            }
        }

        // Token: 0x06000008 RID: 8 RVA: 0x00002328 File Offset: 0x00000528
        public void AffectPawnsAge()
        {
            if (Pawn.RaceProps.Humanlike)
            {
                if (Pawn.ageTracker.AgeBiologicalYears > 25)
                {
                    Pawn.ageTracker.AgeBiologicalTicks.TicksToPeriod(out var biologicalYears,
                        out var biologicalQuadrums, out var biologicalDays, out _);
                    var ageBefore = "AgeBiological".Translate(new object[]
                    {
                        biologicalYears,
                        biologicalQuadrums,
                        biologicalDays
                    });
                    var diffFromOptimalAge = Pawn.ageTracker.AgeBiologicalTicks - 90000000L;
                    Pawn.ageTracker.AgeBiologicalTicks -= (long) (diffFromOptimalAge * 0.05f);
                    Pawn.ageTracker.AgeBiologicalTicks.TicksToPeriod(out biologicalYears, out biologicalQuadrums,
                        out biologicalDays, out _);
                    var ageAfter = "AgeBiological".Translate(new object[]
                    {
                        biologicalYears,
                        biologicalQuadrums,
                        biologicalDays
                    });
                    if (Pawn.IsColonist && Settings.Get().showAgingMessages)
                    {
                        Messages.Message("MessageAgeReduced".Translate(new object[]
                        {
                            Pawn.Label,
                            ageBefore,
                            ageAfter
                        }), MessageTypeDefOf.PositiveEvent);
                        Messages.Message(
                            "MessageAgeReduced".Translate(parent.LabelCap, Pawn.Label, ageBefore, ageAfter), Pawn,
                            MessageTypeDefOf.PositiveEvent);
                    }
                }
                else if (Pawn.ageTracker.AgeBiologicalYears < 25)
                {
                    Pawn.ageTracker.AgeBiologicalTicks += 900000L;
                }
            }
            else
            {
                var curLifeStageIndex = Pawn.ageTracker.CurLifeStageIndex;
                var startOfThirdStage = (long) (Pawn.RaceProps.lifeStageAges[2].minAge * 60f * 60000f);
                var diffFromOptimalAge2 = Pawn.ageTracker.AgeBiologicalTicks - startOfThirdStage;
                if (curLifeStageIndex >= 3)
                {
                    Pawn.ageTracker.AgeBiologicalTicks -= (long) (diffFromOptimalAge2 * 0.05f);
                    return;
                }

                Pawn.ageTracker.AgeBiologicalTicks += 300000L;
            }
        }

        // Token: 0x06000009 RID: 9 RVA: 0x0000259B File Offset: 0x0000079B
        public override void CompExposeData()
        {
            Scribe_Values.Look(ref ticksToHeal, "ticksToHeal");
        }

        // Token: 0x0600000A RID: 10 RVA: 0x000025AF File Offset: 0x000007AF
        public override string CompDebugString()
        {
            return "ticksToHeal: " + ticksToHeal;
        }
    }
}