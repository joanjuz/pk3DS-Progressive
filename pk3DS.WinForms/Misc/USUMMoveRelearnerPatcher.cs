using System;
using System.IO;
using System.Linq;
using pk3DS.Core;

namespace pk3DS.WinForms;

/// <summary>
/// Applies the supplied CAFE.asm + Learn.asm behavior directly to a decompressed
/// USUM code.bin. The patch is intentionally limited to the known USUM offsets.
/// </summary>
internal static class USUMMoveRelearnerPatcher
{
    internal const string ActionId = "usum.move-relearner";

    private const int CodeBaseAddress = 0x100000;

    private const int CafeHookAddress = 0x39A044;
    private const int CafePayloadAddress = 0x5B9FA0;
    private const int LearnInitHookAddress = 0x441654;
    private const int LearnCheckHookAddress = 0x4417D8;
    private const int LearnPayloadAddress = 0x5B9D04;

    // Original instructions replaced by the supplied ASM hooks.
    private static readonly byte[] CafeHookOriginal = [0xF0, 0x4F, 0x2D, 0xE9];
    private static readonly byte[] LearnInitHookOriginal = [0xF0, 0x43, 0x2D, 0xE9];
    private static readonly byte[] LearnCheckHookOriginal = [0x02, 0x00, 0x50, 0xE3];

    // b 0x5B9FA0
    private static readonly byte[] CafeHookPatched = [0xD5, 0x7F, 0x08, 0xEA];

    // b 0x5B9D04
    private static readonly byte[] LearnInitHookPatched = [0xAA, 0xE1, 0x05, 0xEA];

    // b 0x5B9D14
    private static readonly byte[] LearnCheckHookPatched = [0x4D, 0xE1, 0x05, 0xEA];

    // CAFE.asm detour @ 0x5B9FA0.
    // Café script IDs 0x14AA..0x14B7 are redirected to relearner script 0x157C.
    private static readonly byte[] CafePayload =
    [
        0x04, 0x00, 0x2D, 0xE9, 0x24, 0x20, 0x9F, 0xE5, 0x02, 0x00, 0x51, 0xE1,
        0x04, 0x00, 0x00, 0xBA, 0x1C, 0x20, 0x9F, 0xE5, 0x02, 0x00, 0x51, 0xE1,
        0x01, 0x00, 0x00, 0xCA, 0xFF, 0xFF, 0xFF, 0xEA, 0x10, 0x10, 0x9F, 0xE5,
        0x04, 0x00, 0xBD, 0xE8, 0xF0, 0x4F, 0x2D, 0xE9, 0x1D, 0x80, 0xF7, 0xEA,
        0xAA, 0x14, 0x00, 0x00, 0xB7, 0x14, 0x00, 0x00, 0x7C, 0x15, 0x00, 0x00,
    ];

    // Learn.asm code starts at 0x5B9D04. Address 0x5B9D00 is deliberately
    // left untouched because the ASM uses it as runtime storage (poke_ptr).
    private static readonly byte[] LearnPayload =
    [
        0xF0, 0x43, 0x2D, 0xE9, 0x38, 0x40, 0x9F, 0xE5, 0x00, 0x10, 0x84, 0xE5,
        0x50, 0x1E, 0xFA, 0xEA, 0x1F, 0x40, 0x2D, 0xE9, 0x00, 0x40, 0xA0, 0xE1,
        0x24, 0x00, 0x9F, 0xE5, 0x00, 0x00, 0x90, 0xE5, 0xE5, 0xA6, 0xF5, 0xEB,
        0x04, 0x00, 0x50, 0xE1, 0x00, 0x00, 0x00, 0xBA, 0x01, 0x00, 0x00, 0xEA,
        0x1F, 0x40, 0xBD, 0xE8, 0xE1, 0x1E, 0xFA, 0xEA, 0x1F, 0x40, 0xBD, 0xE8,
        0x02, 0x00, 0x50, 0xE3, 0xA4, 0x1E, 0xFA, 0xEA, 0x00, 0x9D, 0x5B, 0x00,
    ];
    // Ultra Moon uses the same café hook/payload as Ultra Sun, but the
    // Learn.asm hooks are shifted forward by one ARM instruction (+4 bytes).
    //
    // IMPORTANT:
    // Do NOT use a clean-to-working whole-code.bin delta here. The historical
    // reference pair also contained unrelated pk3DS changes (for example the
    // Accept Any Pokémon trade hook at file offset 0x2843F4 and its wrapper at
    // 0x4B9C00). Move Relearner must own only its three hooks and two code caves.
    private const int MoonLearnInitHookAddress = 0x441658;
    private const int MoonLearnCheckHookAddress = 0x4417DC;

