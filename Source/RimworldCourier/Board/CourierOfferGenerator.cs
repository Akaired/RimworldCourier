using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Builds the jobs shown on a settlement's board for a specific caravan.
    ///
    /// Determinism: every offer slot has its own seed derived from the board seed, so closing and
    /// reopening the board shows the same destinations, goods and reasons. Only quantities and pay
    /// adapt to the caravan looking at it (capacity, speed, transport).
    ///
    /// Feasibility: an offer is only produced if the caravan, as it is now, can carry the cargo
    /// (within the configured share of its free capacity), can reach the destination, and the trip
    /// fits the configured maximum travel time. The deadline always leaves slack on top of the estimate.
    ///
    /// Availability: taken jobs are only replaced on a later visit. If a visit starts with nothing
    /// the caravan can do, one fallback job is offered; once any job is taken during a visit, no
    /// fallback appears until the caravan leaves and comes back.
    /// </summary>
    public static class CourierOfferGenerator
    {
        private const float MinCargoMass = 5f;
        private const int CandidateLimit = 30;
        private const int AttemptsPerSlot = 4;
        private const int FallbackAttempts = 8;
        private const int RelaxedCargoTries = 8;
        private const int HourTicks = 2500;

        /// <summary>Mass of the empty courier crate; the goods' mass is added on top.</summary>
        public static float CrateMass => CourierDefOf.RC_CourierCrate?.BaseMass ?? 0f;

        private class Context
        {
            public Caravan caravan;
            public Settlement origin;
            public Faction issuer;
            public CourierTransportMode mode;
            public float freeMass;
            public List<Settlement> candidates;
            public List<CourierCargoGroupDef> groups;
            public Dictionary<Settlement, int> travelCache = new Dictionary<Settlement, int>();
            public float foodDays;
        }

        public static List<CourierOffer> Generate(Caravan caravan, Settlement origin, WorldObjectComp_CourierBoard board, out string emptyReason)
        {
            List<CourierOffer> offers = new List<CourierOffer>();
            emptyReason = null;
            CourierSettings settings = CourierMod.Settings;
            Context ctx = new Context
            {
                caravan = caravan,
                origin = origin,
                issuer = origin.Faction,
                mode = CourierTransport.For(caravan)
            };

            // The crate itself weighs something too: an accepted job must never overload the caravan.
            ctx.freeMass = (ctx.mode.FreeMass(caravan) - CrateMass) * settings.cargoMassFraction;
            if (ctx.freeMass < MinCargoMass)
            {
                emptyReason = "RC_Board_NoCapacity".Translate();
                return offers;
            }

            ctx.candidates = DestinationCandidates(ctx);
            if (ctx.candidates.Count == 0)
            {
                emptyReason = "RC_Board_NoDestinations".Translate();
                return offers;
            }

            ctx.groups = CourierCargo.GroupsFor(ctx.issuer);
            if (ctx.groups.Count == 0)
            {
                emptyReason = "RC_Board_NoCargo".Translate();
                return offers;
            }

            ctx.foodDays = ctx.mode.FoodMatters(caravan) ? DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood(caravan) : float.MaxValue;

            int slots;
            Rand.PushState(board.BoardSeed);
            try
            {
                slots = Rand.RangeInclusive(settings.minJobs, Mathf.Max(settings.minJobs, settings.maxJobs));
            }
            finally
            {
                Rand.PopState();
            }

            foreach (int index in board.VisibleSlots(slots))
            {
                CourierOffer offer = MakeSlotOffer(index, relaxed: false, board, ctx);
                if (offer != null)
                {
                    offers.Add(offer);
                }
            }

            // Arriving to an empty board: offer one job the caravan can actually do.
            // Not right after taking jobs, though: stepping off the tile and back must not print jobs.
            if (offers.Count == 0 && !board.TakenThisVisit && !board.TakenWithinRestockTime)
            {
                for (int n = 0; n < FallbackAttempts && offers.Count == 0; n++)
                {
                    int index = board.FallbackSlot(n);
                    if (board.IsTaken(index))
                    {
                        continue;
                    }
                    CourierOffer offer = MakeSlotOffer(index, relaxed: n >= FallbackAttempts / 2, board, ctx);
                    if (offer != null)
                    {
                        offers.Add(offer);
                    }
                }
            }

            if (offers.Count == 0)
            {
                emptyReason = board.TakenThisVisit || board.TakenWithinRestockTime ? "RC_Board_ComeBackLater".Translate() : "RC_Board_NoFeasible".Translate();
            }
            return offers;
        }

        private static CourierOffer MakeSlotOffer(int index, bool relaxed, WorldObjectComp_CourierBoard board, Context ctx)
        {
            CourierOffer offer = null;
            for (int attempt = 0; attempt < AttemptsPerSlot && offer == null; attempt++)
            {
                Rand.PushState(Gen.HashCombineInt(board.BoardSeed, index * 97 + attempt));
                try
                {
                    offer = TryMakeOffer(index, relaxed, ctx);
                }
                finally
                {
                    Rand.PopState();
                }
            }
            return offer;
        }

        private static List<Settlement> DestinationCandidates(Context ctx)
        {
            float maxDistance = ctx.mode.MaxCandidateDistance(ctx.caravan);
            return Find.WorldObjects.Settlements
                .Where(s => IsValidDestination(s, ctx.origin, ctx.issuer))
                .Select(s => new { settlement = s, distance = Find.WorldGrid.ApproxDistanceInTiles(ctx.origin.Tile, s.Tile) })
                .Where(x => x.distance <= maxDistance)
                .OrderBy(x => x.distance)
                .ThenBy(x => x.settlement.ID)
                .Take(CandidateLimit)
                .Select(x => x.settlement)
                .ToList();
        }

        public static bool IsValidDestination(Settlement s, Settlement origin, Faction issuer)
        {
            if (s == origin || !s.Spawned || s.Faction == null || s.Faction.IsPlayer || s.Faction.defeated || s.Faction.Hidden)
            {
                return false;
            }
            if (s.Tile.Layer != origin.Tile.Layer || !s.Visitable)
            {
                return false;
            }
            if (s.Faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }
            if (s.Faction != issuer && s.Faction.HostileTo(issuer))
            {
                return false;
            }
            return true;
        }

        private static int TravelTicks(Settlement destination, Context ctx)
        {
            if (!ctx.travelCache.TryGetValue(destination, out int ticks))
            {
                ticks = ctx.mode.TryEstimateTravel(ctx.caravan, ctx.origin.Tile, destination.Tile, out int estimate) ? estimate : -1;
                ctx.travelCache[destination] = ticks;
            }
            return ticks;
        }

        private static CourierOffer TryMakeOffer(int index, bool relaxed, Context ctx)
        {
            CourierSettings settings = CourierMod.Settings;

            // Destination: nearer settlements are more likely. Relaxed search: the nearest one the
            // caravan can actually reach in time (candidates are sorted by distance).
            Settlement destination = null;
            int ticks = -1;
            if (relaxed)
            {
                foreach (Settlement candidate in ctx.candidates)
                {
                    int t = TravelTicks(candidate, ctx);
                    if (t > 0 && t / (float)GenDate.TicksPerDay <= settings.maxTravelDays)
                    {
                        destination = candidate;
                        ticks = t;
                        break;
                    }
                }
                if (destination == null)
                {
                    return null;
                }
            }
            else
            {
                destination = ctx.candidates.RandomElementByWeight(s => 1f / (1f + Find.WorldGrid.ApproxDistanceInTiles(ctx.origin.Tile, s.Tile) / 15f));
                ticks = TravelTicks(destination, ctx);
            }
            if (ticks <= 0)
            {
                return null;
            }
            float days = ticks / (float)GenDate.TicksPerDay;
            if (days > settings.maxTravelDays)
            {
                return null;
            }

            // Cargo: group, thing, stuff, and a share of the free capacity. The recipient must
            // accept the kind of goods too (e.g. no organs for a faction that abhors organ use).
            List<CourierCargoGroupDef> groups = ctx.groups.Where(g => g.AllowedBy(destination.Faction, asSender: false)).ToList();
            if (groups.Count == 0)
            {
                return null;
            }
            CourierCargoGroupDef group = null;
            ThingDef cargoDef = null;
            ThingDef stuff = null;
            float unitMass = 0f;
            float unitValue = 0f;
            int count = 0;
            int tries = relaxed ? RelaxedCargoTries : 1;
            for (int t = 0; t < tries && count < 1; t++)
            {
                if (!CourierCargo.TryPick(groups, ctx.issuer, out group, out cargoDef, out stuff))
                {
                    continue;
                }
                unitMass = CourierCargo.UnitMass(cargoDef, stuff);
                unitValue = Mathf.Max(0.01f, CourierCargo.UnitValue(cargoDef, stuff));
                float massShare = relaxed ? Rand.Range(0.2f, 0.6f) : Rand.Range(0.35f, 1f);
                count = Mathf.FloorToInt(ctx.freeMass * massShare / unitMass);
                count = Mathf.Min(count, Mathf.FloorToInt(settings.maxCargoValue / unitValue));
                if (group.maxCount > 0)
                {
                    count = Mathf.Min(count, Rand.RangeInclusive(Mathf.Max(1, group.maxCount / 3), group.maxCount));
                }
                if (count >= 20)
                {
                    count = count / 5 * 5;
                }
            }
            if (count < 1 || cargoDef == null)
            {
                return null;
            }
            float cargoMass = unitMass * count;
            float cargoValue = unitValue * count;
            float tiles = Find.WorldGrid.ApproxDistanceInTiles(ctx.origin.Tile, destination.Tile);
            bool sameFaction = destination.Faction == ctx.issuer;
            CourierReasonDef reason = CourierReasonDef.Pick(group, sameFaction, ctx.issuer);
            bool imperial = CourierRoyalty.IsEmpire(ctx.issuer);

            // Pay: time on the road, distance, bulk and a small share of the goods' value, plus
            // running costs such as fuel.
            float massFactor = Mathf.Clamp(Mathf.Pow(cargoMass / 50f, 0.6f), 0.4f, 4f);
            float pay = (60f + 54f * days + 5f * tiles) * (0.5f + 0.5f * massFactor) + 0.05f * cargoValue;
            if (imperial)
            {
                pay *= CourierRoyalty.EmpirePayMultiplier;
            }
            pay = pay * settings.payMultiplier + ctx.mode.RunningCostSilver(ctx.caravan, ctx.origin.Tile, destination.Tile, ticks);
            int total = Mathf.Max(25, Mathf.RoundToInt(pay));

            // The Empire sometimes rewards its couriers with honor, on top of the pay.
            int honor = 0;
            if (imperial && Rand.Chance(CourierRoyalty.HonorChance))
            {
                honor = total > 700 ? Rand.RangeInclusive(2, 3) : Rand.RangeInclusive(1, 2);
            }

            int goodwill = 0;
            int chemfuel = 0;
            int silver = total;
            if (Rand.Chance(0.35f))
            {
                goodwill = Mathf.Clamp(Mathf.RoundToInt(total / 90f), 2, 12);
                silver -= goodwill * 20;
            }
            if (ctx.mode.OffersFuelRewards(ctx.caravan) && Rand.Chance(0.5f))
            {
                float fuelUnitValue = ThingDefOf.Chemfuel.BaseMarketValue;
                chemfuel = Mathf.RoundToInt(silver * Rand.Range(0.3f, 0.6f) / fuelUnitValue);
                silver -= Mathf.RoundToInt(chemfuel * fuelUnitValue);
            }
            silver = Mathf.Max(silver, Mathf.RoundToInt(total * 0.2f));
            silver = Mathf.Max(5, silver / 5 * 5);

            int deadline = Mathf.CeilToInt(ticks * settings.deadlineSlack) + GenDate.TicksPerDay / 2;
            deadline = Mathf.Max(deadline, GenDate.TicksPerDay);
            deadline = Mathf.CeilToInt(deadline / (float)HourTicks) * HourTicks;

            return new CourierOffer
            {
                index = index,
                destination = destination,
                cargoGroup = group,
                cargoDef = cargoDef,
                cargoStuff = stuff,
                cargoCount = count,
                cargoMass = cargoMass,
                cargoValue = cargoValue,
                reason = reason,
                distanceTiles = tiles,
                travelTicks = ticks,
                deadlineTicks = deadline,
                rewardSilver = silver,
                rewardGoodwill = goodwill,
                rewardChemfuel = chemfuel,
                rewardHonor = honor,
                transportKind = ctx.mode.Kind(ctx.caravan),
                foodWarning = ctx.foodDays < days * 1.1f
            };
        }

        /// <summary>Re-checks an offer against the caravan right before accepting it.</summary>
        public static AcceptanceReport CanAccept(CourierOffer offer, Caravan caravan, Settlement origin, WorldObjectComp_CourierBoard board)
        {
            if (CaravanVisitUtility.SettlementVisitedNow(caravan) != origin)
            {
                return "RC_Accept_NotHere".Translate();
            }
            // Another board window (e.g. of a second caravan on the same tile) may have taken it.
            if (board != null && board.IsTaken(offer.index))
            {
                return "RC_Accept_Taken".Translate();
            }
            AcceptanceReport title = CourierRoyalty.CanWorkFor(caravan, origin.Faction);
            if (!title.Accepted)
            {
                return title;
            }
            if (!IsValidDestination(offer.destination, origin, origin.Faction))
            {
                return "RC_Accept_DestinationInvalid".Translate();
            }
            CourierTransportMode mode = CourierTransport.For(caravan);
            float needed = offer.cargoMass + CrateMass;
            if (mode.FreeMass(caravan) < needed)
            {
                return "RC_Accept_TooHeavy".Translate(needed.ToStringMass(), mode.FreeMass(caravan).ToStringMass());
            }
            return true;
        }

        /// <summary>Rewards as separate lines: "320 silver, 45 chemfuel", "+5 goodwill with X", "2 honor for Y".</summary>
        public static List<string> RewardLines(int silver, int goodwill, int chemfuel, Faction issuer, int honor = 0, Pawn honorPawn = null)
        {
            List<string> lines = new List<string>();
            List<string> goods = new List<string>();
            if (silver > 0)
            {
                goods.Add("RC_RewardSilver".Translate(silver));
            }
            if (chemfuel > 0)
            {
                goods.Add("RC_RewardChemfuel".Translate(chemfuel));
            }
            if (goods.Count > 0)
            {
                lines.Add(goods.ToCommaList());
            }
            if (goodwill > 0)
            {
                lines.Add("RC_RewardGoodwill".Translate(goodwill.ToStringWithSign(), issuer?.Name ?? "?"));
            }
            if (honor > 0)
            {
                lines.Add(honorPawn != null
                    ? "RC_RewardHonorFor".Translate(honor, honorPawn.LabelShort).ToString()
                    : "RC_RewardHonor".Translate(honor).ToString());
            }
            return lines;
        }

        public static string RewardLabel(int silver, int goodwill, int chemfuel, Faction issuer, int honor = 0, Pawn honorPawn = null)
        {
            return RewardLines(silver, goodwill, chemfuel, issuer, honor, honorPawn).ToCommaList();
        }

        /// <summary>
        /// "The Kingdom of X is sending 45 packaged survival meals to Oakhaven, a settlement of Y,
        /// to help them through a poor harvest."
        /// </summary>
        public static string Story(CourierReasonDef reason, Faction issuer, string cargoLabel, string destinationLabel, Faction destinationFaction)
        {
            string clause = reason?.clause ?? "RC_Story_DefaultClause".Translate().ToString();
            bool sameFaction = destinationFaction != null && destinationFaction == issuer;
            string key = sameFaction ? "RC_Story_SameFaction" : "RC_Story_OtherFaction";
            return key.Translate(issuer?.Name ?? "?", cargoLabel, destinationLabel, destinationFaction?.Name ?? "?", clause);
        }
    }
}
