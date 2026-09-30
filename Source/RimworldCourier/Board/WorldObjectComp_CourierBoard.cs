using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    public class WorldObjectCompProperties_CourierBoard : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_CourierBoard()
        {
            compClass = typeof(WorldObjectComp_CourierBoard);
        }
    }

    /// <summary>
    /// Added to every Settlement (see Patches). Shows the courier board and the hand-over command
    /// to a player caravan visiting the settlement, the same way vanilla's TradeRequestComp does.
    ///
    /// The board only changes between visits. A visit starts when a player caravan arrives while
    /// none was there (see CourierWorldComponent.UpdateVisits). During a visit, taken jobs are
    /// never replaced: you have to leave and come back.
    /// </summary>
    public class WorldObjectComp_CourierBoard : WorldObjectComp
    {
        /// <summary>Fallback jobs use their own slot range, one block per visit.</summary>
        private const int FallbackSlotBase = 1000000;
        private const int FallbackSlotsPerVisit = 32;

        private int boardPeriod = -1;
        private int boardSeed;
        private int visitIndex;
        private int visitStartTick = -1;
        private List<int> takenSlots = new List<int>();
        private List<int> takenTicks = new List<int>();

        public Settlement Settlement => parent as Settlement;

        public int BoardSeed
        {
            get
            {
                EnsureInitialized();
                return boardSeed;
            }
        }

        public bool IsTaken(int slot)
        {
            EnsureInitialized();
            return takenSlots.Contains(slot);
        }

        public void MarkTaken(int slot)
        {
            EnsureInitialized();
            if (!takenSlots.Contains(slot))
            {
                takenSlots.Add(slot);
                takenTicks.Add(Find.TickManager.TicksGame);
            }
        }

        /// <summary>Whether a job was accepted from this board during the current visit.</summary>
        public bool TakenThisVisit
        {
            get
            {
                EnsureInitialized();
                for (int i = 0; i < takenTicks.Count; i++)
                {
                    if (takenTicks[i] >= visitStartTick)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Whether a job was taken from this board more recently than the restock time.</summary>
        public bool TakenWithinRestockTime
        {
            get
            {
                EnsureInitialized();
                int restockTicks = Mathf.RoundToInt(CourierMod.Settings.boardRestockHours * GenDate.TicksPerHour);
                int now = Find.TickManager.TicksGame;
                for (int i = 0; i < takenTicks.Count; i++)
                {
                    if (now - takenTicks[i] < restockTicks)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public int FallbackSlot(int n) => FallbackSlotBase + (visitIndex % 10000) * FallbackSlotsPerVisit + n;

        /// <summary>Ticks until the board may show a completely new set of jobs (on the next visit after that).</summary>
        public int TicksUntilRefresh
        {
            get
            {
                int period = PeriodTicks;
                return period - Find.TickManager.TicksGame % period;
            }
        }

        private static int PeriodTicks => Mathf.Max(GenDate.TicksPerHour, Mathf.RoundToInt(CourierMod.Settings.boardRefreshDays * GenDate.TicksPerDay));

        /// <summary>A player caravan has just arrived at this settlement with none already there.</summary>
        public void Notify_VisitStarted()
        {
            visitIndex++;
            visitStartTick = Find.TickManager.TicksGame;
            int period = Find.TickManager.TicksGame / PeriodTicks;
            if (period != boardPeriod)
            {
                boardPeriod = period;
                boardSeed = Gen.HashCombineInt(Gen.HashCombineInt(Find.World.info.Seed, parent.ID), period);
                takenSlots.Clear();
                takenTicks.Clear();
            }
        }

        private void EnsureInitialized()
        {
            if (boardPeriod < 0 || visitStartTick < 0)
            {
                Notify_VisitStarted();
            }
        }

        /// <summary>
        /// Slot indices shown on a board of <paramref name="baseSlots"/> positions.
        /// Slots form an endless sequence 0, 1, 2...: a taken slot keeps its position (empty) and is
        /// only replaced by the next slot of the sequence on a later visit, once the restock time
        /// has passed.
        /// </summary>
        public List<int> VisibleSlots(int baseSlots)
        {
            EnsureInitialized();
            int restockTicks = Mathf.RoundToInt(CourierMod.Settings.boardRestockHours * GenDate.TicksPerHour);
            List<int> visible = new List<int>();
            int positions = 0;
            int index = 0;
            while (positions < baseSlots)
            {
                int taken = takenSlots.IndexOf(index);
                if (taken < 0)
                {
                    visible.Add(index);
                    positions++;
                }
                else
                {
                    int takenTick = takenTicks[taken];
                    bool replaced = takenTick < visitStartTick && visitStartTick - takenTick >= restockTicks;
                    if (!replaced)
                    {
                        positions++;
                    }
                }
                index++;
            }
            return visible;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            if (!caravan.IsPlayerControlled || CaravanVisitUtility.SettlementVisitedNow(caravan) != parent)
            {
                yield break;
            }
            Settlement settlement = Settlement;
            if (settlement?.Faction == null || settlement.Faction.IsPlayer)
            {
                yield break;
            }

            Command_Action board = new Command_Action
            {
                defaultLabel = "RC_CommandBoard".Translate(),
                defaultDesc = "RC_CommandBoardDesc".Translate(settlement.LabelCap),
                icon = CourierTextures.BoardIcon,
                // With several caravans selected, open one board, not one per caravan.
                alsoClickIfOtherInGroupClicked = false,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_CourierBoard(caravan, settlement, this));
                }
            };
            AcceptanceReport title = CourierRoyalty.CanWorkFor(caravan, settlement.Faction);
            if (!title.Accepted)
            {
                board.Disable(title.Reason);
            }
            yield return board;

            CourierWorldComponent contracts = CourierWorldComponent.Instance;
            if (contracts == null)
            {
                yield break;
            }
            foreach (CourierContract contract in contracts.HandOverableAt(caravan, settlement))
            {
                yield return CourierCommands.HandOverCommand(contract, caravan);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref boardPeriod, "courierBoardPeriod", -1);
            Scribe_Values.Look(ref boardSeed, "courierBoardSeed");
            Scribe_Values.Look(ref visitIndex, "courierVisitIndex");
            Scribe_Values.Look(ref visitStartTick, "courierVisitStartTick", -1);
            Scribe_Collections.Look(ref takenSlots, "courierTakenSlots", LookMode.Value);
            Scribe_Collections.Look(ref takenTicks, "courierTakenTicks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (takenSlots == null)
                {
                    takenSlots = new List<int>();
                }
                // Saves from v0.1 have no timestamps: treat those slots as taken just now.
                if (takenTicks == null)
                {
                    takenTicks = new List<int>();
                }
                while (takenTicks.Count < takenSlots.Count)
                {
                    takenTicks.Add(Find.TickManager.TicksGame);
                }
            }
        }
    }
}