    // b 0x5B9D04 from the Moon init hook (+4 versus Ultra Sun).
    private static readonly byte[] MoonLearnInitHookPatched =
    [
        0xA9, 0xE1, 0x05, 0xEA,
    ];

    // b 0x5B9D14 from the Moon check hook (+4 versus Ultra Sun).
    private static readonly byte[] MoonLearnCheckHookPatched =
    [
        0x4C, 0xE1, 0x05, 0xEA,
    ];

    private static readonly byte[] MoonLearnPayload = CreateMoonLearnPayload();

    private static byte[] CreateMoonLearnPayload()
    {
        // The code is identical to Ultra Sun except for the three return
        // branches whose source PCs moved forward by one ARM instruction.
        byte[] payload = (byte[])LearnPayload.Clone();
        payload[12] = 0x51; // 0xEAFA1E50 -> 0xEAFA1E51
        payload[52] = 0xE2; // 0xEAFA1EE1 -> 0xEAFA1EE2
        payload[64] = 0xA5; // 0xEAFA1EA4 -> 0xEAFA1EA5
        return payload;
    }
    // Stable Ultra Moon instructions immediately before the shifted Learn hooks.
    // These bytes are not changed by the patch and distinguish Luna from Sol.
    private static readonly byte[] MoonLayoutMarker1 = Hex("F081BDE8");
    private static readonly byte[] MoonLayoutMarker2 = Hex("1BA701EB");

    // Lillie's first Pokémon Center tutorial is StoryText file 77.
    // Lines 5..7 are the vanilla café/drink explanation. The café itself is
    // still introduced by line 4; only the obsolete service explanation changes.
    private const int LillieTutorialFile = 77;
    private const int LillieTutorialFirstLine = 5;

