using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    public static class CourierUtility
    {
        /// <summary>
        /// Something the camera can jump to for a thing that may be spawned, carried by a caravan
        /// pawn or held somewhere on a map.
        /// </summary>
        public static LookTargets LookTargetFor(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return LookTargets.Invalid;
            }
            if (thing.Spawned)
            {
                return new LookTargets(thing);
            }
            Caravan caravan = CaravanOf(thing);
            if (caravan != null)
            {
                return new LookTargets(caravan);
            }
            if (thing.MapHeld != null)
            {
                return new LookTargets(new TargetInfo(thing.PositionHeld, thing.MapHeld));
            }
            return LookTargets.Invalid;
        }

        /// <summary>The caravan whose pawn carries this thing, if any.</summary>
        public static Caravan CaravanOf(Thing thing)
        {
            IThingHolder holder = thing?.ParentHolder;
            while (holder != null)
            {
                if (holder is Caravan caravan)
                {
                    return caravan;
                }
                if (holder is Pawn pawn)
                {
                    Caravan pawnCaravan = pawn.GetCaravan();
                    if (pawnCaravan != null)
                    {
                        return pawnCaravan;
                    }
                }
                holder = holder.ParentHolder;
            }
            return null;
        }
    }
}
