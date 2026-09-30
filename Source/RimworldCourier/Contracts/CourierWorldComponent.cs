using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimworldCourier
{
    /// <summary>
    /// Owns every courier contract: creation on acceptance, delivery, returns, failures and the
    /// periodic sanity checks (deadlines, lost crates, destinations that disappeared).
    /// Every contract has a vanilla quest (Quests tab) and every state change sends a letter
    /// linked to that quest.
    /// </summary>
    public class CourierWorldComponent : WorldComponent
    {
        private const int CheckIntervalTicks = 250;

        private List<CourierContract> contracts = new List<CourierContract>();
        private int nextContractId = 1;

        /// <summary>Settlements whose tile currently has at least one player caravan.</summary>
        private List<int> presentSettlementIds = new List<int>();
        private readonly HashSet<int> tmpPresent = new HashSet<int>();
        private readonly List<Settlement> tmpArrived = new List<Settlement>();

        public CourierWorldComponent(World world) : base(world)
        {
        }

        public static CourierWorldComponent Instance => Find.World?.GetComponent<CourierWorldComponent>();

        public IEnumerable<CourierContract> OpenContracts => contracts.Where(c => c.IsOpen);

        public CourierContract GetContract(int id)
        {
            if (id < 0)
            {
                return null;
            }
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].id == id)
                {
                    return contracts[i];
                }
            }
            return null;
        }

        /// <summary>Open contracts whose crate is inside this caravan and can be handed over at <paramref name="settlement"/>.</summary>
        public List<CourierContract> HandOverableAt(Caravan caravan, Settlement settlement)
        {
            List<CourierContract> result = new List<CourierContract>();
            if (settlement == null)
            {
                return result;
            }
            List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
            for (int i = 0; i < contracts.Count; i++)
            {
                CourierContract c = contracts[i];
                if (c.IsOpen && c.TicksLeft > 0 && c.crate != null && !c.crate.Destroyed && c.CanHandOverAt(settlement) && items.Contains(c.crate))
                {
                    result.Add(c);
                }
            }
            return result;
        }

        public List<CourierCrate> CratesIn(Caravan caravan)
        {
            List<CourierCrate> result = new List<CourierCrate>();
            List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is CourierCrate crate)
                {
                    result.Add(crate);
                }
            }
            return result;
        }

        /// <summary>Closest settlements of a faction to wherever the crate is (for "return the cargo" guidance).</summary>
        public static IEnumerable<Settlement> NearestSettlementsOf(Faction faction, Thing crate, int count)
        {
            if (faction == null)
            {
                return Enumerable.Empty<Settlement>();
            }
            PlanetTile from = crate != null && !crate.Destroyed ? (CourierUtility.CaravanOf(crate)?.Tile ?? crate.Tile) : PlanetTile.Invalid;
            IEnumerable<Settlement> settlements = Find.WorldObjects.Settlements.Where(s => s.Faction == faction && s.Spawned && s.Visitable);
            if (from.Valid)
            {
                settlements = settlements.Where(s => s.Tile.Layer == from.Layer)
                    .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(from, s.Tile));
            }
            return settlements.Take(count);
        }

        // ------------------------------------------------------------------ visits

        /// <summary>
        /// Detects new visits: a settlement whose tile gets a player caravan after having none.
        /// Presence is by tile, not by "stopped at the settlement", so pretending to leave (start
        /// moving, then stop on the same tile) or splitting the caravan does not count as a new visit.
        /// </summary>
        public void UpdateVisits()
        {
            tmpPresent.Clear();
            tmpArrived.Clear();
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (!caravan.IsPlayerControlled || !caravan.Spawned)
                {
                    continue;
                }
                Settlement settlement = Find.WorldObjects.SettlementAt(caravan.Tile);
                if (settlement == null || !tmpPresent.Add(settlement.ID))
                {
                    continue;
                }
                if (!presentSettlementIds.Contains(settlement.ID))
                {
                    tmpArrived.Add(settlement);
                }
            }
            presentSettlementIds.Clear();
            presentSettlementIds.AddRange(tmpPresent);
            for (int i = 0; i < tmpArrived.Count; i++)
            {
                tmpArrived[i].GetComponent<WorldObjectComp_CourierBoard>()?.Notify_VisitStarted();
            }
        }

        // ------------------------------------------------------------------ acceptance

        public CourierContract Accept(CourierOffer offer, Caravan caravan, Settlement origin)
        {
            CourierContract contract = new CourierContract
            {
                id = nextContractId++,
                issuer = origin.Faction,
                origin = origin,
                originLabel = origin.LabelCap,
                destination = offer.destination,
                destinationLabel = offer.destination.LabelCap,
                destinationFaction = offer.destination.Faction,
                cargoGroup = offer.cargoGroup,
                reason = offer.reason,
                cargoDef = offer.cargoDef,
                cargoStuff = offer.cargoStuff,
                cargoCount = offer.cargoCount,
                cargoLabel = GenLabel.ThingLabel(offer.cargoDef, offer.cargoStuff, offer.cargoCount).CapitalizeFirst(),
                cargoMass = offer.cargoMass,
                cargoValue = offer.cargoValue,
                rewardSilver = offer.rewardSilver,
                rewardGoodwill = offer.rewardGoodwill,
                rewardChemfuel = offer.rewardChemfuel,
                rewardHonor = offer.rewardHonor,
                honorPawn = offer.rewardHonor > 0 ? CourierRoyalty.BestTitledPawn(caravan, origin.Faction) : null,
                estimatedTravelTicks = offer.travelTicks,
                acceptedTick = Find.TickManager.TicksGame,
                deadlineTick = Find.TickManager.TicksGame + offer.deadlineTicks,
                state = CourierContractState.Active
            };

            CourierCrate crate = (CourierCrate)ThingMaker.MakeThing(CourierDefOf.RC_CourierCrate);
            crate.contractId = contract.id;
            foreach (Thing cargo in CourierCargo.MakeCargo(offer.cargoDef, offer.cargoStuff, offer.cargoCount))
            {
                crate.AddContents(cargo);
            }
            contract.crate = crate;
            contracts.Add(contract);

            CaravanInventoryUtility.GiveThing(caravan, crate);
            CreateQuest(contract);

            Find.LetterStack.ReceiveLetter(
                "RC_LetterAcceptedLabel".Translate(),
                "RC_LetterAcceptedText".Translate(
                    caravan.LabelCap,
                    contract.Story,
                    contract.CargoLabel,
                    contract.cargoMass.ToStringMass(),
                    contract.DestinationLabel,
                    contract.destinationFaction?.Name ?? "?",
                    contract.TicksLeft.ToStringTicksToPeriod(),
                    contract.estimatedTravelTicks.ToStringTicksToPeriod(),
                    contract.RewardLabel),
                LetterDefOf.NeutralEvent,
                new LookTargets(offer.destination),
                contract.issuer,
                contract.quest);
            return contract;
        }

        // ------------------------------------------------------------------ quests

        private static void CreateQuest(CourierContract contract)
        {
            Quest quest = Quest.MakeRaw();
            quest.root = CourierDefOf.RC_CourierDelivery;
            quest.name = "RC_QuestName".Translate(contract.Title, contract.DestinationLabel);
            quest.challengeRating = ChallengeRating(contract);
            quest.description = QuestDescription(contract);

            QuestPart_CourierDelivery part = new QuestPart_CourierDelivery
            {
                contractId = contract.id,
                inSignalEnable = quest.InitiateSignal
            };
            quest.AddPart(part);

            Find.QuestManager.Add(quest);
            quest.Accept(null);
            contract.quest = quest;
        }

        private static int ChallengeRating(CourierContract contract)
        {
            float days = contract.estimatedTravelTicks / (float)GenDate.TicksPerDay;
            if (days <= 3f)
            {
                return 1;
            }
            return days <= 7f ? 2 : 3;
        }

        private static TaggedString QuestDescription(CourierContract contract)
        {
            TaggedString text = "RC_QuestDescription".Translate(
                contract.issuer?.Name ?? "?",
                contract.Story,
                contract.CargoLabel,
                contract.cargoMass.ToStringMass(),
                contract.cargoValue.ToStringMoney(),
                contract.DestinationLabel,
                contract.destinationFaction?.Name ?? "?",
                contract.RewardLabel,
                FailurePenalty(contract, CourierFailReason.SealBroken));
            if (contract.state == CourierContractState.Returning)
            {
                text += "\n\n" + "RC_QuestDescriptionReturning".Translate(contract.issuer?.Name ?? "?");
            }
            return text;
        }

        private static void EndQuest(CourierContract contract, QuestEndOutcome outcome)
        {
            Quest quest = contract.quest;
            if (quest != null && !quest.Historical)
            {
                // Our own letters replace the generic "quest completed/failed" one.
                quest.End(outcome, sendLetter: false);
            }
        }

        // ------------------------------------------------------------------ hand over

        public void HandOver(CourierContract contract, Caravan caravan)
        {
            if (!contract.IsOpen || contract.crate == null)
            {
                return;
            }
            // The periodic check runs every few seconds: don't accept a hand-over past the deadline in between.
            if (contract.TicksLeft <= 0)
            {
                Fail(contract, CourierFailReason.Expired);
                return;
            }
            bool delivering = contract.state == CourierContractState.Active;

            contract.crate.holdingOwner?.Remove(contract.crate);
            contract.crate.DestroyResolved();
            contract.crate = null;

            if (delivering)
            {
                contract.state = CourierContractState.Completed;
                GiveStack(caravan, ThingDefOf.Silver, contract.rewardSilver);
                GiveStack(caravan, ThingDefOf.Chemfuel, contract.rewardChemfuel);
                if (contract.rewardGoodwill != 0 && contract.issuer != null && !contract.issuer.defeated)
                {
                    Faction.OfPlayer.TryAffectGoodwillWith(contract.issuer, contract.rewardGoodwill, canSendMessage: false,
                        canSendHostilityLetter: true, CourierDefOf.RC_CourierDeliveryCompleted, caravan);
                }
                // No honor from an Empire that is at war with you, even if the delivery still counts.
                if (contract.rewardHonor > 0 && contract.issuer != null && !contract.issuer.HostileTo(Faction.OfPlayer))
                {
                    Pawn honored = CourierRoyalty.GiveHonor(contract.issuer, contract.rewardHonor, contract.honorPawn, caravan);
                    contract.honorPawn = honored;
                }
                SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
                Find.LetterStack.ReceiveLetter(
                    "RC_LetterCompletedLabel".Translate(),
                    "RC_LetterCompletedText".Translate(caravan.LabelCap, contract.CargoLabel, contract.DestinationLabel, contract.RewardLabel),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(caravan),
                    contract.issuer,
                    contract.quest);
                EndQuest(contract, QuestEndOutcome.Success);
            }
            else
            {
                contract.state = CourierContractState.Returned;
                Find.LetterStack.ReceiveLetter(
                    "RC_LetterReturnedLabel".Translate(),
                    "RC_LetterReturnedText".Translate(contract.CargoLabel, contract.issuer?.Name ?? "?"),
                    LetterDefOf.NeutralEvent,
                    new LookTargets(caravan),
                    contract.issuer,
                    contract.quest);
                EndQuest(contract, QuestEndOutcome.Unknown);
            }
            ClearReferences(contract);
        }

        private static void GiveStack(Caravan caravan, ThingDef def, int count)
        {
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = Mathf.Min(count, def.stackLimit);
                count -= thing.stackCount;
                CaravanInventoryUtility.GiveThing(caravan, thing);
            }
        }

        // ------------------------------------------------------------------ crate notifications

        public void Notify_SealBroken(CourierCrate crate)
        {
            CourierContract contract = GetContract(crate.contractId);
            if (contract != null && contract.IsOpen)
            {
                Fail(contract, CourierFailReason.SealBroken);
            }
        }

        public void Notify_CrateLost(CourierCrate crate)
        {
            CourierContract contract = GetContract(crate.contractId);
            if (contract != null && contract.IsOpen)
            {
                Fail(contract, CourierFailReason.CargoLost);
            }
        }

        // ------------------------------------------------------------------ failure / return / void

        /// <summary>
        /// Goodwill penalty grows with the value of the cargo, so keeping valuable goods is never
        /// worth it. Losing cargo you did not profit from costs half of the value part.
        /// </summary>
        public static int FailurePenalty(CourierContract contract, CourierFailReason reason)
        {
            CourierSettings s = CourierMod.Settings;
            float valuePart = contract.cargoValue / Mathf.Max(1f, s.failureSilverPerGoodwill);
            if (reason == CourierFailReason.CargoLost)
            {
                valuePart *= 0.5f;
            }
            return Mathf.Clamp(Mathf.RoundToInt(s.failureBaseGoodwill + valuePart), 1, 100);
        }

        private void Fail(CourierContract contract, CourierFailReason reason)
        {
            bool wasReturning = contract.state == CourierContractState.Returning;
            contract.state = CourierContractState.Failed;
            int penalty = FailurePenalty(contract, reason);

            HistoryEventDef historyEvent = reason == CourierFailReason.SealBroken
                ? CourierDefOf.RC_CourierCargoStolen
                : CourierDefOf.RC_CourierContractFailed;

            if (contract.issuer != null && !contract.issuer.defeated && !contract.issuer.IsPlayer)
            {
                Faction.OfPlayer.TryAffectGoodwillWith(contract.issuer, -penalty, canSendMessage: false,
                    canSendHostilityLetter: true, historyEvent);
            }

            string reasonText = ("RC_FailReason_" + reason).Translate();
            if (wasReturning && reason == CourierFailReason.Expired)
            {
                reasonText = "RC_FailReason_NotReturned".Translate();
            }
            // A lost crate is being destroyed right now: don't point the letter at it.
            LookTargets look = reason != CourierFailReason.CargoLost
                ? CourierUtility.LookTargetFor(contract.crate)
                : LookTargets.Invalid;
            Find.LetterStack.ReceiveLetter(
                "RC_LetterFailedLabel".Translate(),
                "RC_LetterFailedText".Translate(contract.CargoLabel, contract.issuer?.Name ?? "?", reasonText, penalty),
                LetterDefOf.NegativeEvent,
                look,
                contract.issuer,
                contract.quest);
            EndQuest(contract, QuestEndOutcome.Fail);
            ClearReferences(contract);
        }

        private void StartReturn(CourierContract contract, string why)
        {
            contract.state = CourierContractState.Returning;
            int grace = Mathf.RoundToInt(CourierMod.Settings.returnGraceDays * GenDate.TicksPerDay);
            contract.deadlineTick = Mathf.Max(contract.deadlineTick, Find.TickManager.TicksGame + grace);
            contract.destination = null;
            if (contract.quest != null)
            {
                contract.quest.description = QuestDescription(contract);
            }
            // Returning to a hostile sender is only possible once relations improve: say so.
            if (contract.issuer != null && contract.issuer.HostileTo(Faction.OfPlayer))
            {
                contract.issuerHostileNotified = true;
                Find.LetterStack.ReceiveLetter(
                    "RC_LetterReturnLabel".Translate(),
                    "RC_LetterReturnHostileText".Translate(contract.CargoLabel, contract.destinationLabel, why, contract.issuer.Name,
                        contract.TicksLeft.ToStringTicksToPeriod(), FailurePenalty(contract, CourierFailReason.SealBroken)),
                    LetterDefOf.NegativeEvent,
                    CourierUtility.LookTargetFor(contract.crate),
                    contract.issuer,
                    contract.quest);
                return;
            }
            List<GlobalTargetInfo> targets = NearestSettlementsOf(contract.issuer, contract.crate, 3)
                .Select(s => (GlobalTargetInfo)s)
                .ToList();
            Find.LetterStack.ReceiveLetter(
                "RC_LetterReturnLabel".Translate(),
                "RC_LetterReturnText".Translate(contract.CargoLabel, contract.destinationLabel, why, contract.issuer?.Name ?? "?", contract.TicksLeft.ToStringTicksToPeriod()),
                LetterDefOf.NeutralEvent,
                targets.Count > 0 ? new LookTargets(targets) : CourierUtility.LookTargetFor(contract.crate),
                contract.issuer,
                contract.quest);
        }

        private void Void(CourierContract contract)
        {
            contract.state = CourierContractState.Voided;
            Find.LetterStack.ReceiveLetter(
                "RC_LetterVoidedLabel".Translate(),
                "RC_LetterVoidedText".Translate(contract.CargoLabel, contract.issuer?.Name ?? "?"),
                LetterDefOf.NeutralEvent,
                CourierUtility.LookTargetFor(contract.crate),
                contract.issuer,
                contract.quest);
            EndQuest(contract, QuestEndOutcome.Unknown);
            ClearReferences(contract);
        }

        private void SendIssuerHostileLetter(CourierContract contract)
        {
            string key;
            if (contract.state == CourierContractState.Returning)
            {
                key = "RC_LetterClientHostileReturn";
            }
            else if (contract.destinationFaction == contract.issuer)
            {
                key = "RC_LetterClientHostileWait";
            }
            else
            {
                key = "RC_LetterClientHostileDeliver";
            }
            Find.LetterStack.ReceiveLetter(
                "RC_LetterClientHostileLabel".Translate(),
                key.Translate(
                    contract.issuer?.Name ?? "?",
                    contract.CargoLabel,
                    contract.DestinationLabel,
                    contract.destinationFaction?.Name ?? "?",
                    FailurePenalty(contract, CourierFailReason.SealBroken),
                    contract.TicksLeft.ToStringTicksToPeriod()),
                LetterDefOf.NegativeEvent,
                CourierUtility.LookTargetFor(contract.crate),
                contract.issuer,
                contract.quest);
        }

        /// <summary>Drop references to world objects/things so finished contracts never hold dead references.</summary>
        private static void ClearReferences(CourierContract contract)
        {
            if (contract.crate != null && !contract.crate.Destroyed)
            {
                // The crate stays in the world (e.g. after a failure) but no longer belongs to a live contract.
                contract.crate.contractId = -1;
            }
            contract.crate = null;
            contract.origin = null;
            contract.destination = null;
            contract.honorPawn = null;
        }

        // ------------------------------------------------------------------ ticking

        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckIntervalTicks != 0)
            {
                return;
            }
            UpdateVisits();
            CheckContracts();
        }

        private void CheckContracts()
        {
            for (int i = 0; i < contracts.Count; i++)
            {
                CourierContract c = contracts[i];
                if (!c.IsOpen)
                {
                    continue;
                }

                // The crate can disappear without Destroy() being called (e.g. discarded with a
                // world pawn or an abandoned map).
                if (c.crate == null || c.crate.Destroyed)
                {
                    Fail(c, CourierFailReason.CargoLost);
                    continue;
                }

                // Contracts accepted with v0.1 had no quest.
                if (c.quest == null)
                {
                    CreateQuest(c);
                }

                // Don't keep references to world objects that no longer exist (avoids load warnings).
                if (c.origin != null && c.origin.Destroyed)
                {
                    c.origin = null;
                }

                // Nobody left to deliver for: the sender's faction no longer exists.
                if (c.issuer == null || c.issuer.defeated)
                {
                    Void(c);
                    continue;
                }

                if (c.state == CourierContractState.Active)
                {
                    string why = DestinationProblem(c);
                    if (why != null)
                    {
                        StartReturn(c, why);
                    }
                }

                if (c.TicksLeft <= 0)
                {
                    Fail(c, CourierFailReason.Expired);
                    continue;
                }

                // A hostile sender does NOT void the contract (that would hand out the cargo for free):
                // the job still stands, it just may not be completable until relations improve.
                bool issuerHostile = c.issuer.HostileTo(Faction.OfPlayer);
                if (issuerHostile && !c.issuerHostileNotified)
                {
                    c.issuerHostileNotified = true;
                    SendIssuerHostileLetter(c);
                }
                else if (!issuerHostile)
                {
                    c.issuerHostileNotified = false;
                }
            }

            // Keep the save small: finished contracts are only kept for a while.
            contracts.RemoveAll(c => !c.IsOpen && Find.TickManager.TicksGame - c.deadlineTick > GenDate.TicksPerYear);
        }

        private static string DestinationProblem(CourierContract c)
        {
            Settlement dest = c.destination;
            if (dest == null || dest.Destroyed || !dest.Spawned)
            {
                return "RC_ReturnWhy_Gone".Translate();
            }
            // Captured or re-owned settlements (e.g. with Rim War): the consignee is gone.
            if (dest.Faction == null || dest.Faction.IsPlayer || dest.Faction != c.destinationFaction)
            {
                return "RC_ReturnWhy_Gone".Translate();
            }
            // Hostile recipient. When the recipient is the sender itself, returning would be just as
            // impossible: the contract stays as it is and can be completed if relations improve.
            if (dest.Faction.HostileTo(Faction.OfPlayer) && dest.Faction != c.issuer)
            {
                return "RC_ReturnWhy_HostileToYou".Translate(dest.Faction.Name);
            }
            if (dest.Faction != c.issuer && dest.Faction.HostileTo(c.issuer))
            {
                return "RC_ReturnWhy_HostileToIssuer".Translate(dest.Faction.Name, c.issuer.Name);
            }
            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref contracts, "contracts", LookMode.Deep);
            Scribe_Values.Look(ref nextContractId, "nextContractId", 1);
            Scribe_Collections.Look(ref presentSettlementIds, "presentSettlementIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (presentSettlementIds == null)
                {
                    presentSettlementIds = new List<int>();
                }
                if (contracts == null)
                {
                    contracts = new List<CourierContract>();
                }
                contracts.RemoveAll(c => c == null);
            }
        }
    }
}