    private static readonly string[][] LillieTutorialOriginal =
    [
        [
            "わたしは　カフェスペースで　のめる\\nモーモーミルクが　すきだったりします[VAR 0114(0006)]",
            "ガイドブックに　かかれていました\\c\\nポケモンセンターごとに\\nメニューは　ことなるそうです\\r\\nなにか　こだわりが　あるのでしょうね[VAR 0114(0006)]",
            "のみもの　だけでなく\\nおかしや　しまめぐりに　やくだつ\\r\\nアドバイスも　いただけるそうです[VAR 0114(0006)]",
        ],
        [
            "わたしは　カフェスペースで　飲める\\nモーモーミルクが　好きだったりします[VAR 0114(0006)]",
            "ガイドブックに　書かれていました\\c\\nポケモンセンターごとに\\nメニューは　異なるそうです\\r\\nなにか　こだわりが　あるのでしょうね[VAR 0114(0006)]",
            "飲みもの　だけでなく\\nお茶菓子や　島巡りに　役立つ\\r\\nアドバイスも　いただけるそうです[VAR 0114(0006)]",
        ],
        [
            "I like to relax there sometimes with a frosty\\nglass of Moomoo Milk.[VAR 0114(0006)]",
            "I read something in a travel guide about\\nAlola once...\\c\\nApparently each Pokémon Center in Alola offers\\na different selection of drinks.\\r\\nI wonder how they pick what to serve?[VAR 0114(0006)]",
            "The cafés may also offer more than just drinks.\\c\\nI’ve heard they also sell special treats and that\\nsometimes the staff have tips for trial-goers.[VAR 0114(0006)]",
        ],
        [
            "Moi, j’adore venir ici pour boire du Lait Meumeu.[VAR 0114(0006)]",
            "Mais d’après ce que j’ai lu dans des guides\\ntouristiques...\\c\\nChaque Centre Pokémon propose une carte\\ndifférente.\\c\\nChacun a sa spécialité, en quelque sorte.[VAR 0114(0006)]",
            "De plus, on te propose non seulement\\ndes boissons et des friandises, mais aussi\\r\\ndes conseils fort utiles pour ton Tour des Îles.[VAR 0114(0006)]",
        ],
        [
            "Il loro Latte Mumu è la fine del mondo![VAR 0114(0006)]",
            "Ho letto in una guida che il menu cambia\\na seconda del Centro Pokémon.\\c\\nChissà in base a cosa decidono le bevande...[VAR 0114(0006)]",
            "Non offrono solo bibite: danno anche dolcetti\\ne consigli utili a chi fa il giro delle isole![VAR 0114(0006)]",
        ],
        [
            "Hier kann man sich meiner Meinung nach schön\\nbei einem Glas frischer Kuhmuh-Milch erholen.[VAR 0114(0006)]",
            "Übrigens unterscheidet sich das Angebot im\\nCafé-Bereich von Pokémon-Center zu\\r\\nPokémon-Center.\\c\\nZ-Zumindest steht das so im Reiseführer...[VAR 0114(0006)]",
            "Du findest dort nicht nur Getränke, sondern\\nauch Süßes. Ich habe gehört, dass die\\r\\nAngestellten auch gute Ratschläge für Trainer\\r\\nauf Inselwanderschaft auf Lager hätten.[VAR 0114(0006)]",
        ],
        [
            "A veces me gusta pasarme por ahí y tomarme\\nuna Leche Mu-mu para relajarme.[VAR 0114(0006)]",
            "En una guía de viajes leí que las bebidas varían\\nde un Centro Pokémon a otro, así que vale la\\r\\npena pasarse siempre para ver qué tienen.[VAR 0114(0006)]",
            "Puede que tengan algún dulce para acompañar\\ntu bebida e incluso algún que otro consejo para\\r\\ntu recorrido insular.[VAR 0114(0006)]",
        ],
        [
            "저는 카페스페이스에서 마실 수 있는\\n튼튼밀크를 좋아해요[VAR 0114(0006)]",
            "가이드북에 적혀 있었어요\\c\\n포켓몬센터마다\\n메뉴가 다르다고 해요\\r\\n각각 특색이 있는 거겠죠[VAR 0114(0006)]",
            "음료수뿐만 아니라\\n과자를 받거나 섬 순례에 도움이\\r\\n되는 어드바이스도 들을 수 있대요[VAR 0114(0006)]",
        ],
        [
            "我很喜欢在咖啡区里\\n可以喝到的哞哞鲜奶。[VAR 0114(0006)]",
            "按照指南手册上的说法，\\c\\n每个宝可梦中心提供的\\n菜单好像都不一样。\\r\\n应该是有着各自的想法吧。[VAR 0114(0006)]",
            "听说在那里不但有饮料可以喝，\\n还有茶点可以吃，而且还能得到\\r\\n一些有助于诸岛巡礼的建议。[VAR 0114(0006)]",
        ],
        [
            "我很喜歡在咖啡區裡\\n可以喝到的哞哞鮮奶。[VAR 0114(0006)]",
            "按照導覽手冊的說法，\\c\\n每間寶可夢中心的咖啡區\\n提供的飲料種類好像都不一樣。\\r\\n大概每間店有自己講究的地方吧。[VAR 0114(0006)]",
            "另外不只是飲料，\\n好像也會提供茶點\\r\\n和一些對諸島巡禮有幫助的建議。[VAR 0114(0006)]",
        ],
    ];

