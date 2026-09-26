using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using pk3DS.Core.Modding.Research;

namespace pk3DS.WinForms;

/// <summary>
/// Gen7 Brutal Swing / Giro Vil self-confusion v1.
///
/// Audited against the user's USUM Battle.cro (stock move master table):
/// - move 693 is not present in the special-move registry;
/// - timing 0x04 is the post-move cleanup phase used by vanilla move mechanics;
/// - vanilla rampage moves build Confusion as status id 6 through 0x60C6C;
/// - the generic Confusion path allocates command type 11, builds status id 6,
///   then queues it through 0x7CCF4.
///
/// The injected handler registers exactly one timing-0x04 callback. The callback
/// validates the current actor/move event, then queues Confusion for the move user.
/// Normal move data (30 BP, 2-5 hits, AllFoes) stays in the balance template.
/// </summary>
internal static class Gen7BrutalSwingSelfConfusionPatcher
{
    private const int BrutalSwing = 693;
    private const byte PostMoveTiming = 0x04;

    private const int MoveTableInboundRelocation = 755;
    private const int MoveCountOffset = 0x000873EC;
    private const int StockMoveCount = 343;
    private const uint StockMoveTable = 0x00105DF0;

    private const int SelfConfusionCodeSize = 0x6C;

    // Stock USUM Battle.cro helpers proven by the generic Confusion path.
    private const uint EventGuard = 0x00060740;
    private const uint BuildStatusPayload = 0x00060C6C;
    private const uint GenericConfusionPath = 0x00060F28;
    private const uint QueueCommand = 0x0007CCF4;
    private const uint AllocateCommand = 0x000876F8;
    private const uint GetBattler = 0x00092298;

    private static readonly byte[] EventGuardSignature = Hex(
        "70 40 2D E9 00 50 A0 E1 01 00 A0 E1 02 40 A0 E1 " +
        "00 9D 00 EB 04 00 50 E1 07 00 00 1A 12 00 A0 E3");

    private static readonly byte[] StatusBuilderSignature = Hex(
        "70 40 2D E9 06 00 50 E3 08 D0 4D E2 00 50 A0 E1 " +
        "02 40 A0 E1 01 60 A0 E1 09 00 00 2A");

    private static readonly byte[] GenericConfusionSignature = Hex(
        "05 20 A0 E1 0B 10 A0 E3 0A 00 A0 E1 EF 99 00 EB " +
        "06 30 A0 E3 14 50 C0 E5 00 40 A0 E1 08 20 80 E2 " +
        "B4 30 C0 E1 0B 10 A0 E1 03 00 A0 E1 44 FF FF EB " +
        "04 10 A0 E1 0A 00 A0 E1 63 6F 00 EB");

    private enum InstallState
    {
        Ready,
        Installed,
    }

