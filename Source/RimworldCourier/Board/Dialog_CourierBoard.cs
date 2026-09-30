using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    public class Dialog_CourierBoard : Window
    {
        private const float HeaderHeight = 66f;
        private const float FooterHeight = 42f;
        // Four lines of text fit in a row: silver/chemfuel, goodwill, honor, food warning.
        private const float LineHeight = 22f;
        private const float RowHeight = LineHeight * 4f + 16f;
        private const float RowGap = 6f;
        private const float CargoWidth = 240f;
        private const float DestinationWidth = 260f;
        private const float ColumnGap = 10f;
        private static readonly Color TitleColor = new Color(0.95f, 0.82f, 0.45f);

        private readonly Caravan caravan;
        private readonly Settlement settlement;
        private readonly WorldObjectComp_CourierBoard board;

        private List<CourierOffer> offers;
        private string emptyReason;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(1000f, 680f);

        public Dialog_CourierBoard(Caravan caravan, Settlement settlement, WorldObjectComp_CourierBoard board)
        {
            this.caravan = caravan;
            this.settlement = settlement;
            this.board = board;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            // Register the arrival right now, in case the periodic check has not run yet.
            CourierWorldComponent.Instance?.UpdateVisits();
            Rebuild();
        }

        private void Rebuild()
        {
            offers = CourierOfferGenerator.Generate(caravan, settlement, board, out emptyReason);
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight));

            Rect listRect = new Rect(inRect.x, inRect.y + HeaderHeight, inRect.width, inRect.height - HeaderHeight - FooterHeight);
            if (offers.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(listRect, emptyReason ?? "RC_Board_NoFeasible".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, offers.Count * (RowHeight + RowGap));
                Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
                float y = 0f;
                // Accepting an offer rebuilds the list, so iterate over a copy.
                foreach (CourierOffer offer in offers.ToArray())
                {
                    DrawOffer(new Rect(0f, y, viewRect.width, RowHeight), offer);
                    y += RowHeight + RowGap;
                }
                Widgets.EndScrollView();
            }

            Rect closeRect = new Rect(inRect.center.x - 80f, inRect.yMax - 35f, 160f, 35f);
            if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
            {
                Close();
            }
        }

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "RC_BoardTitle".Translate(settlement.LabelCap));
            Text.Font = GameFont.Small;

            CourierTransportMode mode = CourierTransport.For(caravan);
            string subtitle = "RC_BoardSubtitle".Translate(
                settlement.Faction.Name,
                ("RC_Transport_" + mode.Kind(caravan)).Translate(),
                mode.FreeMass(caravan).ToStringMass());
            if (CourierRoyalty.IsEmpire(settlement.Faction))
            {
                subtitle += " | " + "RC_BoardImperial".Translate((CourierRoyalty.EmpirePayMultiplier - 1f).ToStringPercent());
            }
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x, rect.y + 34f, rect.width, 26f), subtitle);
            GUI.color = Color.white;
        }

        private void DrawOffer(Rect rect, CourierOffer offer)
        {
            Widgets.DrawMenuSection(rect);
            Widgets.DrawHighlightIfMouseover(rect);
            Rect inner = rect.ContractedBy(8f);

            // One line per label: nothing may wrap onto a second, clipped line.
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;

            // Cargo icon and label.
            Rect iconRect = new Rect(inner.x, inner.y + (inner.height - 64f) / 2f, 64f, 64f);
            Widgets.ThingIcon(iconRect, offer.cargoDef, offer.cargoStuff);

            float x = iconRect.xMax + 12f;
            float cargoWidth = CargoWidth;
            GUI.color = TitleColor;
            Widgets.Label(new Rect(x, inner.y, cargoWidth, LineHeight), offer.Title.Truncate(cargoWidth));
            GUI.color = Color.white;
            Widgets.Label(new Rect(x, inner.y + LineHeight, cargoWidth, LineHeight), offer.CargoLabel.Truncate(cargoWidth));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(x, inner.y + LineHeight * 2f, cargoWidth, LineHeight),
                "RC_OfferCargoDetails".Translate(offer.cargoMass.ToStringMass(), offer.cargoValue.ToStringMoney()).Truncate(cargoWidth));
            GUI.color = Color.white;

            // Destination and timing.
            x += cargoWidth + ColumnGap;
            float destWidth = DestinationWidth;
            Widgets.Label(new Rect(x, inner.y, destWidth, LineHeight), "RC_OfferTo".Translate(offer.destination.LabelCap).Truncate(destWidth));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(x, inner.y + LineHeight, destWidth, LineHeight), offer.destination.Faction.Name.Truncate(destWidth));
            GUI.color = Color.white;
            Widgets.Label(new Rect(x, inner.y + LineHeight * 2f, destWidth, LineHeight),
                "RC_OfferTiming".Translate(offer.travelTicks.ToStringTicksToDays("0.0"), offer.deadlineTicks.ToStringTicksToDays("0.0")).Truncate(destWidth));

            // Reward (one line each), food warning and accept button.
            x += destWidth + ColumnGap;
            float acceptWidth = 110f;
            float rewardWidth = inner.xMax - x - acceptWidth - ColumnGap;
            float lineY = inner.y;
            foreach (string line in RewardLines(offer))
            {
                Widgets.Label(new Rect(x, lineY, rewardWidth, LineHeight), line.Truncate(rewardWidth));
                lineY += LineHeight;
            }
            if (offer.foodWarning)
            {
                GUI.color = ColorLibrary.Yellow;
                Widgets.Label(new Rect(x, lineY, rewardWidth, LineHeight), "RC_OfferFoodWarning".Translate().Truncate(rewardWidth));
                GUI.color = Color.white;
            }

            Text.WordWrap = wordWrap;

            AcceptanceReport report = CourierOfferGenerator.CanAccept(offer, caravan, settlement, board);
            Rect acceptRect = new Rect(inner.xMax - acceptWidth, inner.y + (inner.height - 36f) / 2f, acceptWidth, 36f);
            if (Widgets.ButtonText(acceptRect, "RC_Accept".Translate(), active: report.Accepted))
            {
                if (!report.Accepted)
                {
                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                }
                else
                {
                    board.MarkTaken(offer.index);
                    CourierWorldComponent.Instance.Accept(offer, caravan, settlement);
                    Rebuild();
                }
            }
            if (!report.Accepted && !report.Reason.NullOrEmpty())
            {
                TooltipHandler.TipRegion(acceptRect, report.Reason);
            }

            TooltipHandler.TipRegion(new Rect(rect.x, rect.y, acceptRect.x - rect.x, rect.height), OfferTooltip(offer));
        }

        private List<string> RewardLines(CourierOffer offer)
        {
            return CourierOfferGenerator.RewardLines(offer.rewardSilver, offer.rewardGoodwill, offer.rewardChemfuel, settlement.Faction,
                offer.rewardHonor, offer.rewardHonor > 0 ? CourierRoyalty.BestTitledPawn(caravan, settlement.Faction) : null);
        }

        private string OfferTooltip(CourierOffer offer)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(CourierOfferGenerator.Story(offer.reason, settlement.Faction, offer.CargoLabel, offer.destination.LabelCap, offer.destination.Faction));
            sb.AppendLine();
            sb.AppendLine("RC_TipReward".Translate(CourierOfferGenerator.RewardLabel(offer.rewardSilver, offer.rewardGoodwill, offer.rewardChemfuel,
                settlement.Faction, offer.rewardHonor, offer.rewardHonor > 0 ? CourierRoyalty.BestTitledPawn(caravan, settlement.Faction) : null)));
            sb.AppendLine("RC_TipDistance".Translate(offer.distanceTiles.ToString("0")));
            sb.AppendLine("RC_TipTravel".Translate(offer.travelTicks.ToStringTicksToPeriod()));
            sb.AppendLine("RC_TipDeadline".Translate(offer.deadlineTicks.ToStringTicksToPeriod()));
            sb.AppendLine();
            sb.AppendLine("RC_TipSealed".Translate());
            CourierContract preview = new CourierContract { cargoValue = offer.cargoValue };
            sb.Append("RC_TipFailure".Translate(
                CourierWorldComponent.FailurePenalty(preview, CourierFailReason.SealBroken),
                CourierWorldComponent.FailurePenalty(preview, CourierFailReason.CargoLost)));
            return sb.ToString();
        }
    }
}
