# pk3DS Progressive

**pk3DS Progressive** is a free and open-source evolution of [pk3DS](https://github.com/kwsch/pk3DS), focused on deeper progression systems, configurable randomization, balancing, automation, quality-of-life features, and advanced customization for Pokémon 3DS games.

> **Free software, always.** The latest stable version of pk3DS Progressive is available for free. There are no paid builds, premium editions, memberships, or paywalled features.

## Project status

- **Current stable line:** v4.9.0
- **Development branch:** `Eris-Ultranova`
- **Platform:** Windows x64
- **License:** GNU GPL v3
- **Primary progressive feature target:** Pokémon Ultra Sun / Ultra Moon

pk3DS Progressive keeps the original pk3DS editing and randomization foundation while adding a growing set of systems designed around repeatable, progression-aware ROM customization.

## Current development highlights

The features below describe the current `Eris-Ultranova` development branch. Some were added after the v4.9.0 stable release and will be included in future stable releases after validation.

### Progressive randomization

- Progressive BST balancing
- Progressive wild encounter scaling
- Story-aware trainer scaling
- Trainer Level Caps
- Player Level Caps
- Totem Level Caps and Totem improvements
- Static, gift, and trade progression controls

### Trainer customization

- Better Movesets with optional TM support
- Smart Held Items with configurable quality
- Strong Stat move-selection logic
- Normal / Important / Boss trainer categories
- Maximum trainer team-size rules
- Random Double Battles with story-aware restrictions
- Double Battle AI support
- Configurable Trainer AI

### Templates and automation

- Global Randomization Template
- Batch ROM Builder
- Reusable trainer, wild, level-cap, and gameplay settings
- Cross-feature execution ordering so dependent systems can work together consistently

### Economy and item QoL

- Editable Fix Economy system
- Expanded Marts
- Optional Rare Candies and EV items in marts
- Persistent battle consumables
- Additional item and shop quality-of-life options

### USUM gameplay QoL

- Mega Evolution availability options
- All Pokémon Can Call Allies
- Move Relearner patches
- Trade quality-of-life improvements
- Static / gift / trade filters
- Additional configurable gameplay patches

Progressive-specific functionality is currently most developed for **Ultra Sun / Ultra Moon**. The original pk3DS functionality for other supported Nintendo 3DS Pokémon titles remains part of the project, but not every Progressive feature is available for every game.

## Downloads

Stable releases are distributed **for free** through this repository's [Releases](../../releases).

The latest published stable release is **v4.9.0**. The `Eris-Ultranova` branch contains newer development features that are not yet part of a published stable build.

New stable versions will be released after their features have been completed and validated.

pk3DS Progressive does **not** distribute ROMs, decrypted game files, copyrighted Pokémon assets, or Nintendo content.

## Requirements

For prebuilt releases:

- Windows x64
- A legally obtained copy of the supported game
- Properly dumped and extracted game files

For building from source:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A compiler supporting C# 14

Build from the repository root with:

```powershell
dotnet build .\pk3DS.slnx -c Release
```

To publish a self-contained Windows x64 build:

```powershell
dotnet publish .\pk3DS.WinForms\pk3DS.WinForms.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true
```

## Usage

1. Dump your own legally obtained Pokémon 3DS game.
2. Extract the required RomFS / ExeFS content using your preferred 3DS tooling.
3. Keep a backup of the original extracted files.
4. Open the extracted game in pk3DS Progressive.
5. Configure the randomization and gameplay options you want.
6. Save the modified files and rebuild or deploy them using your normal 3DS modding workflow.

Because many Progressive features can modify multiple game systems at once, keeping a clean backup is strongly recommended.

## Support, donations, and commissions

pk3DS Progressive itself is free.

Optional donations through Ko-fi help support continued development, but donating is never required to access stable builds or project features.

Paid **custom development commissions** are also available for requested features, tools, patches, integrations, workflows, or other project-specific programming work.

Commission fees pay for development time and programming work. They do not include the sale or distribution of ROMs, copyrighted game files, Pokémon assets, or Nintendo content.

## License

pk3DS Progressive is distributed under the **GNU General Public License v3.0**.

See [LICENSE.md](LICENSE.md) for the complete license text.

This project is based on the original [pk3DS](https://github.com/kwsch/pk3DS) project and the work of its contributors. pk3DS Progressive contains modifications and additional functionality built on that foundation.

## Credits

- [kwsch/pk3DS](https://github.com/kwsch/pk3DS) — original pk3DS project
- Original pk3DS contributors and the wider Project Pokémon community
- Contributors, testers, and users who help validate pk3DS Progressive

The original pk3DS discussion and community resources are available on [Project Pokémon](https://projectpokemon.org/home/forums/topic/34377-pk3ds-pok%C3%A9mon-3ds-rom-editor-and-randomizer/).

## Disclaimer

pk3DS Progressive is an independent community project.

It is not affiliated with, endorsed by, sponsored by, or associated with Nintendo, The Pokémon Company, Game Freak, or Creatures Inc.

Pokémon and related names and assets are trademarks and copyrights of their respective owners.
