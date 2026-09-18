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

## Current format

Format 2 supports:

- compact same-length byte replacements;
- length-changing files through copy/literal delta operations;
- deletion of files from full-replacement `encdata` / `mapGR` templates;
- raw RomFS files that already exist in the clean dump.

For length-changing files, unchanged ranges are referenced from the user's clean source file instead of being embedded in the manifest. Only unmatched literal bytes are stored.

The remaining intentional limitation is a legacy template that introduces a completely new game file that does not exist in the clean dump. Generation stops for that case instead of embedding the complete added file; it should be implemented with a semantic generator or another source-derived transformation.
