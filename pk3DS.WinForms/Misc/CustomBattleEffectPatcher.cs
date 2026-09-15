using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pk3DS.WinForms;

internal static class CustomBattleEffectPatcher
{
    internal sealed class BattlePatchRequest
    {
        public int Move { get; init; }
        public bool KingShieldAttackMinusOne { get; init; }
        public string BattlePatch { get; init; } = string.Empty;
        public int? Param0x0B { get; init; }
    }

    internal static void ApplyMoveDataSpecials(int generation, int move, byte[] data, bool kingShieldAttackMinusOne, string battlePatch)
    {
        DumpSpecificMoveDataForBalanceDebug(generation, move, data, battlePatch);
        if (data is null || data.Length <= 0x1D)
            return;

        if (kingShieldAttackMinusOne || HasPatchToken(battlePatch, "KingShieldMinusOne", "Gen6KingShieldMinusOne", "Gen7KingShieldMinusOne", "EscudoRealMinusOne"))
            SetKingShieldAttackDrop(data);
    
        if (generation == 7 && HasPatchToken(battlePatch, "MagicCoatThreeTurns", "Gen7MagicCoatThreeTurns", "CapaMagica3Turnos"))
        {
            SetTurnDuration(data, 3, 3);
            DumpMagicCoatMoveData(move, data, battlePatch);
        }
        Gen7MeditateV73Patcher.ConfigureMoveData(generation, move, data, battlePatch);
        Gen7FairyLockV11Patcher.ConfigureMoveData(generation, move, data, battlePatch);
        Gen7LuckyChantCritPatcher.ConfigureMoveData(generation, move, data, battlePatch);
        Gen7MistOwnerActivePatcher.ConfigureMoveData(generation, move, data, battlePatch);}

    internal static int ApplyExternalPatches(int generation, IEnumerable<BattlePatchRequest> requests, Action<string, string> alert)
    {
        if (requests is null)
            return 0;

        var list = requests.ToArray();
        int changed = 0;
        bool failed = false;

        if (generation == 7 && requests is not null && requests.Any(IsGen7NightmareSleepV76Request))
        {
            int nightmareV76Changed = Gen7NightmareSleepV76Patcher.Apply();
            if (nightmareV76Changed < 0)
            {
                failed = true;
                alert?.Invoke(
                    "Special battle patch skipped",
                    "Could not apply Gen7 Nightmare / Pesadilla awake-to-Sleep v76 to Battle.cro.");
            }
            else
            {
                changed += nightmareV76Changed;
            }
        }

        if (list.Any(z => IsKingShieldMinusOneRequest(z, generation)))
        {
            int result = generation switch
            {
                6 => PatchGen6KingShieldMinusOne(),
                7 => PatchGen7KingShieldMinusOne(),
                _ => -1,
            };

            if (result < 0)
            {
                failed = true;
                if (alert is not null)
                    alert("Special battle patch skipped", $"Could not apply King's Shield -1 Attack battle patch for Gen{generation}.");
            }
            else
            {
                changed += result;
            }
        }

        if (generation == 7)
        {
            var sportRequests = list.Where(IsGen7WaterMudSportRequest).ToArray();
            if (sportRequests.Length != 0)
            {
                int result = PatchGen7WaterMudSportReduction(sportRequests);
                if (result < 0)
                {
                    failed = true;
                    if (alert is not null)
                        alert("Special battle patch skipped", "Could not apply the Gen7 Water Sport / Mud Sport Battle.cro reduction patch.");
                }
                else
                {
                    changed += result;
                }
            }

            if (list.Any(IsGen7WishPivotRequest))
            {
                int result = PatchGen7WishPivot();
                if (result < 0)
                {
                    failed = true;
                    if (alert is not null)
                        alert("Special battle patch skipped", "Could not apply the Gen7 Wish / Deseo immediate-pivot Battle.cro patch.");
                }
                else
                {
                    changed += result;
                }
            }
        }

                if (generation == 7 && list.Any(Gen7MeditateV73Patcher.IsRequested))
        {
            int result = Gen7MeditateV73Patcher.Apply();

            if (result < 0)
            {
                failed = true;
                if (alert is not null)
                {
                    alert(
                        "Special battle patch skipped",
                        "Could not apply the validated Gen7 Meditate / Meditacion v7.3 Battle.cro patch.");
                }
            }
            else
            {
                changed += result;
            }
        }
        if (generation == 7 && list.Any(Gen7FairyLockV11Patcher.IsRequested))
        {
            int result = Gen7FairyLockV11Patcher.Apply();

            if (result < 0)
            {
                failed = true;
                if (alert is not null)
                {
                    alert(
                        "Special battle patch skipped",
                        "Could not apply the validated Gen7 Fairy Lock / Cerrojo Feerico v11 Battle.cro patch.");
                }
            }
            else
            {
                changed += result;
            }
        }
        if (generation == 7 && list.Any(Gen7LuckyChantCritPatcher.IsRequested))
        {
            int result = Gen7LuckyChantCritPatcher.Apply();

            if (result < 0)
            {
                failed = true;
                if (alert is not null)
                {
                    alert(
                        "Special battle patch skipped",
                        "Could not apply the Gen7 Lucky Chant / Conjuro protected-side +1 critical-stage Battle.cro patch.");
                }
            }
            else
            {
                changed += result;
            }
        }
        if (generation == 7 && list.Any(Gen7MistOwnerActivePatcher.IsRequested))
        {
            int result = Gen7MistOwnerActivePatcher.Apply();

            if (result < 0)
            {
                failed = true;
                if (alert is not null)
                {
                    alert(
                        "Special battle patch skipped",
                        "Could not apply the validated Gen7 Mist / Neblina owner-active v5.76.2 Battle.cro patch.");
                }
            }
            else
            {
                changed += result;
            }
        }
        return failed ? -1 : changed;
    }

