# Catch Zone patch manifests

This folder contains compact patch manifests used by pk3DS-Progressive to enable Catch Zones without distributing complete modified game files.

Expected files:

- `XY.json`
- `ORAS.json`

## One-time migration from the legacy templates

During development, if a manifest does not exist but `catch_zone_templates/XY` or `catch_zone_templates/ORAS` exists next to the executable, the **Enable Catch Zones** button can generate the manifest automatically.

The loaded RomFS must be a clean compatible dump. pk3DS will:

1. unpack clean `encdata` and `mapGR` from the user's RomFS;
2. compare them against the legacy modified templates;
3. compare legacy `raw` files against the corresponding files in the clean RomFS;
4. store only changed byte ranges, hashes and deletion instructions in the generated JSON;
5. apply the resulting manifest.

The generated JSON is written next to the executable under `catch_zone_patches/`.

After validating it on a second clean dump, copy the generated manifest into this source folder so it is included automatically in future builds/publishes.

## Current format limitations

Format 1 intentionally supports only:

- same-length byte replacements;
- deletion of files from full-replacement `encdata` / `mapGR` templates;
- raw RomFS files that already exist in the clean dump.

If a legacy template adds a new file or changes a file length, generation stops instead of embedding a complete replacement file. That case should be implemented with a semantic generator or a future delta format.