    private static readonly string[][] LillieTutorialPatched =
    [
        [
            "カフェの　てんいんさんは\\nポケモンが　いまのレベルまでに\\r\\nおぼえられる　わざを\\nおもいださせてくれます[VAR 0114(0006)]",
            "わすれてしまった　わざが　あれば\\nてんいんさんに　はなしかけてみてください[VAR 0114(0006)]",
            "このサービスは　アローラじゅうの\\nポケモンセンターの　カフェで\\r\\nりようできます[VAR 0114(0006)]",
        ],
        [
            "カフェの 店員さんは\\nポケモンが 今のレベルまでに\\r\\n覚えられる 技を\\n思い出させてくれます[VAR 0114(0006)]",
            "忘れてしまった 技が あれば\\n店員さんに 話しかけてみてください[VAR 0114(0006)]",
            "このサービスは アローラ中の\\nポケモンセンターの カフェで\\r\\n利用できます[VAR 0114(0006)]",
        ],
        [
            "The café staff can help your Pokémon remember\\nmoves they could have learned by their current\\r\\nlevel.[VAR 0114(0006)]",
            "If one of your Pokémon has forgotten a useful\\nmove, just ask the staff for help.[VAR 0114(0006)]",
            "You can use this service at Pokémon Center\\ncafés all across Alola.[VAR 0114(0006)]",
        ],
        [
            "Le personnel du café peut aider tes Pokémon à\\nse rappeler des capacités qu’ils auraient pu\\r\\napprendre à leur niveau actuel.[VAR 0114(0006)]",
            "Si l’un de tes Pokémon a oublié une capacité\\nutile, demande simplement de l’aide au personnel.[VAR 0114(0006)]",
            "Ce service est disponible dans les cafés des\\nCentres Pokémon de toute la région d’Alola.[VAR 0114(0006)]",
        ],
        [
            "Il personale della caffetteria può aiutare i tuoi\\nPokémon a ricordare mosse che avrebbero potuto\\r\\nimparare al loro livello attuale.[VAR 0114(0006)]",
            "Se un Pokémon ha dimenticato una mossa utile,\\nchiedi aiuto al personale.[VAR 0114(0006)]",
            "Troverai questo servizio nelle caffetterie dei\\nCentri Pokémon di tutta Alola.[VAR 0114(0006)]",
        ],
        [
            "Das Personal im Café kann deinen Pokémon\\nAttacken wieder beibringen, die sie auf ihrem\\r\\naktuellen Level bereits lernen könnten.[VAR 0114(0006)]",
            "Hat eines deiner Pokémon eine nützliche Attacke\\nvergessen, sprich einfach das Personal an.[VAR 0114(0006)]",
            "Diesen Service gibt es in den Café-Bereichen der\\nPokémon-Center in ganz Alola.[VAR 0114(0006)]",
        ],
        [
            "El encargado de la cafetería puede ayudar a tus\\nPokémon a recordar movimientos que ya podrían\\r\\nhaber aprendido a su nivel actual.[VAR 0114(0006)]",
            "Si alguno ha olvidado un movimiento útil,\\nhabla con el encargado para que pueda recordarlo.[VAR 0114(0006)]",
            "Encontrarás este servicio en las cafeterías de\\nlos Centros Pokémon de toda Alola.[VAR 0114(0006)]",
        ],
        [
            "카페 직원에게 부탁하면 포켓몬이\\n현재 레벨까지 배울 수 있는 기술을\\r\\n다시 떠올리게 할 수 있어요[VAR 0114(0006)]",
            "유용한 기술을 잊어버린 포켓몬이 있다면\\n직원에게 말을 걸어보세요[VAR 0114(0006)]",
            "이 서비스는 알로라의 모든 포켓몬센터\\n카페스페이스에서 이용할 수 있어요[VAR 0114(0006)]",
        ],
        [
            "咖啡区的工作人员可以帮助宝可梦\\n回忆起在当前等级前本来可以学会的\\r\\n招式。[VAR 0114(0006)]",
            "如果宝可梦忘记了有用的招式，\\n就去找工作人员帮忙吧。[VAR 0114(0006)]",
            "阿罗拉各地宝可梦中心的咖啡区\\n都提供这项服务。[VAR 0114(0006)]",
        ],
        [
            "咖啡區的工作人員可以幫助寶可夢\\n回想起在目前等級前原本可以學會的\\r\\n招式。[VAR 0114(0006)]",
            "如果寶可夢忘記了有用的招式，\\n就去找工作人員幫忙吧。[VAR 0114(0006)]",
            "阿羅拉各地寶可夢中心的咖啡區\\n都提供這項服務。[VAR 0114(0006)]",
        ],
    ];

    public static string Apply(string exefsPath, GameConfig config)
    {
        // Validate every localized StoryText bank before touching code.bin. This
        // prevents the gameplay patch from being written if the tutorial text is
        // from an unknown revision or was already customized by another mod.
        ValidateLillieTutorial(config);

        string codeReport = ApplyCode(exefsPath);
        string textReport = ApplyLillieTutorial(config);

        return codeReport + Environment.NewLine + textReport;
    }

