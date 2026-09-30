using UnityEngine;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Entry point. RimWorld instantiates every <see cref="Mod"/> subclass it finds while loading
    /// assemblies, so this is where the settings are read.
    /// </summary>
    public class CourierMod : Mod
    {
        public static CourierSettings Settings { get; private set; }

        public CourierMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CourierSettings>();
        }

        public override string SettingsCategory()
        {
            return "RC_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}