    private static bool IsKingShieldMinusOneRequest(BattlePatchRequest request, int generation)
    {
        if (request.KingShieldAttackMinusOne)
            return true;

        foreach (string token in GetPatchTokens(request.BattlePatch))
        {
            string normalized = CustomBalanceTemplates.NormalizeToken(token);

            if (normalized is "kingshieldminusone" or "escudorealminusone")
                return true;

            if (generation == 6 && normalized == "gen6kingshieldminusone")
                return true;

            if (generation == 7 && normalized == "gen7kingshieldminusone")
                return true;
        }

        return false;
    }

    private static bool IsGen7WaterMudSportRequest(BattlePatchRequest request)
        => request.Move is 300 or 346 && request.Param0x0B.HasValue;

    private static bool IsGen7WishPivotRequest(BattlePatchRequest request)
        => request.Move == 273 &&
           HasPatchToken(request.BattlePatch, "Gen7WishPivot", "WishPivot", "DeseoPivot");

    private static bool IsGen7NightmareSleepV76Request(BattlePatchRequest request)
        => request.Move == 171 &&
           HasPatchToken(
               request.BattlePatch,
               "Gen7NightmareSleepV76",
               "NightmareSleepV76",
               "PesadillaSuenoV76");
    private static bool HasPatchToken(string battlePatch, params string[] names)
    {
        var wanted = new HashSet<string>(names.Select(CustomBalanceTemplates.NormalizeToken));
        return GetPatchTokens(battlePatch).Any(z => wanted.Contains(CustomBalanceTemplates.NormalizeToken(z)));
    }