    internal static bool IsRequested(CustomBattleEffectPatcher.BattlePatchRequest request)
    {
        if (request is null || request.Move != BrutalSwing)
            return false;

        if (string.IsNullOrWhiteSpace(request.BattlePatch))
            return false;

        return request.BattlePatch
            .Split(
                [',', ';', '|'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(CustomBalanceTemplates.NormalizeToken)
            .Any(z =>
                z is "gen7brutalswingselfconfusionv1" or
                    "gen7brutalswingselfconfusion" or
                    "brutalswingselfconfusion" or
                    "girovilautoconfusion");
    }

    internal static int Apply()
    {
        string path = FindBattleCro();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return -1;

        byte[] data = File.ReadAllBytes(path);

        if (!TryClassify(data, out InstallState state))
            return -1;

        if (state == InstallState.Installed)
            return 0;

        var mechanic =
            new CroMechanicRequest
            {
                Name = "Brutal Swing post-move self-confusion",
                Effects =
                [
                    new CroMechanicEffectSpec
                    {
                        Timing = PostMoveTiming,
                        Name = "post-move self-confusion",
                        Code = BuildPlaceholderCode(),
                    },
                ],
            };

        if (!CroMoveMechanicInstaller.TryInstall(
                data,
                BrutalSwing,
                mechanic,
                out byte[] updated,
                out CroMoveMechanicInstallReport report,
                out _))
        {
            return -1;
        }

        if (report.Effects.Count != 1 ||
            report.Effects[0].Timing != PostMoveTiming ||
            report.Effects[0].CodeLength != SelfConfusionCodeSize)
        {
            return -1;
        }

        if (!TryGetBrutalSwingFunction(
                updated,
                requireExactCode: false,
                out uint functionOffset) ||
            functionOffset != report.Effects[0].FunctionOffset)
        {
            return -1;
        }

        byte[] code = BuildSelfConfusionCode(functionOffset);
        if (code.Length != SelfConfusionCodeSize ||
            !RangeFits(updated.Length, functionOffset, (uint)code.Length))
        {
            return -1;
        }

        code.CopyTo(updated, checked((int)functionOffset));

        if (!TryUpdateHashes(updated) ||
            !TryClassify(updated, out InstallState finalState) ||
            finalState != InstallState.Installed)
        {
            return -1;
        }

        BackupOnce(path, ".bak_brutal_swing_self_confusion_v1");
        File.WriteAllBytes(path, updated);
        return 1;
    }

    private static bool TryClassify(
        byte[] data,
        out InstallState state)
    {
        state = InstallState.Ready;

        if (data is null ||
            !Match(data, (int)EventGuard, EventGuardSignature) ||
            !Match(data, (int)BuildStatusPayload, StatusBuilderSignature) ||
            !Match(data, (int)GenericConfusionPath, GenericConfusionSignature) ||
            !CroRelocationMap.TryCreate(data, out CroRelocationMap map, out _))
        {
            return false;
        }

        if (!TryGetMoveTable(
                data,
                map,
                out uint table,
                out int count))
        {
            return false;
        }

        int matches = 0;
        uint brutalSwingEntry = 0;

        for (int i = 0; i < count; i++)
        {
            uint entry =
                checked(
                    table +
                    (uint)(i * 8));

            if (ReadUInt32(data, checked((int)entry)) != BrutalSwing)
                continue;

            matches++;
            brutalSwingEntry = entry;
        }

        if (matches == 0)
        {
            if (count != StockMoveCount ||
                table != StockMoveTable)
            {
                return false;
            }

            state = InstallState.Ready;
            return true;
        }

        if (matches != 1 ||
            !TryGetBrutalSwingFunction(
                data,
                map,
                brutalSwingEntry,
                requireExactCode: true,
                out _))
        {
            return false;
        }

        state = InstallState.Installed;
        return true;
    }

    private static bool TryGetBrutalSwingFunction(
        byte[] data,
        bool requireExactCode,
        out uint functionOffset)
    {
        functionOffset = 0;

        if (!CroRelocationMap.TryCreate(
                data,
                out CroRelocationMap map,
                out _))
        {
            return false;
        }

        if (!TryGetMoveTable(
                data,
                map,
                out uint table,
                out int count))
        {
            return false;
        }

        int matches = 0;
        uint entry = 0;

        for (int i = 0; i < count; i++)
        {
            uint candidate =
                checked(
                    table +
                    (uint)(i * 8));

            if (ReadUInt32(
                    data,
                    checked((int)candidate)) != BrutalSwing)
            {
                continue;
            }

            matches++;
            entry = candidate;
        }

        if (matches != 1)
            return false;

        return TryGetBrutalSwingFunction(
            data,
            map,
            entry,
            requireExactCode,
            out functionOffset);
    }

    private static bool TryGetBrutalSwingFunction(
        byte[] data,
        CroRelocationMap map,
        uint entry,
        bool requireExactCode,
        out uint functionOffset)
    {
        functionOffset = 0;

        if (!TryResolveUniquePointer(
                map,
                checked(entry + 4u),
                out uint handler) ||
            !RangeFits(data.Length, handler, 20))
        {
            return false;
        }

        if (ReadUInt32(data, checked((int)handler)) != 0xE3A01001u ||
            ReadUInt32(data, checked((int)handler + 4)) != 0xE5801000u ||
            ReadUInt32(data, checked((int)handler + 8)) != 0xE59F0000u ||
            ReadUInt32(data, checked((int)handler + 12)) != 0xE12FFF1Eu)
        {
            return false;
        }

        if (!TryResolveUniquePointer(
                map,
                checked(handler + 16u),
                out uint timingTable) ||
            !RangeFits(data.Length, timingTable, 8))
        {
            return false;
        }

        int timing = checked((int)timingTable);
        if (data[timing] != PostMoveTiming ||
            data[timing + 1] != 0 ||
            data[timing + 2] != 0 ||
            data[timing + 3] != 0)
        {
            return false;
        }

        if (!TryResolveUniquePointer(
                map,
                checked(timingTable + 4u),
                out functionOffset) ||
            !RangeFits(
                data.Length,
                functionOffset,
                SelfConfusionCodeSize))
        {
            return false;
        }

        if (!requireExactCode)
            return Match(
                data,
                checked((int)functionOffset),
                BuildPlaceholderCode());

        byte[] expected =
            BuildSelfConfusionCode(
                functionOffset);

        return expected.Length == SelfConfusionCodeSize &&
               Match(
                   data,
                   checked((int)functionOffset),
                   expected);
    }

    private static bool TryGetMoveTable(
        byte[] data,
        CroRelocationMap map,
        out uint table,
        out int count)
    {
        table = 0;
        count = 0;

        if (map.References.Count <= MoveTableInboundRelocation ||
            MoveCountOffset < 0 ||
            MoveCountOffset + 4 > data.Length)
        {
            return false;
        }

        CroRelocationReference inbound =
            map.References[
                MoveTableInboundRelocation];

        if (!inbound.TargetFileBacked)
            return false;

        uint rawCount =
            ReadUInt32(
                data,
                MoveCountOffset);

        if (rawCount == uint.MaxValue ||
            rawCount < StockMoveCount ||
            rawCount > 1024)
        {
            return false;
        }

        table = inbound.TargetAddress;
        count = checked((int)rawCount);

        ulong tableEnd =
            (ulong)table +
            ((ulong)count * 8u);

        return tableEnd <= (ulong)data.Length;
    }

    private static bool TryResolveUniquePointer(
        CroRelocationMap map,
        uint writeAddress,
        out uint targetAddress)
    {
        targetAddress = 0;
        bool found = false;

        foreach (CroRelocationReference reference in map.References)
        {
            if (!reference.WriteFileBacked ||
                reference.WriteAddress != writeAddress)
            {
                continue;
            }

            if (found || !reference.TargetFileBacked)
                return false;

            targetAddress = reference.TargetAddress;
            found = true;
        }

        return found;
    }

    private static byte[] BuildPlaceholderCode()
    {
        byte[] code =
            new byte[SelfConfusionCodeSize];

        for (int i = 0; i < code.Length; i += 4)
        {
            BitConverter
                .GetBytes(0xE320F000u) // NOP
                .CopyTo(code, i);
        }

        return code;
    }

    private static byte[] BuildSelfConfusionCode(
        uint functionStart)
    {
        if ((functionStart & 3u) != 0 ||
            !TryEncodeBranchLink(
                checked(functionStart + 0x10u),
                EventGuard,
                out uint guardCall) ||
            !TryEncodeBranchLink(
                checked(functionStart + 0x24u),
                GetBattler,
                out uint battlerCall) ||
            !TryEncodeBranchLink(
                checked(functionStart + 0x38u),
                AllocateCommand,
                out uint allocateCall) ||
            !TryEncodeBranchLink(
                checked(functionStart + 0x58u),
                BuildStatusPayload,
                out uint statusCall) ||
            !TryEncodeBranchLink(
                checked(functionStart + 0x64u),
                QueueCommand,
                out uint queueCall))
        {
            return [];
        }

        uint[] words =
        [
            0xE92D41F0u, // PUSH {r4,r5,r6,r7,r8,lr}
            0xE1A04001u, // MOV  r4,r1       ; battle context
            0xE1A05002u, // MOV  r5,r2       ; move user
            0xE3A01002u, // MOV  r1,#2       ; actor event var
            guardCall,   // BL   EventGuard
            0xE3500000u, // CMP  r0,#0
            0x0A000012u, // BEQ  done
            0xE1A00004u, // MOV  r0,r4
            0xE1A01005u, // MOV  r1,r5
            battlerCall, // BL   GetBattler
            0xE1A06000u, // MOV  r6,r0
            0xE1A02005u, // MOV  r2,r5
            0xE3A0100Bu, // MOV  r1,#11      ; status command
            0xE1A00004u, // MOV  r0,r4
            allocateCall,// BL   AllocateCommand
            0xE1A07000u, // MOV  r7,r0
            0xE3A03006u, // MOV  r3,#6       ; Confusion
            0xE5C75014u, // STRB r5,[r7,#0x14]
            0xE2872008u, // ADD  r2,r7,#8
            0xE1C730B4u, // STRH r3,[r7,#4]
            0xE1A01006u, // MOV  r1,r6
            0xE1A00003u, // MOV  r0,r3
            statusCall,  // BL   BuildStatusPayload
            0xE1A01007u, // MOV  r1,r7
            0xE1A00004u, // MOV  r0,r4
            queueCall,   // BL   QueueCommand
            0xE8BD81F0u, // POP  {r4,r5,r6,r7,r8,pc}
        ];

        byte[] code =
            new byte[words.Length * 4];

        for (int i = 0; i < words.Length; i++)
        {
            BitConverter
                .GetBytes(words[i])
                .CopyTo(
                    code,
                    i * 4);
        }

        return code;
    }

    private static bool TryEncodeBranchLink(
        uint instructionAddress,
        uint targetAddress,
        out uint instruction)
    {
        instruction = 0;

        long delta =
            (long)targetAddress -
            ((long)instructionAddress + 8L);

        if ((delta & 3L) != 0)
            return false;

        long words =
            delta >> 2;

        if (words < -0x800000L ||
            words > 0x7FFFFFL)
        {
            return false;
        }

        instruction =
            0xEB000000u |
            ((uint)words & 0x00FFFFFFu);

        return true;
    }

    private static bool TryUpdateHashes(
        byte[] data)
    {
        if (data is null ||
            data.Length < 0xC0)
        {
            return false;
        }

        uint codeStart = ReadUInt32(data, 0xB0);
        uint codeSize = ReadUInt32(data, 0xB4);
        uint dataStart = ReadUInt32(data, 0xB8);
        uint dataSize = ReadUInt32(data, 0xBC);

        if (codeStart < 0x80 ||
            !RangeFits(data.Length, codeStart, codeSize) ||
            !RangeFits(data.Length, dataStart, dataSize))
        {
            return false;
        }

        ulong codeEnd64 =
            (ulong)codeStart +
            codeSize;

        if (codeEnd64 > uint.MaxValue ||
            dataStart < codeEnd64)
        {
            return false;
        }

        uint codeEnd =
            (uint)codeEnd64;

        uint headerSize =
            codeStart - 0x80u;

        uint middleSize =
            dataStart - codeEnd;

        byte[] headerHash =
            SHA256.HashData(
                data.AsSpan(
                    0x80,
                    checked((int)headerSize)));

        byte[] codeHash =
            SHA256.HashData(
                data.AsSpan(
                    checked((int)codeStart),
                    checked((int)codeSize)));

        byte[] middleHash =
            SHA256.HashData(
                data.AsSpan(
                    checked((int)codeEnd),
                    checked((int)middleSize)));

        byte[] dataHash =
            SHA256.HashData(
                data.AsSpan(
                    checked((int)dataStart),
                    checked((int)dataSize)));

        headerHash.CopyTo(data, 0x00);
        codeHash.CopyTo(data, 0x20);
        middleHash.CopyTo(data, 0x40);
        dataHash.CopyTo(data, 0x60);

        return true;
    }

    private static bool RangeFits(
        int fileLength,
        uint start,
        uint length) =>
        start <= (uint)fileLength &&
        length <=
        (uint)fileLength - start;

    private static uint ReadUInt32(
        byte[] data,
        int offset)
    {
        if (data is null ||
            offset < 0 ||
            offset + 4 > data.Length)
        {
            return uint.MaxValue;
        }

        return BitConverter.ToUInt32(
            data,
            offset);
    }

    private static bool Match(
        byte[] data,
        int offset,
        byte[] expected)
    {
        if (data is null ||
            expected is null ||
            offset < 0 ||
            offset + expected.Length > data.Length)
        {
            return false;
        }

        return data
            .AsSpan(
                offset,
                expected.Length)
            .SequenceEqual(
                expected);
    }

    private static byte[] Hex(
        string value) =>
        value
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(
                z => Convert.ToByte(
                    z,
                    16))
            .ToArray();

    private static string FindBattleCro()
    {
        if (string.IsNullOrWhiteSpace(
                Main.RomFSPath))
        {
            return string.Empty;
        }

        string direct =
            Path.Combine(
                Main.RomFSPath,
                "Battle.cro");

        if (File.Exists(direct))
            return direct;

        string extracted =
            Path.Combine(
                Main.RomFSPath,
                "ExtractedRomFS",
                "Battle.cro");

        return File.Exists(extracted)
            ? extracted
            : string.Empty;
    }

    private static void BackupOnce(
        string path,
        string suffix)
    {
        string backup =
            path + suffix;

        if (!File.Exists(backup))
        {
            File.Copy(
                path,
                backup,
                false);
        }
    }
}