    private static string ApplyCode(string exefsPath)
    {
        string codePath = FindCodeBinary(exefsPath);
        var info = new FileInfo(codePath);

        if ((info.Length & 0x1FF) != 0)
        {
            throw new InvalidDataException(
                "code.bin appears to be compressed. Decompress it before applying the USUM Move Relearner patch.");
        }

        byte[] code = File.ReadAllBytes(codePath);

        if (IsUltraMoonLayout(code))
            return ApplyUltraMoonSurgical(codePath, code);
        EnsureRange(code, CafePayloadAddress, CafePayload.Length);
        EnsureRange(code, LearnPayloadAddress, LearnPayload.Length);

        bool cafeHookDone = ValidateHook(
            code, CafeHookAddress, CafeHookOriginal, CafeHookPatched, "CAFE.asm hook");
        bool learnInitDone = ValidateHook(
            code, LearnInitHookAddress, LearnInitHookOriginal, LearnInitHookPatched, "Learn.asm init hook");
        bool learnCheckDone = ValidateHook(
            code, LearnCheckHookAddress, LearnCheckHookOriginal, LearnCheckHookPatched, "Learn.asm check hook");

        bool cafePayloadDone = Matches(code, ToOffset(CafePayloadAddress), CafePayload);
        bool learnPayloadDone = Matches(code, ToOffset(LearnPayloadAddress), LearnPayload);

        if (cafeHookDone && !cafePayloadDone)
            throw new InvalidDataException("The café hook is already modified, but its payload does not match this patch.");
        if ((learnInitDone || learnCheckDone) && !learnPayloadDone)
            throw new InvalidDataException("A Learn.asm hook is already modified, but its payload does not match this patch.");

        if (cafeHookDone && learnInitDone && learnCheckDone && cafePayloadDone && learnPayloadDone)
            return "The USUM Move Relearner patch is already applied. No files were changed.";

        string backupPath = codePath + ".pk3ds-usum-relearner.bak";
        if (!File.Exists(backupPath))
            File.Copy(codePath, backupPath);

        // Write detours first, then hooks.
        Write(code, ToOffset(CafePayloadAddress), CafePayload);
        Write(code, ToOffset(LearnPayloadAddress), LearnPayload);
        Write(code, ToOffset(CafeHookAddress), CafeHookPatched);
        Write(code, ToOffset(LearnInitHookAddress), LearnInitHookPatched);
        Write(code, ToOffset(LearnCheckHookAddress), LearnCheckHookPatched);

        File.WriteAllBytes(codePath, code);

        return
            "Pokémon Center cafés now open the Move Relearner." + Environment.NewLine +
            "Future level-up moves are filtered out; only moves available at the Pokémon's current level or earlier are offered." +
            Environment.NewLine + Environment.NewLine +
            "Backup: " + backupPath;
    }

    private static void ValidateLillieTutorial(GameConfig config)
    {
        if (config?.USUM != true)
            throw new NotSupportedException("Lillie's Move Relearner tutorial patch is available only for USUM.");
        if (string.IsNullOrWhiteSpace(config.RomFS) || !Directory.Exists(config.RomFS))
            throw new DirectoryNotFoundException("The loaded USUM RomFS folder could not be found.");

        int originalLanguage = config.Language;

        try
        {
            for (int language = 0; language < LillieTutorialOriginal.Length; language++)
            {
                config.Language = language;
                var story = config.GetGARCData("storytext");
                byte[][] files = story.Files;

                if ((uint)LillieTutorialFile >= (uint)files.Length)
                {
                    throw new InvalidDataException(
                        $"USUM StoryText language offset {language} does not contain file {LillieTutorialFile}.");
                }

                string[] lines = TextFile.GetStrings(config, files[LillieTutorialFile]);

                if (!MatchesLillieTutorial(lines, LillieTutorialOriginal[language]) &&
                    !MatchesLillieTutorial(lines, LillieTutorialPatched[language]))
                {
                    string actual =
                        lines.Length > LillieTutorialFirstLine + 2
                            ? string.Join(
                                " | ",
                                Enumerable.Range(LillieTutorialFirstLine, 3)
                                    .Select(index => $"{index}: {lines[index]}"))
                            : $"line count {lines.Length}";

                    throw new InvalidDataException(
                        $"Lillie tutorial text does not match the validated vanilla/already-patched text " +
                        $"for language offset {language}, StoryText file {LillieTutorialFile}. " +
                        $"Actual: {actual}. Nothing was changed.");
                }
            }
        }
        finally
        {
            config.Language = originalLanguage;
        }
    }