    private static IEnumerable<string> GetPatchTokens(string battlePatch)
    {
        if (string.IsNullOrWhiteSpace(battlePatch))
            yield break;

        foreach (string token in battlePatch.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return token;
    }

    private static void DumpMagicCoatMoveData(int move, byte[] data, string battlePatch)
    {
        if (move != 277)
            return;

        try
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Magic Coat / Capa Magica Gen7 move-data dump");
            sb.AppendLine("Move: 277");
            sb.AppendLine("BattlePatch: " + (battlePatch ?? string.Empty));
            sb.AppendLine("DataLength: " + data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine();

            for (int i = 0; i < data.Length; i++)
            {
                sb.AppendLine("0x" + i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + " = " + data[i].ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + " / 0x" + data[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }

            sb.AppendLine();
            sb.AppendLine("Common fields:");
            if (data.Length > 0x09) sb.AppendLine("Accuracy 0x09: " + data[0x09].ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (data.Length > 0x0A) sb.AppendLine("Effect   0x0A: " + data[0x0A].ToString(System.Globalization.CultureInfo.InvariantCulture) + " / 0x" + data[0x0A].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            if (data.Length > 0x0B) sb.AppendLine("Param    0x0B: " + data[0x0B].ToString(System.Globalization.CultureInfo.InvariantCulture) + " / 0x" + data[0x0B].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            if (data.Length > 0x0C) sb.AppendLine("TurnMin  0x0C: " + data[0x0C].ToString(System.Globalization.CultureInfo.InvariantCulture) + " / 0x" + data[0x0C].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            if (data.Length > 0x0D) sb.AppendLine("TurnMax  0x0D: " + data[0x0D].ToString(System.Globalization.CultureInfo.InvariantCulture) + " / 0x" + data[0x0D].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));

            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "magic_coat_move277_dump.txt");
            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // Debug dump must never break move balancing.
        }
    }

    private static void SetTurnDuration(byte[] data, int minTurns, int maxTurns)
    {
        if (data.Length <= 0x0D)
            return;

        data[0x0C] = (byte)minTurns;
        data[0x0D] = (byte)maxTurns;
    }
    private static void SetKingShieldAttackDrop(byte[] data)
    {
        ClearMoveStatEffects(data);

        // Visual move-data side: Attack -1 at 100%.
        // The real in-battle behavior is hardcoded and patched separately.
        data[0x15] = 1; // Attack
        data[0x18] = unchecked((byte)-1);
        data[0x1B] = 100;
    }
    private static void ClearMoveStatEffects(byte[] data)
    {
        data[0x15] = 0;
        data[0x16] = 0;
        data[0x17] = 0;

        data[0x18] = 0;
        data[0x19] = 0;
        data[0x1A] = 0;

        data[0x1B] = 0;
        data[0x1C] = 0;
        data[0x1D] = 0;
    }

    private static int PatchGen6KingShieldMinusOne()
    {
        string path = FindRomFile("DLLBattle.cro");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return -1;

        byte[] data = File.ReadAllBytes(path);

        byte[] pattern =
        {
            0x01, 0x20, 0xA0, 0xE3,
            0xFE, 0x30, 0xA0, 0xE3,
        };

        byte[] alreadyPatched =
        {
            0x01, 0x20, 0xA0, 0xE3,
            0xFF, 0x30, 0xA0, 0xE3,
        };

        int changed = PatchPatternByte(data, pattern, 4, 0xFF);
        if (changed < 0)
            return FindBytes(data, alreadyPatched) >= 0 ? 0 : -1;

        BackupOnce(path, ".bak_kings_shield_minus_one");
        File.WriteAllBytes(path, data);
        return changed;
    }

    private static int PatchGen7KingShieldMinusOne()
    {
        string path = FindRomFile("Battle.cro");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return -1;

        byte[] data = File.ReadAllBytes(path);

        // Gen7 candidate 2, verified in-game:
        // Change mov r0, #0xFE (-2) to mov r0, #0xFF (-1).
        byte[] pattern =
        {
            0x01, 0x20, 0xA0, 0xE3,
            0xFE, 0x00, 0xA0, 0xE3,
            0x04, 0x20, 0xC4, 0xE5,
            0x11, 0x00, 0xC4, 0xE5,
            0x26, 0x00, 0x81, 0xE3,
            0x10, 0x20, 0xC4, 0xE5,
        };

        byte[] alreadyPatched =
        {
            0x01, 0x20, 0xA0, 0xE3,
            0xFF, 0x00, 0xA0, 0xE3,
            0x04, 0x20, 0xC4, 0xE5,
            0x11, 0x00, 0xC4, 0xE5,
            0x26, 0x00, 0x81, 0xE3,
            0x10, 0x20, 0xC4, 0xE5,
        };

        int changed = PatchPatternByte(data, pattern, 4, 0xFF);
        if (changed < 0)
            return FindBytes(data, alreadyPatched) >= 0 ? 0 : -1;

        BackupOnce(path, ".bak_kings_shield_minus_one");
        File.WriteAllBytes(path, data);
        return changed;
    }

    private static int PatchGen7WishPivot()
    {
        string path = FindRomFile("Battle.cro");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return -1;

        byte[] data = File.ReadAllBytes(path);

        // Confirmed on the user's USUM Battle.cro:
        // Wish's special-move factory normally exposes one event descriptor.
        // We redirect it to an unused two-entry descriptor pair:
        //   [0] event 0xBF -> original Wish handler 0xC6F88
        //   [1] event 0xA5 -> generic status-move pivot handler 0xC90E0
        // The second handler is the same generic pivot path used by Parting Shot
        // (and by U-turn / Volt Switch under their own event), so it switches the
        // user without Baton Pass stat-transfer behavior.
        const int WishFactoryOffset = 0x000C171C;
        const int NewDescriptorOffset = 0x00105CD0;
        const int NewDescriptor2Offset = 0x00105CD8;

        const uint WishCountVanilla = 0xE3A01001; // MOV r1,#1
        const uint WishCountPatched = 0xE3A01002; // MOV r1,#2
        const uint WishEvent = 0x000000BF;
        const uint StatusPivotEvent = 0x000000A5;

        const int WishDescriptorRelocIndex = 882;
        const int Slot0HandlerRelocIndex = 5913;
        const int Slot1HandlerRelocIndex = 5751;

        const uint WishRelocTag = 0x00C15AC0;
        const uint Slot0RelocTag = 0x0008CD41;
        const uint Slot1RelocTag = 0x0008CDC1;

        const uint WishDescriptorOriginalAddend = 0x00008B18; // -> 0x105B18
        const uint WishDescriptorNewAddend = 0x00008CD0;      // -> 0x105CD0

        const uint Slot0OriginalAddend = 0x000C869C; // -> 0xC881C
        const uint WishHandlerAddend = 0x000C6E08;  // -> 0xC6F88

        const uint Slot1OriginalAddend = 0x000C3B24; // -> 0xC3CA4
        const uint PivotHandlerAddend = 0x000C8F60; // -> 0xC90E0

        uint wishCount = ReadUInt32(data, WishFactoryOffset);
        if (wishCount != WishCountVanilla && wishCount != WishCountPatched)
            return -1;

        // Remaining Wish factory instructions must match the discovered stock factory.
        if (ReadUInt32(data, WishFactoryOffset + 4) != 0xE5801000 ||
            ReadUInt32(data, WishFactoryOffset + 8) != 0xE59F0000 ||
            ReadUInt32(data, WishFactoryOffset + 12) != 0xE12FFF1E)
        {
            return -1;
        }

        uint slot0Event = ReadUInt32(data, NewDescriptorOffset);
        uint slot1Event = ReadUInt32(data, NewDescriptor2Offset);

        if (slot0Event != WishEvent ||
            (slot1Event != WishEvent && slot1Event != StatusPivotEvent) ||
            ReadUInt32(data, NewDescriptorOffset + 4) != 0 ||
            ReadUInt32(data, NewDescriptor2Offset + 4) != 0)
        {
            return -1;
        }

        if (!TryGetInternalRelocationEntry(
                data,
                WishDescriptorRelocIndex,
                WishRelocTag,
                2,
                1,
                WishDescriptorOriginalAddend,
                WishDescriptorNewAddend,
                out int wishRelocEntry) ||
            !TryGetInternalRelocationEntry(
                data,
                Slot0HandlerRelocIndex,
                Slot0RelocTag,
                2,
                0,
                Slot0OriginalAddend,
                WishHandlerAddend,
                out int slot0RelocEntry) ||
            !TryGetInternalRelocationEntry(
                data,
                Slot1HandlerRelocIndex,
                Slot1RelocTag,
                2,
                0,
                Slot1OriginalAddend,
                PivotHandlerAddend,
                out int slot1RelocEntry))
        {
            return -1;
        }

        bool alreadyPatched =
            wishCount == WishCountPatched &&
            slot1Event == StatusPivotEvent &&
            ReadUInt32(data, wishRelocEntry + 8) == WishDescriptorNewAddend &&
            ReadUInt32(data, slot0RelocEntry + 8) == WishHandlerAddend &&
            ReadUInt32(data, slot1RelocEntry + 8) == PivotHandlerAddend;

        if (alreadyPatched)
            return 0;

        BackupOnce(path, ".bak_wish_pivot");

        int changed = 0;

        if (WriteUInt32IfDifferent(data, WishFactoryOffset, WishCountPatched))
            changed++;
        if (WriteUInt32IfDifferent(data, NewDescriptorOffset, WishEvent))
            changed++;
        if (WriteUInt32IfDifferent(data, NewDescriptor2Offset, StatusPivotEvent))
            changed++;

        if (WriteUInt32IfDifferent(data, wishRelocEntry + 8, WishDescriptorNewAddend))
            changed++;
        if (WriteUInt32IfDifferent(data, slot0RelocEntry + 8, WishHandlerAddend))
            changed++;
        if (WriteUInt32IfDifferent(data, slot1RelocEntry + 8, PivotHandlerAddend))
            changed++;

        File.WriteAllBytes(path, data);
        return changed;
    }

    private static bool TryGetInternalRelocationEntry(
        byte[] data,
        int index,
        uint expectedTag,
        byte expectedType,
        byte expectedRefSegment,
        uint originalAddend,
        uint patchedAddend,
        out int entryOffset)
    {
        entryOffset = -1;

        uint tableOffsetValue = ReadUInt32(data, 0x128);
        uint countValue = ReadUInt32(data, 0x12C);
        if (tableOffsetValue == uint.MaxValue || countValue == uint.MaxValue || index < 0 || (uint)index >= countValue)
            return false;

        long entry = tableOffsetValue + ((long)index * 12);
        if (entry < 0 || entry + 12 > data.Length)
            return false;

        entryOffset = (int)entry;

        if (ReadUInt32(data, entryOffset) != expectedTag ||
            data[entryOffset + 4] != expectedType ||
            data[entryOffset + 5] != expectedRefSegment)
        {
            return false;
        }

        uint addend = ReadUInt32(data, entryOffset + 8);
        return addend == originalAddend || addend == patchedAddend;
    }

    private static int PatchGen7WaterMudSportReduction(IEnumerable<BattlePatchRequest> requests)
    {
        string path = FindRomFile("Battle.cro");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return -1;

        byte[] data = File.ReadAllBytes(path);

        const int MudLiteralOffset = 0x000D3378;
        const int WaterLiteralOffset = 0x000D33A4;
        const int ZeroPreserveMovOffset = 0x000188C4;
        const int ZeroPreserveBlOffset = 0x000188C8;

        // Discarded diagnostics from development. If one of these known test
        // states is present, put it back to vanilla before saving Battle.cro.
        const int FinalClampOffset = 0x0007F9DC;
        const int IntermediateClamp1Offset = 0x00018AD4;
        const int IntermediateClamp2Offset = 0x00018B24;

        byte[] movVanilla = { 0x05, 0x00, 0xA0, 0xE1 }; // MOV  r0,r5
        byte[] movPatched = { 0x05, 0x00, 0xB0, 0xE1 }; // MOVS r0,r5
        byte[] blVanilla = { 0xF7, 0xA5, 0x01, 0xEB };  // BLAL 0x820AC
        byte[] blPatched = { 0xF7, 0xA5, 0x01, 0x1B };  // BLNE 0x820AC

        byte[] finalClampVanilla = { 0x01, 0x00, 0xA0, 0x03 }; // MOVEQ r0,#1
        byte[] finalClampTest = { 0x00, 0x00, 0xA0, 0x03 };    // discarded MOVEQ r0,#0
        byte[] intermediateClampVanilla = { 0x01, 0x50, 0xA0, 0x03 }; // MOVEQ r5,#1
        byte[] intermediateClampTest = { 0x00, 0x50, 0xA0, 0x03 };    // discarded MOVEQ r5,#0

        // Verify the exact USUM handlers discovered for Mud Sport and Water Sport.
        // Mud Sport: Electric type (12), arg 0x35 multiplier literal at 0xD3378.
        if (!BytesAt(data, 0x000D3350, 0x19, 0x00, 0xA0, 0xE3) ||
            !BytesAt(data, 0x000D335C, 0x0C, 0x00, 0x50, 0xE3) ||
            !BytesAt(data, 0x000D3364, 0x0C, 0x10, 0x9F, 0xE5) ||
            !BytesAt(data, 0x000D336C, 0x35, 0x00, 0xA0, 0xE3))
        {
            return -1;
        }

        // Water Sport: Fire type (9), arg 0x35 multiplier literal at 0xD33A4.
        if (!BytesAt(data, 0x000D337C, 0x19, 0x00, 0xA0, 0xE3) ||
            !BytesAt(data, 0x000D3388, 0x09, 0x00, 0x50, 0xE3) ||
            !BytesAt(data, 0x000D3390, 0x0C, 0x10, 0x9F, 0xE5) ||
            !BytesAt(data, 0x000D3398, 0x35, 0x00, 0xA0, 0xE3))
        {
            return -1;
        }

        bool engineVanilla =
            BytesAt(data, ZeroPreserveMovOffset, movVanilla) &&
            BytesAt(data, ZeroPreserveBlOffset, blVanilla);
        bool enginePatched =
            BytesAt(data, ZeroPreserveMovOffset, movPatched) &&
            BytesAt(data, ZeroPreserveBlOffset, blPatched);

        if (!engineVanilla && !enginePatched)
            return -1;

        if (!IsEitherKnownState(data, FinalClampOffset, finalClampVanilla, finalClampTest) ||
            !IsEitherKnownState(data, IntermediateClamp1Offset, intermediateClampVanilla, intermediateClampTest) ||
            !IsEitherKnownState(data, IntermediateClamp2Offset, intermediateClampVanilla, intermediateClampTest))
        {
            return -1;
        }

        var requested = requests
            .Where(IsGen7WaterMudSportRequest)
            .GroupBy(z => z.Move)
            .Select(z => z.Last())
            .ToArray();

        foreach (var request in requested)
        {
            int reductionPercent = request.Param0x0B.Value;
            if (reductionPercent is < 0 or > 100)
                return -1;
        }

        BackupOnce(path, ".bak_water_mud_sport");

        int changed = 0;

        foreach (var request in requested)
        {
            int reductionPercent = request.Param0x0B.Value;
            uint q12 = ReductionPercentToQ12(reductionPercent);
            int literalOffset = request.Move == 300 ? MudLiteralOffset : WaterLiteralOffset;

            if (WriteUInt32IfDifferent(data, literalOffset, q12))
                changed++;
        }

        // A 100% sport reduction produces Q12=0. The normal base-damage helper
        // contains the standard +2 term, so preserve a true zero by skipping that
        // helper only when the pre-formula value is zero. In-game this yields the
        // accepted stable 1 HP minimum after the engine's later final handling.
        bool needsZeroPreservation =
            ReadUInt32(data, MudLiteralOffset) == 0 ||
            ReadUInt32(data, WaterLiteralOffset) == 0;

        byte[] wantedMov = needsZeroPreservation ? movPatched : movVanilla;
        byte[] wantedBl = needsZeroPreservation ? blPatched : blVanilla;

        if (WriteBytesIfDifferent(data, ZeroPreserveMovOffset, wantedMov))
            changed++;
        if (WriteBytesIfDifferent(data, ZeroPreserveBlOffset, wantedBl))
            changed++;

        // Keep all disproven diagnostic experiments out of the final implementation.
        if (WriteBytesIfDifferent(data, FinalClampOffset, finalClampVanilla))
            changed++;
        if (WriteBytesIfDifferent(data, IntermediateClamp1Offset, intermediateClampVanilla))
            changed++;
        if (WriteBytesIfDifferent(data, IntermediateClamp2Offset, intermediateClampVanilla))
            changed++;

        File.WriteAllBytes(path, data);
        return changed;
    }

    private static uint ReductionPercentToQ12(int reductionPercent)
    {
        // Q12 0x1000 = 1.0x. Param0x0B is treated as percent reduction:
        //   67% reduction -> round(4096 * 33 / 100) = 0x0548 (vanilla)
        //  100% reduction -> 0x0000
        int remainingPercent = 100 - reductionPercent;
        return (uint)((0x1000 * remainingPercent + 50) / 100);
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        if (offset < 0 || offset + 4 > data.Length)
            return uint.MaxValue;

        return (uint)(
            data[offset] |
            (data[offset + 1] << 8) |
            (data[offset + 2] << 16) |
            (data[offset + 3] << 24));
    }

    private static bool WriteUInt32IfDifferent(byte[] data, int offset, uint value)
    {
        byte[] bytes =
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
        };

        return WriteBytesIfDifferent(data, offset, bytes);
    }

    private static bool WriteBytesIfDifferent(byte[] data, int offset, byte[] bytes)
    {
        if (offset < 0 || offset + bytes.Length > data.Length)
            return false;

        bool different = false;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (data[offset + i] == bytes[i])
                continue;

            different = true;
            break;
        }

        if (!different)
            return false;

        Array.Copy(bytes, 0, data, offset, bytes.Length);
        return true;
    }

    private static bool IsEitherKnownState(byte[] data, int offset, byte[] first, byte[] second)
        => BytesAt(data, offset, first) || BytesAt(data, offset, second);

    private static bool BytesAt(byte[] data, int offset, params byte[] expected)
    {
        if (offset < 0 || offset + expected.Length > data.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (data[offset + i] != expected[i])
                return false;
        }

        return true;
    }

    private static string FindRomFile(string fileName)
    {
        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(Main.RomFSPath))
            roots.Add(Main.RomFSPath);

        roots.Add(Environment.CurrentDirectory);
        roots.Add(AppDomain.CurrentDomain.BaseDirectory);

        foreach (string root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string[] directCandidates =
            [
                Path.Combine(root, fileName),
                Path.Combine(root, "ExtractedRomFS", fileName),
                Path.Combine(root, "RomFS", fileName),
                Path.Combine(root, "romfs", fileName),
            ];

            foreach (string candidate in directCandidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            try
            {
                string match = Directory.GetFiles(root, fileName, SearchOption.AllDirectories)
                    .OrderByDescending(z => z.Contains("ExtractedRomFS", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(match))
                    return match;
            }
            catch
            {
                // Some roots may not be fully searchable; try the next one.
            }
        }

        return string.Empty;
    }

    private static void BackupOnce(string path, string suffix)
    {
        string backup = path + suffix;
        if (!File.Exists(backup))
            File.Copy(path, backup, false);
    }

    private static int PatchPatternByte(byte[] data, byte[] pattern, int byteOffset, byte replacement)
    {
        int changed = 0;

        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;

            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] == pattern[j])
                    continue;

                match = false;
                break;
            }

            if (!match)
                continue;

            data[i + byteOffset] = replacement;
            changed++;
        }

        return changed == 0 ? -1 : changed;
    }

