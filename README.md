# PSO2 map generator

Given what the server sends in the `0x03-0x24` packet for a zone,
it produces **the same grid the client would produce**, without opening the game.

## Requirements

**.NET 8 Desktop Runtime (x64)** — https://dotnet.microsoft.com/download/dotnet/8.0
(the "Desktop Runtime" installer, not the SDK). The GUI needs the Desktop one; the CLI runs on
the plain Runtime. Windows x64.

## The projects

| | |
|---|---|
| `Pso2MapGen.Core` | the library: MT19937, the `.lps`/`.lat`/packet readers, the generator, the export |
| `Pso2MapGen.Cli` | the verification harness and the command line |
| `Pso2MapGen.Gui` | the window: fields on the left, preview on the right, generate and export |

```
dotnet build -c Release
```

The extracted game data is looked up from the executable outwards, walking up until an
`extracted/landform_common` turns up — so in a normal checkout it works with no configuration
at all. To keep the data elsewhere: the `PSO2_EXTRACTED` environment variable (for the CLI) or
`GameData.Root` (for the library).

### Rebuilding

```
dotnet publish Pso2MapGen.Gui -c Release -o ..\release\Pso2MapGen
dotnet publish Pso2MapGen.Cli -c Release -o ..\release\Pso2MapGen
```

Copy next to the two executables:


| `Pso2MapGen/extracted/landform_common/` | 220 `.lps`, each area's piece catalogue |
| `Pso2MapGen/extracted/lat/` | 3765 `.lat`, the fixed maps and the river skeletons |
| `bigchunks.txt`, `infotables.txt`, `models.txt` | the labels and the reference dumps |

## The GUI

```
Pso2MapGen.Gui.exe [packet.bin | quest.json | block.json] [zone | variant]
```

With no arguments it opens empty; with a `.bin`, a `quest.json` or a `block.json` it fills the
fields straight away (you can also drop any of them onto the executable). The three buttons —
**Open BIN/JSON**, **Generate map** and **Export / Save** — are pinned to the top so they never
scroll out of reach.

- **Open BIN/JSON** takes any of the three and decides for itself:
  - a `.bin` is a `0x03-0x24` capture: it fills the zone list, and picking a zone fills every
    field, including the mapA and mapB sides;
  - a `.json` is the server's own data, so you can see the map a `block.json` produces without
    opening the game. It loads the **sibling blocks** with it (`b1`, `b2`, ...), because one
    block alone cannot give a zone's entrance sides, and it **ignores every field it does not
    use** — `DropTables`, `EnemyID`, `Entrances` and the rest — as well as the `//` comments and
    trailing commas files may carry;
  - a **`quest.json`** opens the whole quest: a **Variant** box appears under the zone list and
    switching variant redraws the map, so you can walk through all without going back to
    the file dialog. Variants parked under `variant/disabled` are listed too, as
    `disabled/0`, and marked as switched off;
- **New map...** asks how many zones you want and starts a map from scratch; the `+` and `-`
  next to the list add and remove zones;
- **Generate map** draws the grid, with `--` and `|` only between cells that **really connect**
  (the `0x005EC5E0` rule), and with columns and rows lettered and numbered the same way as the
  in-game map grid;
- the third button is **Export...** or **Save**, depending on where the map came from:
  - a map from a **packet**, or one started with **New map...**, has no files of its own, so the
    button **exports**: it writes a fresh `block.json`, and with more than one zone it offers
    the whole map as `b1/block.json`, `b2/block.json`, ...
  - a map opened from a **`block.json`** already has its files, so the button **saves straight
    back into them**. Exporting is the road from a packet to a new block; a block that exists
    does not need it.

What the exported `block.json` holds:

| `LatNumber` — it is the `.lat` number, `sn_<model>_<n>.lat` |
| `MapWidth` / `MapHeight` — a ceiling, not the size of the map produced |
| `MapType` — 0 fixed, 1 generated, 2 river |
| `Unknown04` / `MapConfig` |
| `DecorationTable` / `SetFileTable` / `MapGenTable` |
| `MapWidth`, `MapHeight`, `ExitMinDistance`, `OpenBranchCap`, `ExtraPassFlag` |
| `HeightMode` (byte 0, the `gen->0x94`) and `MultiPartyArea` (byte 1) |
| `ZoneFlags` |
| `BigChunk1` / `BigChunk1Variant` / `BigChunk2` / `BigChunk2Variant` |
| `Unknown88`, `Unknown90`, `UnknownA0`, `UnknownA8`, `UnknownB0`, `UnknownB8`, `UnknownC0`, `UnknownC8`, `UnknownD0`, `Unknown14` |
| `Unknown*`, same offsets |

