# Rimworld Courier

RimWorld 1.6 mod: player caravans work as couriers, carrying sealed crates between friendly/neutral settlements for silver, goodwill and (with vehicles or a shuttle) chemfuel.

## Folder layout

```
Rimworld Courier/
├─ RimworldCourier/          <- the mod RimWorld loads (the only folder you publish)
│  ├─ About/About.xml
│  ├─ LoadFolders.xml
│  ├─ Languages/English/Keyed/
│  ├─ Textures/Things/Item/RC_CourierCrate.png
│  └─ 1.6/
│     ├─ Assemblies/RimworldCourier.dll          (built)
│     ├─ Defs/  Patches/
│     └─ Compat/VehicleFramework/                 (loaded only if Vehicle Framework is active)
│        ├─ Assemblies/RimworldCourier.VehicleFramework.dll  (built)
│        └─ Patches/
├─ Source/                   <- C# projects (RimworldCourier.sln)
├─ Art/                      <- art sources (scripts, previews); not part of the mod
```

## Build

Requirements: .NET SDK 8 (or Visual Studio 2022 / Rider). No NuGet packages: the projects reference the game's own DLLs.

```
cd Source
dotnet build RimworldCourier.sln -c Release
```

Paths default to `D:\SteamLibrary\steamapps\common\RimWorld` and the Workshop folder next to it. Override if needed:

```
dotnet build RimworldCourier.sln -c Release -p:RimWorldDir="E:\Games\RimWorld" -p:WorkshopDir="E:\Steam\steamapps\workshop\content\294100"
```

The Vehicle Framework project needs VF installed (Workshop id 3014915404). To build only the core: `dotnet build RimworldCourier/RimworldCourier.csproj -c Release`.

## Test in game

Link the mod folder into RimWorld's Mods folder once (Windows `cmd`, no admin needed):

```
mklink /J "<RimWorld folder>\Mods\RimworldCourier" "<this repository>\RimworldCourier"
```

Then enable *Rimworld Courier* in the mod list, form a caravan and visit a neutral or friendly settlement: the **Courier board** command appears next to Trade.

## How it works

- **Board**: `WorldObjectComp_CourierBoard` is patched onto every `Settlement` (same mechanism as vanilla trade requests, no Harmony). Jobs are seeded per settlement and refresh every N days, so reopening the board does not reroll them; quantities and pay adapt to the caravan looking at it.
- **Feasibility**: `CourierTransportMode` decides free capacity, reachability and travel time. Implementations: on foot (vanilla), Odyssey passenger shuttle, Vehicle Framework (compat assembly). A job is only offered if the caravan can carry it, reach the destination and make it within the deadline (travel estimate x slack).
- **Cargo**: `CourierCrate` holds the real goods in a sealed container. It is not tradeable, not food, not fuel, never ticks (no spoilage). Its mass is its own 5 kg plus the goods' mass (`StatPart_CourierCrateContentsMass`); offers leave room for both. The goods are a deep-saved list, not a `ThingOwner`, so they don't count as colony wealth (raid points) while a crate sits in your colony.
- **Contracts**: `CourierWorldComponent` tracks the crate, not the caravan (shuttle launches and caravan splits recreate caravans). Deadline, lost or abandoned crate, broken seal -> failure with a goodwill penalty that grows with the cargo value. Destination destroyed/captured, hostile to you or to the sender -> the cargo must be returned to any settlement of the sender. Sender defeated -> contract void (the crate is yours).
- **Hostile sender** (1.0): if the sender turns hostile mid-delivery (e.g. one of their people dies on your map), the contract is *not* voided, otherwise the cargo would be free. It still stands: deliver it (you still get paid), or, if the destination is theirs too or the cargo is being returned, wait for relations to improve before the deadline. A letter explains which case applies.
- **Quests and letters** (v0.2): accepting a job creates a vanilla quest (Quests tab, live countdown, "View quest" on the destination, alert when less than a day is left). Accepted / completed / failed / return / void each send a letter linked to the quest.
- **Visits** (v0.3): the board only changes between visits. A visit starts when a player caravan reaches the settlement's tile while none was there (splitting or fake departures don't count). Taken jobs are replaced on a later visit, once the restock time has passed. A visit that starts with nothing feasible gets one fallback job; once a job is taken during a visit, nothing new appears until the caravan leaves and comes back.
- **Cargo groups and reasons** (v0.3): `CourierCargoGroupDef` (Defs/CourierDefs/RC_CargoGroups.xml) picks the kind of goods by weight, then a thing inside it; weapons and apparel get a stuff. `CourierReasonDef` (RC_Reasons.xml) gives each job a title and a story sentence, depending on the cargo group and on whether the destination belongs to the sender's faction. Both are plain defs: easy to extend or translate.

- **DLC** (v0.4): `1.6/Compat/<DLC>/` folders are loaded only with that DLC (LoadFolders.xml).
  - Royalty: Empire boards need a colonist titled Yeoman or higher in the caravan (`CourierRoyalty`); imperial jobs pay x1.15 and 15% of them also give 1-3 honor to that colonist. Empire-only reasons use `issuerFactionDefs`.
  - Biotech: hemogen, genetic material (genepacks, embryos, ova), subcores.
  - Ideology: stand-in relics (`RC_Relic_*`) drawn with the vanilla relic textures; real relics are tied to precepts and can't be freight.
  - Anomaly: tomes and shards, bioferrite, disruptor flare packs.
  - Ideology precepts on organ use block organ jobs for factions that forbid them.

## Art

`Art/CourierCrate/hardcase.py` draws the crate texture (SVG shapes + weathering) and writes the PNGs; the mod uses the 128 px sealed version in `Textures/Things/Item/RC_CourierCrate.png`.

`Art/WorkshopCards/` holds the section cards of the Steam Workshop page (`make_cards.py` draws them with RimWorld's own UI font and icons, which are not included here).

`Art/Preview/` holds the preview artwork: `preview_source_full.png` is the full-size source of `About/Preview.png` (640x360, well under Steam's 1 MB limit); `compose.py` draws the first layout it was based on.

## Not done yet

- Compensation demand for very valuable stolen cargo (currently: goodwill penalty only).
- Command icons (still vanilla placeholders).
- Vehicle Framework assembly: compiled against VF 1.6 (Workshop 3014915404), not tested in game yet.
- In-game testing and balancing.