    private static int FindBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;

            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] == pattern[j])
                    continue;

                match = false;
                break;
            }

            if (match)
                return i;
        }

        return -1;
    }

    private static void DumpSpecificMoveDataForBalanceDebug(int generation, int move, byte[] data, string battlePatch)
    {
        if (generation != 7 || data == null)
            return;

        if (move != 300 && move != 346)
            return;

        string moveName = move == 300 ? "Mud Sport / Chapoteo Lodo" : "Water Sport / Hidrochorro";
        string safeName = move == 300 ? "mud_sport_300" : "water_sport_346";
        string path = System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            safeName + "_gen7_balance_dump.txt");

        using (var sw = new System.IO.StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
        {
            sw.WriteLine(moveName + " Gen7 move-data dump");
            sw.WriteLine("Move: " + move);
            sw.WriteLine("Generation: " + generation);
            sw.WriteLine("BattlePatch: " + (battlePatch ?? ""));
            sw.WriteLine("DataLength: " + data.Length);
            sw.WriteLine("Dump moment: after CSV/template values have been applied to the in-memory move data.");
            sw.WriteLine();

            for (int i = 0; i < data.Length; i++)
                sw.WriteLine("0x" + i.ToString("X2") + " = " + data[i].ToString("000") + " / 0x" + data[i].ToString("X2"));

            sw.WriteLine();
            sw.WriteLine("Likely Gen7 fields to inspect:");
            WriteByteIfPresent(sw, data, 0x00, "Type?");
            WriteByteIfPresent(sw, data, 0x01, "Category?");
            WriteByteIfPresent(sw, data, 0x04, "Accuracy");
            WriteByteIfPresent(sw, data, 0x05, "PP");
            WriteByteIfPresent(sw, data, 0x08, "Priority?");
            WriteByteIfPresent(sw, data, 0x0B, "Param0x0B from CSV");
            WriteU16IfPresent(sw, data, 0x10, "Effect? u16 little-endian");
            WriteByteIfPresent(sw, data, 0x12, "Possible effect parameter");
            WriteByteIfPresent(sw, data, 0x13, "Possible effect chance/parameter");
            WriteByteIfPresent(sw, data, 0x14, "Targeting?");
            WriteByteIfPresent(sw, data, 0x15, "Possible stat/effect parameter");
            WriteByteIfPresent(sw, data, 0x16, "Possible stat/effect parameter");
            WriteByteIfPresent(sw, data, 0x17, "Possible stat/effect parameter");

            sw.WriteLine();
            sw.WriteLine("What to compare:");
            sw.WriteLine("- Run once with the current template line.");
            sw.WriteLine("- Then temporarily change only Param0x0B between 50, 67 and 100, apply Balance Moves again, and compare this dump.");
            sw.WriteLine("- The byte that changes with Param0x0B is the writer location; the real effect may read a different byte.");
        }
    }

    private static void WriteByteIfPresent(System.IO.StreamWriter sw, byte[] data, int offset, string label)
    {
        if (offset >= 0 && offset < data.Length)
            sw.WriteLine(label + " 0x" + offset.ToString("X2") + ": " + data[offset] + " / 0x" + data[offset].ToString("X2"));
    }

    private static void WriteU16IfPresent(System.IO.StreamWriter sw, byte[] data, int offset, string label)
    {
        if (offset >= 0 && offset + 1 < data.Length)
        {
            int value = data[offset] | (data[offset + 1] << 8);
            sw.WriteLine(label + " 0x" + offset.ToString("X2") + "-0x" + (offset + 1).ToString("X2") + ": " + value + " / 0x" + value.ToString("X4"));
        }
    }
}
