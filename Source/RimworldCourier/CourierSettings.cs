using UnityEngine;
using Verse;

namespace RimworldCourier
{
    public class CourierSettings : ModSettings
    {
        // Job board
        public int minJobs = 1;
        public int maxJobs = 5;
        public float boardRefreshDays = 3f;
        public float boardRestockHours = 12f;

        // Feasibility
        public float cargoMassFraction = 0.85f;
        public float deadlineSlack = 1.75f;
        public float maxTravelDays = 15f;

        // Economy
        public float payMultiplier = 1f;
        public float maxCargoValue = 4000f;

        // Failure
        public int failureBaseGoodwill = 8;
        public float failureSilverPerGoodwill = 50f;
        public float returnGraceDays = 7f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minJobs, "minJobs", 1);
            Scribe_Values.Look(ref maxJobs, "maxJobs", 5);
            Scribe_Values.Look(ref boardRefreshDays, "boardRefreshDays", 3f);
            Scribe_Values.Look(ref boardRestockHours, "boardRestockHours", 12f);
            Scribe_Values.Look(ref cargoMassFraction, "cargoMassFraction", 0.85f);
            Scribe_Values.Look(ref deadlineSlack, "deadlineSlack", 1.75f);
            Scribe_Values.Look(ref maxTravelDays, "maxTravelDays", 15f);
            Scribe_Values.Look(ref payMultiplier, "payMultiplier", 1f);
            Scribe_Values.Look(ref maxCargoValue, "maxCargoValue", 4000f);
            Scribe_Values.Look(ref failureBaseGoodwill, "failureBaseGoodwill", 8);
            Scribe_Values.Look(ref failureSilverPerGoodwill, "failureSilverPerGoodwill", 50f);
            Scribe_Values.Look(ref returnGraceDays, "returnGraceDays", 7f);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("RC_Settings_Board".Translate());
            list.GapLine();
            minJobs = Mathf.RoundToInt(list.SliderLabeled("RC_Settings_MinJobs".Translate(minJobs), minJobs, 1f, 5f));
            maxJobs = Mathf.RoundToInt(list.SliderLabeled("RC_Settings_MaxJobs".Translate(maxJobs), maxJobs, 1f, 8f));
            if (maxJobs < minJobs)
            {
                maxJobs = minJobs;
            }
            boardRefreshDays = RoundTo(list.SliderLabeled("RC_Settings_RefreshDays".Translate(boardRefreshDays.ToString("0.#")), boardRefreshDays, 1f, 15f), 0.5f);
            boardRestockHours = RoundTo(list.SliderLabeled("RC_Settings_RestockHours".Translate(boardRestockHours.ToString("0")), boardRestockHours, 0f, 72f), 1f);

            list.Gap();
            list.Label("RC_Settings_Feasibility".Translate());
            list.GapLine();
            cargoMassFraction = RoundTo(list.SliderLabeled("RC_Settings_CargoFraction".Translate(cargoMassFraction.ToStringPercent()), cargoMassFraction, 0.3f, 1f), 0.05f);
            deadlineSlack = RoundTo(list.SliderLabeled("RC_Settings_DeadlineSlack".Translate(deadlineSlack.ToString("0.00")), deadlineSlack, 1.2f, 3f), 0.05f);
            maxTravelDays = RoundTo(list.SliderLabeled("RC_Settings_MaxTravelDays".Translate(maxTravelDays.ToString("0")), maxTravelDays, 3f, 30f), 1f);

            list.Gap();
            list.Label("RC_Settings_Economy".Translate());
            list.GapLine();
            payMultiplier = RoundTo(list.SliderLabeled("RC_Settings_PayMultiplier".Translate(payMultiplier.ToStringPercent()), payMultiplier, 0.25f, 3f), 0.05f);
            maxCargoValue = RoundTo(list.SliderLabeled("RC_Settings_MaxCargoValue".Translate(maxCargoValue.ToString("0")), maxCargoValue, 500f, 20000f), 250f);

            list.Gap();
            list.Label("RC_Settings_Failure".Translate());
            list.GapLine();
            failureBaseGoodwill = Mathf.RoundToInt(list.SliderLabeled("RC_Settings_FailureBase".Translate(failureBaseGoodwill), failureBaseGoodwill, 0f, 30f));
            failureSilverPerGoodwill = RoundTo(list.SliderLabeled("RC_Settings_FailurePerSilver".Translate(failureSilverPerGoodwill.ToString("0")), failureSilverPerGoodwill, 10f, 200f), 5f);
            returnGraceDays = RoundTo(list.SliderLabeled("RC_Settings_ReturnGrace".Translate(returnGraceDays.ToString("0.#")), returnGraceDays, 2f, 20f), 0.5f);

            list.Gap();
            if (list.ButtonText("RC_Settings_Reset".Translate()))
            {
                ResetToDefaults();
            }

            list.End();
        }

        private void ResetToDefaults()
        {
            CourierSettings defaults = new CourierSettings();
            minJobs = defaults.minJobs;
            maxJobs = defaults.maxJobs;
            boardRefreshDays = defaults.boardRefreshDays;
            boardRestockHours = defaults.boardRestockHours;
            cargoMassFraction = defaults.cargoMassFraction;
            deadlineSlack = defaults.deadlineSlack;
            maxTravelDays = defaults.maxTravelDays;
            payMultiplier = defaults.payMultiplier;
            maxCargoValue = defaults.maxCargoValue;
            failureBaseGoodwill = defaults.failureBaseGoodwill;
            failureSilverPerGoodwill = defaults.failureSilverPerGoodwill;
            returnGraceDays = defaults.returnGraceDays;
        }

        private static float RoundTo(float value, float step)
        {
            return Mathf.Round(value / step) * step;
        }
    }
}
