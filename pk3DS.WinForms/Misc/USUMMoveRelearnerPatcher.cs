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
    // Ultra Moon exact-delta integration.
    //
    // Reference pair:
    //   clean  SHA-256: 0aa12056d403a7d747d9c299e1dfbd3c0d5e4ce147675bc8300a8d61e999919b
    //   patched SHA-256: 2539ea304d7a428cbf8e9c0591900e07aa97449590b4c438ff701395a49ea41b
    //
    // These are the 143 contiguous byte-difference runs between the two
    // validated binaries: 516 changed bytes total. Only bytes that actually
    // differ are written; unchanged neighboring bytes are left untouched.
    // Offsets below are file offsets in decompressed code.bin.
    private static readonly (int Offset, byte[] Original, byte[] Patched)[] MoonExactPatches =
    [
        (0x00225AD4, Hex("000055E1"), Hex("CA4F0AEB")),
        (0x002843F4, Hex("EA68FD"), Hex("01D608")),
        (0x0029A044, Hex("F04F2DE9"), Hex("D57F08EA")),
        (0x002A72D4, Hex("08"), Hex("00")),
        (0x002A72D7, Hex("0A"), Hex("00")),
        (0x002D0058, Hex("01"), Hex("03")),
        (0x002D005B, Hex("E1"), Hex("E3")),
        (0x002D08FC, Hex("00"), Hex("03")),
        (0x002D08FE, Hex("D0E5"), Hex("A0E3")),
        (0x00341658, Hex("F0432DE9"), Hex("A9E105EA")),
        (0x003417DC, Hex("020050E3"), Hex("4CE105EA")),
        (0x003455F8, Hex("40"), Hex("00")),
        (0x003455FA, Hex("9DE5"), Hex("A0E3")),
        (0x003456D0, Hex("CE02"), Hex("EC01")),
        (0x003AC4B8, Hex("DC"), Hex("A0")),
        (0x003AC4C4, Hex("DC"), Hex("A0")),
        (0x003AC4DC, Hex("DC"), Hex("A0")),
        (0x004B99F8, Hex("00"), Hex("01")),
        (0x004B99FA, Hex("00000000000000"), Hex("80E27E402DE903")),
        (0x004B9A03, Hex("0000000000"), Hex("EA7E402DE9")),
        (0x004B9A0A, Hex("000000"), Hex("55E120")),
        (0x004B9A0F, Hex("0000"), Hex("0A05")),
        (0x004B9A12, Hex("000000"), Hex("A0E164")),
        (0x004B9A16, Hex("000000"), Hex("50E31D")),
        (0x004B9A1B, Hex("00000000000000000000000000"), Hex("8A7C409FE57C309FE57C508FE2")),
        (0x004B9A29, Hex("00000000"), Hex("10D5E5FF")),
        (0x004B9A2E, Hex("000000"), Hex("51E30F")),
        (0x004B9A33, Hex("00"), Hex("0A")),
        (0x004B9A36, Hex("000000"), Hex("51E308")),
        (0x004B9A3B, Hex("0000"), Hex("0A01")),
        (0x004B9A3E, Hex("000000"), Hex("51E313")),
        (0x004B9A43, Hex("000000000000000000000000000000000000"), Hex("1AB210D5E1811083E0B010D1E1B460D5E106")),
        (0x004B9A56, Hex("000000"), Hex("51E10B")),
        (0x004B9A5B, Hex("0000"), Hex("2A04")),
        (0x004B9A5F, Hex("0000000000000000000000000000"), Hex("EAB210D5E10110D4E70460D5E506")),
        (0x004B9A6E, Hex("000000"), Hex("11E105")),
        (0x004B9A73, Hex("000000000000"), Hex("1A0160D5E506")),
        (0x004B9A7A, Hex("000000"), Hex("50E104")),
        (0x004B9A7F, Hex("0000"), Hex("8A01")),
        (0x004B9A82, Hex("0000"), Hex("A0E3")),
        (0x004B9A86, Hex("0000000000000000000000000000"), Hex("50E37E80BDE8065085E2E4FFFFEA")),
        (0x004B9A96, Hex("0000"), Hex("A0E3")),
        (0x004B9A9A, Hex("0000000000000000000000000000"), Hex("50E37E80BDE8D4360133042F0133")),
        (0x004B9AA9, Hex("0000"), Hex("0E10")),
        (0x004B9AAC, Hex("00"), Hex("08")),
        (0x004B9AAF, Hex("0000"), Hex("1311")),
        (0x004B9AB2, Hex("00"), Hex("08")),
        (0x004B9AB5, Hex("0000"), Hex("1810")),
        (0x004B9AB8, Hex("00"), Hex("10")),
        (0x004B9ABB, Hex("0000"), Hex("1A10")),
        (0x004B9ABE, Hex("00"), Hex("40")),
        (0x004B9AC1, Hex("0000"), Hex("1D10")),
        (0x004B9AC4, Hex("00"), Hex("20")),
        (0x004B9AC7, Hex("0000"), Hex("2211")),
        (0x004B9ACA, Hex("00"), Hex("10")),
        (0x004B9ACD, Hex("0000"), Hex("2810")),
        (0x004B9AD0, Hex("00"), Hex("80")),
        (0x004B9AD3, Hex("0000"), Hex("2A11")),
        (0x004B9AD6, Hex("00"), Hex("01")),
        (0x004B9AD9, Hex("00000000"), Hex("33980180")),
        (0x004B9ADF, Hex("0000"), Hex("3511")),
        (0x004B9AE2, Hex("00"), Hex("20")),
        (0x004B9AE5, Hex("00000000"), Hex("388B0180")),
        (0x004B9AEB, Hex("0000"), Hex("3B11")),
        (0x004B9AEE, Hex("00"), Hex("02")),
        (0x004B9AF0, Hex("000000"), Hex("013C44")),
        (0x004B9AF4, Hex("0000"), Hex("0407")),
        (0x004B9AF7, Hex("0000"), Hex("4213")),
        (0x004B9AFA, Hex("00"), Hex("20")),
        (0x004B9AFD, Hex("0000"), Hex("4311")),
        (0x004B9B00, Hex("00"), Hex("40")),
        (0x004B9B02, Hex("000000"), Hex("014444")),
        (0x004B9B06, Hex("0000"), Hex("3A07")),
        (0x004B9B09, Hex("00000000"), Hex("488F0101")),
        (0x004B9B0F, Hex("00000000"), Hex("48B80120")),
        (0x004B9B15, Hex("00000000"), Hex("488E0120")),
        (0x004B9B1B, Hex("00000000"), Hex("488E0102")),
        (0x004B9B20, Hex("000000"), Hex("014B44")),
        (0x004B9B24, Hex("00000000"), Hex("D007FF64")),
        (0x004B9C02, Hex("00000000000000000000"), Hex("51E31EFF2F01E592F4EA")),
        (0x004B9D04, Hex("0000000000000000"), Hex("F0432DE938409FE5")),
        (0x004B9D0D, Hex("0000000000000000000000"), Hex("1084E5511EFAEA1F402DE9")),
        (0x004B9D19, Hex("00000000"), Hex("40A0E124")),
        (0x004B9D1E, Hex("0000"), Hex("9FE5")),
        (0x004B9D22, Hex("00000000000000"), Hex("90E5E5A6F5EB04")),
        (0x004B9D2A, Hex("0000"), Hex("50E1")),
        (0x004B9D2F, Hex("0000"), Hex("BA01")),
        (0x004B9D33, Hex("0000000000000000000000000000"), Hex("EA1F40BDE8E21EFAEA1F40BDE802")),
        (0x004B9D42, Hex("000000000000"), Hex("50E3A51EFAEA")),
        (0x004B9D49, Hex("0000"), Hex("9D5B")),
        (0x004B9FA0, Hex("00"), Hex("04")),
        (0x004B9FA2, Hex("00000000000000"), Hex("2DE924209FE502")),
        (0x004B9FAA, Hex("000000"), Hex("51E104")),
        (0x004B9FAF, Hex("000000000000"), Hex("BA1C209FE502")),
        (0x004B9FB6, Hex("000000"), Hex("51E101")),
        (0x004B9FBB, Hex("00000000000000000000"), Hex("CAFFFFFFEA10109FE504")),
        (0x004B9FC6, Hex("000000000000000000000000"), Hex("BDE8F04F2DE91D80F7EAAA14")),
        (0x004B9FD4, Hex("0000"), Hex("B714")),
        (0x004B9FD8, Hex("0000"), Hex("7C15")),
        (0x004BB98E, Hex("0E0251"), Hex("10009F")),
        (0x004BB992, Hex("D9015B012E005C"), Hex("350275003902F2")),
        (0x004BB99A, Hex("02015301DA"), Hex("84002B0036")),
        (0x004BB9A0, Hex("ED"), Hex("DE")),
        (0x004BB9A2, Hex("F1"), Hex("C1")),
        (0x004BB9A4, Hex("0D"), Hex("98")),
        (0x004BB9A6, Hex("3A003B003F"), Hex("2A021802D3")),
        (0x004BB9AC, Hex("71"), Hex("08")),
        (0x004BB9AE, Hex("B600F0"), Hex("CA02F8")),
        (0x004BB9B2, Hex("63"), Hex("A7")),
        (0x004BB9B4, Hex("DB00DA004C"), Hex("B4021F02F1")),
        (0x004BB9BA, Hex("DF"), Hex("77")),
        (0x004BB9BC, Hex("5500570059"), Hex("9C01B502D1")),
        (0x004BB9C2, Hex("D8008D005E"), Hex("C901090240")),
        (0x004BB9C8, Hex("F7001801680073"), Hex("63029C021A019C")),
        (0x004BB9D0, Hex("E2"), Hex("B7")),
        (0x004BB9D2, Hex("3500BC"), Hex("C10237")),
        (0x004BB9D6, Hex("C9007E"), Hex("690154")),
        (0x004BB9DA, Hex("3D"), Hex("A8")),
        (0x004BB9DC, Hex("4C0103"), Hex("690090")),
        (0x004BB9E0, Hex("07"), Hex("F9")),
        (0x004BB9E2, Hex("E8"), Hex("28")),
        (0x004BB9E4, Hex("9C"), Hex("53")),
        (0x004BB9E6, Hex("D5"), Hex("58")),
        (0x004BB9E8, Hex("A800EA01F001F1013B01D3009B"), Hex("4F021B022D0012026002B30254")),
        (0x004BB9F6, Hex("9C"), Hex("79")),
        (0x004BB9F8, Hex("CE00F7017601C3"), Hex("6702B600B00073")),
        (0x004BBA00, Hex("FB"), Hex("26")),
        (0x004BBA02, Hex("B502FF01050100"), Hex("4200B2026A000D")),
        (0x004BBA0A, Hex("75"), Hex("6B")),
        (0x004BBA0C, Hex("99"), Hex("EB")),
        (0x004BBA0E, Hex("A5"), Hex("E5")),
        (0x004BBA10, Hex("7301AC02A0"), Hex("AF005C01D5")),
        (0x004BBA18, Hex("B602BC0109"), Hex("C700E100CB")),
        (0x004BBA1E, Hex("560068"), Hex("2401A1")),
        (0x004BBA22, Hex("0E001300F4"), Hex("9E029301B9")),
        (0x004BBA28, Hex("0B020C029D"), Hex("AE01BD00E7")),
        (0x004BBA2E, Hex("94"), Hex("97")),
        (0x004BBA30, Hex("0D0263028E018A00BF01CF00D6007101A400AE01B10110"), Hex("35015D015802C001AE002402C101BF008801CC02A6025B")),
        (0x004BBA48, Hex("39"), Hex("41")),
        (0x004BBA4A, Hex("2B020B"), Hex("FB0129")),
        (0x004BBA4E, Hex("8F"), Hex("3C")),
        (0x004BBA50, Hex("7F"), Hex("92")),
        (0x004BBA52, Hex("5D024E02"), Hex("03010E01")),
    ];

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
            return ApplyUltraMoonExact(codePath, code);
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

    private static string ApplyUltraMoonExact(string codePath, byte[] code)
    {
        bool allPatched = true;

        // Validate the entire recipe before writing anything.
        foreach (var patch in MoonExactPatches)
            allPatched &= ValidateMoonExactPatch(code, patch);

        if (allPatched)
        {
            return
                "The verified Ultra Moon Move Relearner patch is already applied. " +
                "No files were changed.";
        }

        string backupPath =
            codePath +
            ".pk3ds-usum-relearner-ultra-moon-clean.bak";

        if (!File.Exists(backupPath))
            File.Copy(codePath, backupPath);

        byte[] updated = (byte[])code.Clone();

        foreach (var patch in MoonExactPatches)
            Write(updated, patch.Offset, patch.Patched);

        foreach (var patch in MoonExactPatches)
        {
            if (!Matches(updated, patch.Offset, patch.Patched))
            {
                throw new InvalidDataException(
                    $"Internal verification failed for Ultra Moon patch at file offset 0x{patch.Offset:X8}. " +
                    "The code.bin was not written.");
            }
        }

        File.WriteAllBytes(codePath, updated);

        return
            "Ultra Moon Pokémon Center cafés now open the Move Relearner." +
            Environment.NewLine +
            "Applied the exact validated clean-to-working Ultra Moon delta " +
            "(143 changed runs / 516 bytes)." +
            Environment.NewLine +
            Environment.NewLine +
            "Backup: " +
            backupPath;
    }

    private static bool ValidateMoonExactPatch(
        byte[] code,
        (int Offset, byte[] Original, byte[] Patched) patch)
    {
        if (Matches(code, patch.Offset, patch.Patched))
            return true;

        if (Matches(code, patch.Offset, patch.Original))
            return false;

        int length = Math.Min(patch.Patched.Length, 16);

        string actual =
            patch.Offset >= 0 &&
            patch.Offset <= code.Length - patch.Patched.Length
                ? string.Join(
                    " ",
                    code
                        .Skip(patch.Offset)
                        .Take(length)
                        .Select(z => z.ToString("X2")))
                : "out-of-range";

        throw new InvalidDataException(
            $"Ultra Moon code.bin does not match the validated clean/already-patched bytes at file offset 0x{patch.Offset:X8}. " +
            $"Actual: {actual}. Nothing was changed.");
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