Where a field's meaning is not known it is named after its **offset** in the record, which is what
someone reading the client needs. `Unknown04` is ZoneSettings' `+0x04`; the `ZoneSettingsExtra`
record's own `+0x04` is `ExtraUnknown04`.

**Saving does not rewrite the file.** Only the *value* of the fields you actually changed is
swapped, in place; everything else stays byte for byte as it was — the `//` comments, the
commented-out blocks, the hand-made indentation, and every field the tool does not even read
(`DropTables`, `EnemyID`, `Entrances`, `Weather`, `Forecast`, `ForecastSFX`, `hasDayCycle`, ...).

**A hand-made zone no longer exports its InfoTables, nor its `ZoneSettingsExtra`, as zeros.** They do not touch the map,
but they do touch what the zone looks like, so a block full of zeros has to be fixed by hand
afterwards. A zone that came from a packet exports its own tables unchanged; one made in the
tool falls back to what that area usually has, from `infotables.txt` — keyed by
`<ModelId> <MapType> <ZoneIndex>` and falling back to `<ModelId> <MapType>` and then `<ModelId>`.
`MapGenTable.MapWidth`/`MapHeight` are not in that file: they are the zone's own `MapWidth`/
`MapHeight`.

**`BigChunk1Variant` and `BigChunk2Variant` are in the window now**, as dropdowns that follow the design beside them:
pick a chunk in `BigChunk1` and `BigChunk1Variant` offers the ICE variants that really exist for it. The table is
`bigchunks.txt`, beside the executable and editable like `models.txt`. It was measured against
the client's own files, and the answer is narrow — only **80** and **81** exist anywhere, 80 for
every chunk and 81 for seven of them. The variant never changes the layout: the generator groups
chunks by **name**, so two designs with the same name and different variants are one chunk in
the pool.

**The area names in the ModelId list come from `models.txt`**, a plain text file beside the
executable that is meant to be edited: one line per id, `330 = Ruins`, with `#` for comments.
An id that is not in the file just shows its number, and the file can be deleted without
consequence — the name is a label, and what the tool generates and exports is always the
number.

Every field has an **[i]** button that opens a window explaining what that field does to the
map — and saying so when it is **not known**, which is the case for a good many of the `Unk*`.

The two fields that **do not come from the packet** are exposed on purpose:

| | |
|---|---|
| `gen->0x94` | picks the row of the ramp-draw tables and the starting height. "Automatic" now reads it from the **packet**: it is byte 0 of `UnkO`, sign-extended, which is what `0x00647EE5` loads. It is per **zone**, not per area — in area 320 the corpus has zones with 0, with 1 and with 4 |
| did the `0x00647BB0` init block run? | when it does not, `0x94`, `0x98` and `0x9C` stay at their defaults and **there is no big-chunk demotion** |

## The command line

```
Pso2MapGen.Cli rng <seed> <count>             # the MT sequence and the three draw routines
Pso2MapGen.Cli lps <model>                    # the piece catalogue, with the 4 rotations
Pso2MapGen.Cli lat <model> <args> [--river]   # one `.lat`; also takes a path
Pso2MapGen.Cli models                         # the models that have a `.lps` on disk
Pso2MapGen.Cli bigchunks [model]              # the big chunks and their ICE variants
Pso2MapGen.Cli height                         # the areas where gen->0x94 has any effect
Pso2MapGen.Cli new  <zones> --model N [...] [--out <folder>]   # a map from scratch
Pso2MapGen.Cli show --packet <bin> --zone N [--doors]
Pso2MapGen.Cli show --block <block.json> [--zone N]      # the same, from the server's data
Pso2MapGen.Cli quest <quest.json> [--variant N] [--json] [--folder]   # a whole quest
Pso2MapGen.Cli gen  --packet <bin> --zone N   # the dump that is compared against Python
Pso2MapGen.Cli gen  --model N --seed N --sx N --sy N [--unkb N --args N ...]
Pso2MapGen.Cli sweep <folder> [--limit N]     # generates every zone of a tree of packets
Pso2MapGen.Cli dump  <folder> [--limit N]     # the same, as a single dump
Pso2MapGen.Cli check <cases.json> [-v]        # against the client's own dumps
Pso2MapGen.Cli export --packet <bin> --zone N [--out <file>]
Pso2MapGen.Cli export --block <block.json> [--zone N] [--out <file>]
Pso2MapGen.Cli export ... --out-dir <folder>   # the whole map, as b1/, b2/, ...
Pso2MapGen.Cli export ... --sides-a 2 --sides-b 0   # with the sides changed
Pso2MapGen.Cli save   --block <block.json> [--zone N] [--dry-run]   # in place, no rewrite
Pso2MapGen.Cli save   ... --seed N --size 5x6 --sides-a 2 --sides-b 0
```

`--block` takes a file or the folder that holds the `b*` folders, and `--zone` picks the zone;
without it, the zone of the file you named.