    private static string ApplyLillieTutorial(GameConfig config)
    {
        int originalLanguage = config.Language;
        int changedBanks = 0;

        try
        {
            for (int language = 0; language < LillieTutorialOriginal.Length; language++)
            {
                config.Language = language;

                var story = config.GetGARCData("storytext");
                byte[][] files = story.Files;
                string[] lines = TextFile.GetStrings(config, files[LillieTutorialFile]);

                if (MatchesLillieTutorial(lines, LillieTutorialPatched[language]))
                    continue;

                string storyPath =
                    Path.Combine(
                        config.RomFS,
                        config.GetGARCFileName("storytext"));

                string backupPath =
                    storyPath + ".pk3ds-usum-relearner-lillie.bak";

                if (!File.Exists(backupPath))
                    File.Copy(storyPath, backupPath);

                for (int line = 0; line < LillieTutorialPatched[language].Length; line++)
                {
                    lines[LillieTutorialFirstLine + line] =
                        LillieTutorialPatched[language][line];
                }

                files[LillieTutorialFile] =
                    TextFile.GetBytes(
                        config,
                        lines);

                story.Files = files;
                story.Save();
                changedBanks++;
            }
        }
        finally
        {
            config.Language = originalLanguage;
        }

        return changedBanks == 0
            ? "Lillie's early Pokémon Center tutorial already explains the café Move Relearner service."
            : $"Updated Lillie's early Pokémon Center tutorial in {changedBanks} StoryText language bank(s).";
    }

