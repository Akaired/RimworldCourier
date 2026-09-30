using RimWorld;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    [DefOf]
    public static class CourierDefOf
    {
        public static ThingDef RC_CourierCrate;

        public static QuestScriptDef RC_CourierDelivery;

        public static HistoryEventDef RC_CourierDeliveryCompleted;
        public static HistoryEventDef RC_CourierContractFailed;
        public static HistoryEventDef RC_CourierCargoStolen;

        static CourierDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CourierDefOf));
        }
    }

    /// <summary>
    /// Command icons. These are vanilla textures used as placeholders until the mod has its own art.
    /// Every command needs an icon: Vehicle Framework hides icon-less commands on vehicle caravans.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CourierTextures
    {
        public static readonly Texture2D BoardIcon = ContentFinder<Texture2D>.Get("UI/Commands/ViewQuest");
        public static readonly Texture2D DeliverIcon = ContentFinder<Texture2D>.Get("UI/Commands/FulfillTradeRequest");
        public static readonly Texture2D ReturnIcon = ContentFinder<Texture2D>.Get("UI/Commands/OfferGifts");
        public static readonly Texture2D BreakSealIcon = ContentFinder<Texture2D>.Get("UI/Commands/HoldOpen");
    }
}