    private static bool MatchesLillieTutorial(
        string[] lines,
        string[] expected)
    {
        if (lines is null ||
            expected is null ||
            lines.Length < LillieTutorialFirstLine + expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    lines[LillieTutorialFirstLine + index],
                    expected[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUltraMoonLayout(byte[] code)
    {
        return
            Matches(code, 0x341654, MoonLayoutMarker1) &&
            Matches(code, 0x3417D8, MoonLayoutMarker2);
    }

    private static string ApplyUltraMoonSurgical(string codePath, byte[] code)
    {
        EnsureRange(code, CafePayloadAddress, CafePayload.Length);
        EnsureRange(code, LearnPayloadAddress, MoonLearnPayload.Length);

        bool cafeHookDone = ValidateHook(
            code,
            CafeHookAddress,
            CafeHookOriginal,
            CafeHookPatched,
            "Ultra Moon CAFE.asm hook");

        bool learnInitDone = ValidateHook(
            code,
            MoonLearnInitHookAddress,
            LearnInitHookOriginal,
            MoonLearnInitHookPatched,
            "Ultra Moon Learn.asm init hook");

        bool learnCheckDone = ValidateHook(
            code,
            MoonLearnCheckHookAddress,
            LearnCheckHookOriginal,
            MoonLearnCheckHookPatched,
            "Ultra Moon Learn.asm check hook");

        int cafePayloadOffset = ToOffset(CafePayloadAddress);
        int learnPayloadOffset = ToOffset(LearnPayloadAddress);

        bool cafePayloadDone =
            Matches(code, cafePayloadOffset, CafePayload);

        bool learnPayloadDone =
            Matches(code, learnPayloadOffset, MoonLearnPayload);

        // The payload caves are the only regions besides the three hooks that
        // Move Relearner owns. Unrelated modifications anywhere else in
        // code.bin are intentionally ignored.
        if (!cafePayloadDone &&
            !IsAllZero(code, cafePayloadOffset, CafePayload.Length))
        {
            throw new InvalidDataException(
                $"Ultra Moon Move Relearner code cave at file offset 0x{cafePayloadOffset:X8} " +
                "is already in use by another patch. Nothing was changed.");
        }

        if (!learnPayloadDone &&
            !IsAllZero(code, learnPayloadOffset, MoonLearnPayload.Length))
        {
            throw new InvalidDataException(
                $"Ultra Moon Move Relearner Learn code cave at file offset 0x{learnPayloadOffset:X8} " +
                "is already in use by another patch. Nothing was changed.");
        }

        if (cafeHookDone &&
            learnInitDone &&
            learnCheckDone &&
            cafePayloadDone &&
            learnPayloadDone)
        {
            return
                "The Ultra Moon Move Relearner patch is already applied. " +
                "No files were changed.";
        }

        string backupPath =
            codePath +
            ".pk3ds-usum-relearner.bak";

        if (!File.Exists(backupPath))
            File.Copy(codePath, backupPath);

        byte[] updated = (byte[])code.Clone();

        // Repair-safe order: payloads first, hooks last. This also allows a
        // partially installed patch to be completed when the remaining cave is
        // still clean.
        if (!cafePayloadDone)
            Write(updated, cafePayloadOffset, CafePayload);

        if (!learnPayloadDone)
            Write(updated, learnPayloadOffset, MoonLearnPayload);

        if (!cafeHookDone)
            Write(updated, ToOffset(CafeHookAddress), CafeHookPatched);

        if (!learnInitDone)
            Write(updated, ToOffset(MoonLearnInitHookAddress), MoonLearnInitHookPatched);

        if (!learnCheckDone)
            Write(updated, ToOffset(MoonLearnCheckHookAddress), MoonLearnCheckHookPatched);

        if (!Matches(updated, ToOffset(CafeHookAddress), CafeHookPatched) ||
            !Matches(updated, ToOffset(MoonLearnInitHookAddress), MoonLearnInitHookPatched) ||
            !Matches(updated, ToOffset(MoonLearnCheckHookAddress), MoonLearnCheckHookPatched) ||
            !Matches(updated, cafePayloadOffset, CafePayload) ||
            !Matches(updated, learnPayloadOffset, MoonLearnPayload))
        {
            throw new InvalidDataException(
                "Internal verification failed for the Ultra Moon Move Relearner patch. " +
                "The code.bin was not written.");
        }

        File.WriteAllBytes(codePath, updated);

        return
            "Ultra Moon Pokémon Center cafés now open the Move Relearner." +
            Environment.NewLine +
            "Applied the surgical Ultra Moon patch (3 hooks + 2 owned payload regions); " +
            "unrelated code.bin modifications were preserved." +
            Environment.NewLine +
            Environment.NewLine +
            "Backup: " +
            backupPath;
    }

    private static bool IsAllZero(byte[] code, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > code.Length - length)
            return false;

        return code
            .AsSpan(offset, length)
            .IndexOfAnyExcept((byte)0) < 0;
    }
    private static byte[] Hex(string value) =>
        Convert.FromHexString(value);

    private static string FindCodeBinary(string exefsPath)
    {
        if (string.IsNullOrWhiteSpace(exefsPath) || !Directory.Exists(exefsPath))
            throw new DirectoryNotFoundException("The loaded ExeFS folder could not be found.");

        string dotCode = Path.Combine(exefsPath, ".code.bin");
        if (File.Exists(dotCode))
            return dotCode;

        string code = Path.Combine(exefsPath, "code.bin");
        if (File.Exists(code))
            return code;

        string fallback = Directory.GetFiles(exefsPath)
            .FirstOrDefault(z => Path.GetFileName(z).Contains("code", StringComparison.OrdinalIgnoreCase));

        return fallback ?? throw new FileNotFoundException(
            "Could not find code.bin in the loaded ExeFS folder.");
    }

    private static bool ValidateHook(
        byte[] code,
        int address,
        byte[] original,
        byte[] patched,
        string name)
    {
        int offset = ToOffset(address);
        EnsureRange(code, address, patched.Length);

        if (Matches(code, offset, patched))
            return true;
        if (Matches(code, offset, original))
            return false;

        throw new InvalidDataException(
            $"{name} at 0x{address:X8} does not match the expected clean USUM instruction. " +
            "The code.bin may be a different revision or another patch may already use this hook.");
    }

    private static int ToOffset(int address) => address - CodeBaseAddress;

    private static void EnsureRange(byte[] code, int address, int length)
    {
        int offset = ToOffset(address);
        if (offset < 0 || offset > code.Length - length)
        {
            throw new InvalidDataException(
                $"code.bin is too small for the required patch address 0x{address:X8}.");
        }
    }

    private static bool Matches(byte[] code, int offset, byte[] expected)
    {
        if (offset < 0 || offset > code.Length - expected.Length)
            return false;

        return code.AsSpan(offset, expected.Length).SequenceEqual(expected);
    }

    private static void Write(byte[] code, int offset, byte[] value) =>
        value.CopyTo(code, offset);
}
