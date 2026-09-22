using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace pk3DS.WinForms;

/// <summary>
/// Lightweight visual refresh layer for the legacy WinForms UI.
/// It styles forms as they open without rewriting every designer file.
/// </summary>
internal static class ModernUI
{
    internal static readonly Color Background = Color.FromArgb(247, 248, 250);
    internal static readonly Color Surface = Color.White;
    internal static readonly Color SurfaceAlt = Color.FromArgb(250, 251, 252);
    internal static readonly Color Border = Color.FromArgb(222, 226, 230);
    internal static readonly Color Text = Color.FromArgb(32, 38, 46);
    internal static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    internal static readonly Color Accent = Color.FromArgb(71, 85, 105);
    internal static readonly Color AccentSoft = Color.FromArgb(241, 245, 249);
    internal static readonly Color AccentHover = Color.FromArgb(226, 232, 240);
    internal static readonly Color AccentBorder = Color.FromArgb(203, 213, 225);
    internal static readonly Color Danger = Color.FromArgb(185, 28, 28);

    private static readonly Font UiFont = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font UiFontBold = new("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly ConditionalWeakTable<Control, Marker> StyledControls = new();
    private static readonly ConditionalWeakTable<Form, Marker> LayoutFixedForms = new();
    private static bool enabled;

    private sealed class Marker { }

    internal static void EnableGlobalStyling()
    {
        if (enabled)
            return;

        enabled = true;
        Application.Idle += (_, _) => ApplyOpenForms();
    }

    private static void ApplyOpenForms()
    {
        foreach (Form form in Application.OpenForms)
            Apply(form);
    }

    internal static void Apply(Form form)
    {
        if (form == null || form.IsDisposed)
            return;

        // Several Gen 7 editors use very tight, fixed-position WinForms layouts.
        // The global refresh was making those screens overlap badly, so leave
        // them on their original layout until each one is redesigned directly in
        // its own Designer file. Gen 6 screens that already look correct are not
        // touched by this guard.
        if (ShouldSkipModernUI(form))
        {
            ApplySkippedFormLayoutFixes(form);
            return;
        }

        ApplyControl(form);
        ApplyFormLayoutFixes(form);
    }

    private static bool ShouldSkipModernUI(Form form)
    {
        return form.GetType().Name switch
        {
            // Gen 7 trainer/static/wild/move/item editors: keep original UI for now.
            "SMTE" => true,
            "StaticEncounterEditor7" => true,
            "SMWE" => true,
            "MoveEditor7" => true,
            "EggMoveEditor7" => true,
            "PersonalEditor7" => true,
            "TMEditor7" => true,
            _ => false,
        };
    }

    private static void ApplyControl(Control control)
    {
        if (control == null || control.IsDisposed)
            return;

        if (!StyledControls.TryGetValue(control, out _))
        {
            StyledControls.Add(control, new Marker());
            StyleControl(control);
            control.ControlAdded += (_, e) => ApplyControl(e.Control);
        }

        foreach (Control child in control.Controls)
            ApplyControl(child);
    }


    private static void ApplyFormLayoutFixes(Form form)
    {
        if (form == null || form.IsDisposed || LayoutFixedForms.TryGetValue(form, out _))
            return;

        LayoutFixedForms.Add(form, new Marker());

        // Keep this layer conservative. Most pk3DS editors use fixed WinForms
        // coordinates, so broad automatic resizing/moving can make old screens
        // overlap or clip. Only forms that were explicitly rebuilt should get
        // layout assistance here.
        string name = form.GetType().Name;
        switch (name)
        {
            case "TrainerRand":
                FixTrainerRand(form);
                break;
            case "TrainerLevelCapDialog":
                FixTrainerLevelCapDialog(form);
                break;
            case "TrainerMoveRulesDialog":
                FixTrainerMoveRulesDialog(form);
                break;
            case "EvolutionEditor6":
            case "EvolutionEditor7":
                FixEvolutionEditor(form);
                break;
            case "XYWE":
            case "RSWE":
                FixWildEditor(form);
                break;
            case "SMWE":
                FixGen7WildEditor(form);
                break;
            case "MoveEditor6":
                FixMoveEditor(form);
                break;
            case "MoveEditor7":
                FixMoveEditor7(form);
                break;
            case "LevelUpEditor6":
            case "LevelUpEditor7":
                FixLevelUpEditor(form);
                break;
            case "EggMoveEditor6":
                FixEggMoveEditor(form);
                break;
            case "EggMoveEditor7":
                FixEggMoveEditor7(form);
                break;
            case "StarterEditor6":
                FixStarterEditor(form);
                break;
            case "GiftEditor6":
                FixGiftEditor(form);
                break;
            case "StaticEncounterEditor7":
                FixStaticEncounterEditor7(form);
                break;
            case "ItemEditor6":
            case "ItemEditor7":
                FixItemEditor(form);
                break;
            case "ShinyRate":
                FixShinyRateEditor(form);
                break;
            case "PersonalEditor7":
                FixPersonalEditor7(form);
                break;
            case "TMEditor7":
                FixTMEditor7(form);
                break;
            case "RSTE":
                FixTrainerEditor(form);
                break;
            case "SMTE":
                FixGen7TrainerEditor(form);
                break;
        }
    }


    private static void ApplySkippedFormLayoutFixes(Form form)
    {
        if (form == null || form.IsDisposed || LayoutFixedForms.TryGetValue(form, out _))
            return;

        LayoutFixedForms.Add(form, new Marker());

        switch (form.GetType().Name)
        {
            case "SMTE":
                FixSkippedGen7TrainerEditor(form);
                break;
            case "SMWE":
                FixSkippedGen7WildEditor(form);
                break;
            case "StaticEncounterEditor7":
                FixSkippedStaticEncounterEditor7(form);
                break;
            case "MartEditor6":
            case "MartEditor7":
            case "MartEditor7UU":
                FixMartButtons(form);
                break;
        }
    }

    private static void FixSkippedGen7TrainerEditor(Form form)
    {
        // Gen7 Trainer Editor is extremely position-sensitive. Do not move the
        // Stats/Moves controls or Trainer controls globally; that was pushing
        // entire pages under the tab border. Keep the original designer layout
        // and apply only the two small fixes that are known to be safe.
        EnsureClientSize(form, 760, 540);
        form.MinimumSize = new Size(740, 520);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        if (FindControl<TabPage>(form, "Tab_Trainer1") is { } trainer)
        {
            foreach (var label in FindControls<Label>(trainer))
            {
                if (label.Text.StartsWith("Min PKM", StringComparison.OrdinalIgnoreCase))
                    MoveAndSize(label, 255, 34, 80, 20);
                else if (label.Text.StartsWith("Max PKM", StringComparison.OrdinalIgnoreCase))
                    MoveAndSize(label, 255, 66, 80, 20);
            }

            MoveAndSize(FindControl<NumericUpDown>(trainer, "NUD_RMin"), 338, 30, 56, 24);
            MoveAndSize(FindControl<NumericUpDown>(trainer, "NUD_RMax"), 338, 62, 56, 24);
        }
    }

    private static void FixSkippedGen7WildEditor(Form form)
    {
        EnsureClientSize(form, 1420, 820);
        form.MinimumSize = new Size(1280, 760);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        form.AutoScrollMinSize = new Size(0, 1050);
        MakeTextControlsReadable(form);

        // Keep the dense encounter grid untouched. Extra Randomization Tweaks is
        // a global panel, so place it below the encounter editor and make it
        // reachable with the form scrollbar. Do not let it float over Pokémon.
        foreach (var group in FindControls<GroupBox>(form))
        {
            if (!group.Text.Contains("Extra Randomization", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ReferenceEquals(group.Parent, form))
            {
                group.Parent?.Controls.Remove(group);
                form.Controls.Add(group);
            }

            group.AutoSize = false;
            MoveAndSize(group, 18, 745, Math.Max(760, Math.Min(920, form.ClientSize.Width - 36)), 270);
            group.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            group.Visible = true;
            group.BringToFront();
        }
    }

    private static void FixSkippedStaticEncounterEditor7(Form form)
    {
        EnsureClientSize(form, 760, 640);
        form.MinimumSize = new Size(720, 600);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        if (FindControl<TabPage>(form, "Tab_Trades") is not { } trades)
            return;

        trades.AutoScroll = true;

        // Legacy trade helper buttons used to live inside Tab_Trades.
        // Scope this compatibility layout strictly to that tab. pk3DS Progressive
        // now exposes its trade tools in Randomizer Options, so a form-wide scan
        // would incorrectly move those new buttons to the old Trades coordinates.
        foreach (var button in FindControls<Button>(trades))
        {
            bool isAcceptAny =
                button.Name.Contains(
                    "TradeAny",
                    StringComparison.OrdinalIgnoreCase) ||
                button.Text.Contains(
                    "accept any",
                    StringComparison.OrdinalIgnoreCase);

            bool isRandomOffer =
                button.Name.Contains(
                    "RandomOffer",
                    StringComparison.OrdinalIgnoreCase) ||
                button.Text.Contains(
                    "random offer",
                    StringComparison.OrdinalIgnoreCase);

            if (!isAcceptAny &&
                !isRandomOffer)
            {
                continue;
            }

            if (isAcceptAny)
            {
                button.Text =
                    "Trades accept any Pokemon";

                MoveAndSize(
                    button,
                    315,
                    270,
                    285,
                    30);
            }
            else
            {
                button.Text =
                    "Any request + random offer";

                MoveAndSize(
                    button,
                    315,
                    308,
                    285,
                    30);
            }

            button.Anchor =
                AnchorStyles.Left |
                AnchorStyles.Top;

            StyleButton(
                button);
        }
    }
    private static void FixTrainerLevelCapDialog(Form form)
    {
        EnsureClientSize(form, 1100, 630);
        form.MinimumSize = new Size(1040, 600);
        MakeTextControlsReadable(form);
        foreach (var button in FindControls<Button>(form))
        {
            switch (button.Text)
            {
                case "Select":
                    button.Text = "Select None";
                    button.Width = Math.Max(button.Width, 110);
                    break;
                case "Select None":
                    button.Width = Math.Max(button.Width, 110);
                    break;
                case "Select All":
                    button.Width = Math.Max(button.Width, 105);
                    break;
                case "OK":
                    button.Width = Math.Max(button.Width, 90);
                    break;
            }
        }
    }

    private static void FixTrainerMoveRulesDialog(Form form)
    {
        EnsureClientSize(form, 1540, 700);
        form.MinimumSize = new Size(1480, 660);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        foreach (var label in FindControls<Label>(form))
        {
            // The long help paragraph was useful while developing, but it costs
            // vertical space in the cleaned UI. Hide it; documentation now covers it.
            if (label.Text.Contains("Strong Stat compares", StringComparison.OrdinalIgnoreCase) ||
                label.Text.Contains("Better Movesets forces", StringComparison.OrdinalIgnoreCase) ||
                label.Text.Contains("Smart Items gives", StringComparison.OrdinalIgnoreCase))
                label.Visible = false;
        }

        int bottomY = Math.Max(610, form.ClientSize.Height - 46);
        var layout = new (string Text, int X, int W)[]
        {
            ("Select All", 18, 110),
            ("Select None", 136, 120),
            ("Allow Status All", 266, 140),
            ("Allow Status None", 416, 150),
            ("Better Movesets All", 576, 165),
            ("Better Movesets None", 751, 180),
            ("Smart Items All", 941, 150),
            ("Smart Items None", 1101, 160),
            ("Cancel", 1340, 86),
            ("OK", 1436, 70),
        };

        foreach (var (text, x, w) in layout)
        {
            foreach (var button in FindControls<Button>(form).Where(b => b.Text.Equals(text, StringComparison.OrdinalIgnoreCase)))
            {
                MoveAndSize(button, x, bottomY, w, 30);
                button.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
                StyleButton(button);
                button.Visible = true;
                button.BringToFront();
            }
        }

        foreach (var button in FindControls<Button>(form))
        {
            if (button.Text == "Select")
                button.Text = "Select None";
            if (button.Text == "OK" || button.Text == "Cancel")
                StyleButton(button);
        }
    }

    private static void FixEvolutionEditor(Form form)
    {
        EnsureClientSize(form, 740, 660);
        form.MinimumSize = new Size(720, 640);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        var buttons = FindControls<Button>(form)
            .Where(b => b.Text.Contains("evolution", StringComparison.OrdinalIgnoreCase) ||
                        b.Text.Contains("Randomize", StringComparison.OrdinalIgnoreCase) ||
                        b.Text.Contains("Trade", StringComparison.OrdinalIgnoreCase) ||
                        b.Text.Contains("Normalize", StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.Top)
            .ToList();

        int x = Math.Max(500, form.ClientSize.Width - 205);
        int y = Math.Max(350, form.ClientSize.Height - 178);
        foreach (var button in buttons)
        {
            MoveAndSize(button, x, y, 190, 32);
            y += 40;
        }
    }

    private static void FixMartButtons(Form form)
    {
        MakeTextControlsReadable(form);
        foreach (var button in FindControls<Button>(form))
        {
            if (button.Name == "B_AddRareCandies" || button.Text.StartsWith("Add rare", StringComparison.OrdinalIgnoreCase))
            {
                button.Text = "Add Rare Candies";
                button.Width = Math.Max(button.Width, 148);
            }
        }
    }

    private static void FixTrainerRand(Form form)
    {
        EnsureClientSize(form, 1020, 760);
        form.MinimumSize = new Size(1000, 730);
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        // Do not enlarge GB_Tweak here. It is intentionally short in
        // TrainerRand so the Mega Evolution options can live below it; making
        // the group taller visually swallowed those options.
        if (FindControl<GroupBox>(form, "GB_Tweak") is { } tweak)
            tweak.Height = Math.Min(tweak.Height, 155);

        if (FindControl<CheckBox>(form, "CHK_6PKM") is { } six)
            six.MaximumSize = Size.Empty;
        if (FindControl<CheckBox>(form, "CHK_StoryMEvos") is { } story)
            story.MaximumSize = Size.Empty;
        if (FindControl<CheckBox>(form, "CHK_ForceHighPower") is { } highPower)
            highPower.MaximumSize = Size.Empty;

        // Give the after-battle gift percentage a bit more room so the number,
        // percent sign and Max AI checkbox do not feel glued together.
        if (FindControl<NumericUpDown>(form, "NUD_GiftPercent") is { } gift)
            MoveAndSize(gift, gift.Left + 28, gift.Top, 58, gift.Height);
        if (FindControl<Label>(form, "label1") is { } percent)
            percent.Location = new Point(percent.Left + 34, percent.Top);
        if (FindControl<CheckBox>(form, "CHK_MaxDiffAI") is { } maxAi)
            maxAi.Location = new Point(maxAi.Left + 42, maxAi.Top);
    }

    private static void FixWildEditor(Form form)
    {
        EnsureClientSize(form, 1260, 760);
        form.MinimumSize = new Size(1180, 720);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        var tweak = FindControl<GroupBox>(form, "GB_Tweak");
        const int bottomPanelHeight = 210;

        if (FindControl<TabControl>(form, "Tab_Main") is { } tabs)
        {
            tabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tabs.Width = Math.Max(tabs.Width, form.ClientSize.Width - 24);
            tabs.Height = Math.Max(440, form.ClientSize.Height - bottomPanelHeight - tabs.Top - 28);
        }

        if (tweak is { })
        {
            // Extra Tweaks are global randomizer controls, not encounter-table
            // controls. Keep them below the tabs in a wide strip so they are
            // always visible and never hidden inside Land/Water/Horde/Flowers.
            if (!ReferenceEquals(tweak.Parent, form))
            {
                tweak.Parent?.Controls.Remove(tweak);
                form.Controls.Add(tweak);
            }

            int y = Math.Max(510, form.ClientSize.Height - bottomPanelHeight + 8);
            MoveAndSize(tweak, 12, y, Math.Min(1120, form.ClientSize.Width - 24), 205);
            tweak.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            tweak.Visible = true;
            tweak.BringToFront();

            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_Level"), 18, 24, 170, 22);
            MoveAndSize(FindControl<NumericUpDown>(tweak, "NUD_LevelAmp"), 210, 22, 58, 22);
            MoveAndSize(FindControl<Button>(tweak, "B_LevelPlus"), 285, 18, 96, 28);
            MoveAndSize(FindControl<Label>(tweak, "L_RandOpt"), 30, 56, 160, 18);

            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_G1"), 32, 82, 62, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_G2"), 32, 106, 62, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_G3"), 32, 130, 62, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_G4"), 112, 82, 62, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_G5"), 112, 106, 62, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_G6"), 112, 130, 62, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_L"), 205, 82, 125, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_E"), 205, 106, 125, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_BST"), 205, 130, 150, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_HomogeneousHordes"), 390, 82, 185, 20);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_MegaForm"), 390, 106, 170, 20);

            // Keep the advanced wild-level row below the generation/options grid.
            // It was being created at the very bottom of GB_Tweak, causing the
            // "+ flat" and "%" labels to be clipped by the group border.
            const int scaleY = 154;
            MoveAndSize(FindControl<Label>(tweak, "L_AdvancedWildLevels"), 18, scaleY + 4, 120, 18);
            MoveAndSize(FindControl<NumericUpDown>(tweak, "NUD_WildLevelFlat"), 145, scaleY, 58, 24);
            if (FindLabelByText(tweak, "+ flat") is { } flatLabel)
                MoveAndSize(flatLabel, 210, scaleY + 4, 46, 18);
            MoveAndSize(FindControl<NumericUpDown>(tweak, "NUD_WildLevelMultiplier"), 262, scaleY, 62, 24);
            if (FindLabelByText(tweak, "%") is { } percentLabel)
                MoveAndSize(percentLabel, 330, scaleY + 4, 22, 18);
            MoveAndSize(FindControl<CheckBox>(tweak, "CHK_WildLevelKeepRange"), 360, scaleY + 2, 155, 22);
            MoveAndSize(FindControl<Button>(tweak, "B_AdvancedWildLevels"), 560, scaleY - 2, 150, 28);
            tweak.Height = Math.Max(tweak.Height, 215);
        }

        foreach (var combo in FindControls<ComboBox>(form))
        {
            if (combo.Name.StartsWith("CB_Horde", StringComparison.OrdinalIgnoreCase))
                combo.Width = Math.Max(combo.Width, 145);
        }
    }


    private static void FixMoveEditor(Form form)
    {
        EnsureClientSize(form, 660, 620);
        form.MinimumSize = new Size(650, 600);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        int right = Math.Max(430, form.ClientSize.Width - 168);
        MoveAndSize(FindControl<Button>(form, "B_RandAll"), right, 10, 140, 32);
        MoveAndSize(FindControl<Button>(form, "B_Metronome"), right, 48, 140, 32);
        MoveAndSize(FindControl<Button>(form, "B_BalanceMoves"), right, 86, 140, 32);
        MoveAndSize(FindControl<Button>(form, "B_Table"), right, 132, 140, 32);
        MoveAndSize(FindControl<GroupBox>(form, "groupBox1"), right, 170, 140, 78);
        MoveAndSize(FindControl<CheckedListBox>(form, "CLB_Flags"), right, 258, 155, Math.Max(250, form.ClientSize.Height - 270));

        // Separate stat changes from the core fields; the percentage columns
        // need extra width or the first digit gets clipped by the spinner.
        if (FindControl<GroupBox>(form, "GB_Stat") is { } statBox)
        {
            MoveAndSize(statBox, 11, 292, 330, 138);
            EnsureChildLabel(statBox, "L_StatNameHeader", "Stat", 36, 20);
            EnsureChildLabel(statBox, "L_StatStageHeader", "Stage", 170, 20);
            EnsureChildLabel(statBox, "L_StatChanceHeader", "%", 252, 20);
            MoveAndSize(FindControl<Label>(statBox, "L_Stage1"), 8, 44, 20, 18);
            MoveAndSize(FindControl<Label>(statBox, "L_Stage2"), 8, 74, 20, 18);
            MoveAndSize(FindControl<Label>(statBox, "L_Stage3"), 8, 104, 20, 18);
            MoveAndSize(FindControl<ComboBox>(statBox, "CB_Stat1"), 30, 40, 120, 24);
            MoveAndSize(FindControl<ComboBox>(statBox, "CB_Stat2"), 30, 70, 120, 24);
            MoveAndSize(FindControl<ComboBox>(statBox, "CB_Stat3"), 30, 100, 120, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_Stat1"), 170, 40, 66, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_Stat2"), 170, 70, 66, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_Stat3"), 170, 100, 66, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_StatP1"), 252, 40, 70, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_StatP2"), 252, 70, 70, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_StatP3"), 252, 100, 70, 24);
        }

        if (FindControl<RichTextBox>(form, "RTB") is { } description)
        {
            description.Location = new Point(12, 448);
            description.Width = Math.Max(description.Width, right - description.Left - 16);
            description.Height = Math.Max(90, form.ClientSize.Height - description.Top - 12);
            description.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        }
    }


    private static void FixLevelUpEditor(Form form)
    {
        EnsureClientSize(form, 760, 650);
        form.MinimumSize = new Size(730, 620);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        int optionsLeft = Math.Max(500, form.ClientSize.Width - 235);

        MoveAndSize(FindControl<Button>(form, "B_Dump"), 290, 10, 110, 30);
        MoveAndSize(FindControl<Button>(form, "B_RandAll"), optionsLeft, 10, 135, 30);
        MoveAndSize(FindControl<Button>(form, "B_Metronome"), optionsLeft, 46, 135, 30);

        if (FindControl<DataGridView>(form, "dgv") is { } grid)
        {
            grid.Width = Math.Min(470, optionsLeft - 24);
            grid.Height = Math.Max(520, form.ClientSize.Height - grid.Top - 12);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        }

        if (FindControl<ComboBox>(form, "CB_Species") is { } species)
            species.Width = Math.Max(species.Width, 170);

        if (FindControl<GroupBox>(form, "groupBox1") is { } options)
        {
            MoveAndSize(options, optionsLeft, 82, 225, form.ClientSize.Height - 94);
            options.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;

            MoveAndSize(FindControl<CheckBox>(options, "CHK_HMs"), 16, 28, 170, 22);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_STAB"), 16, 64, 170, 22);
            MoveAndSize(FindControl<Label>(options, "L_STAB"), 40, 91, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_STAB"), 138, 87, 62, 22);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_Expand"), 16, 124, 170, 22);
            MoveAndSize(FindControl<Label>(options, "L_Moves"), 40, 151, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_Moves"), 138, 147, 62, 22);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_4MovesLvl1"), 16, 184, 180, 40);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_Spread"), 16, 244, 170, 22);
            MoveAndSize(FindControl<Label>(options, "L_Scale1"), 40, 272, 120, 18);
            MoveAndSize(FindControl<Label>(options, "L_Scale2"), 40, 294, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_Level"), 138, 290, 62, 22);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_NoFixedDamage"), 16, 338, 195, 82);

            SetDependentEnabled(options, "CHK_STAB", "NUD_STAB", "L_STAB");
            SetDependentEnabled(options, "CHK_Expand", "NUD_Moves", "L_Moves");
            SetDependentEnabled(options, "CHK_Spread", "NUD_Level", "L_Scale1", "L_Scale2");
        }
    }

    private static void FixStarterEditor(Form form)
    {
        EnsureClientSize(form, 900, 410);
        form.MinimumSize = new Size(880, 400);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        // Keep the original starter grid clean. Put randomizer settings in the
        // right-side action area instead of over the Gen 1 middle starter.
        int rightX = Math.Max(520, form.ClientSize.Width - 360);
        int actionY = Math.Max(330, form.ClientSize.Height - 62);

        if (FindControl<GroupBox>(form, "groupBox1") is { } settings)
            MoveAndSize(settings, rightX, actionY - 112, 220, 100);

        MoveAndSize(FindControl<Button>(form, "B_Randomize"), rightX, actionY, 110, 34);
        MoveAndSize(FindControl<Button>(form, "B_Save"), rightX + 125, actionY, 105, 34);
        MoveAndSize(FindControl<Button>(form, "B_Cancel"), rightX + 240, actionY, 105, 34);
    }


    private static void FixEggMoveEditor(Form form)
    {
        EnsureClientSize(form, 650, 430);
        form.MinimumSize = new Size(620, 410);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        MoveAndSize(FindControl<Button>(form, "B_Dump"), 278, 10, 118, 30);
        MoveAndSize(FindControl<Button>(form, "B_RandAll"), 410, 10, 130, 30);
        MoveAndSize(FindControl<Button>(form, "B_Metronome"), 410, 46, 130, 30);
        FindControl<Button>(form, "B_Dump")?.BringToFront();

        int optionsLeft = Math.Max(390, form.ClientSize.Width - 220);
        if (FindControl<DataGridView>(form, "dgv") is { } grid)
        {
            grid.Width = Math.Max(320, optionsLeft - 24);
            grid.Height = Math.Max(310, form.ClientSize.Height - grid.Top - 12);
        }

        if (FindControl<GroupBox>(form, "groupBox1") is { } options)
        {
            MoveAndSize(options, optionsLeft, 84, 205, 178);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_HMs"), 16, 28, 160, 22);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_STAB"), 16, 64, 160, 22);
            MoveAndSize(FindControl<Label>(options, "L_STAB"), 40, 90, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_STAB"), 128, 86, 64, 24);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_Expand"), 16, 122, 160, 22);
            MoveAndSize(FindControl<Label>(options, "L_Moves"), 40, 148, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_Moves"), 128, 144, 64, 24);
        }
    }


    private static void FixGiftEditor(Form form)
    {
        EnsureClientSize(form, 760, 600);
        form.MinimumSize = new Size(720, 570);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        if (FindControl<TabControl>(form, "tabControl1") is { } tabs)
            MoveAndSize(tabs, tabs.Left, tabs.Top, 420, 470);

        if (FindControl<TabPage>(form, "tabPage1") is { } editorTab)
        {
            MoveAndSize(FindControl<CheckBox>(editorTab, "CHK_ShinyLock"), 65, 205, 150, 22);
            if (FindControl<GroupBox>(editorTab, "GB_EIVs") is { } ivs)
            {
                MoveAndSize(ivs, 14, 245, 220, 130);
                MoveAndSize(FindControl<NumericUpDown>(ivs, "NUD_IV0"), 44, 22, 54, 24);
                MoveAndSize(FindControl<NumericUpDown>(ivs, "NUD_IV1"), 44, 56, 54, 24);
                MoveAndSize(FindControl<NumericUpDown>(ivs, "NUD_IV2"), 44, 90, 54, 24);
                MoveAndSize(FindControl<Label>(ivs, "L_SPA"), 112, 20, 34, 21);
                MoveAndSize(FindControl<Label>(ivs, "L_SPD"), 112, 54, 34, 21);
                MoveAndSize(FindControl<Label>(ivs, "L_SPE"), 112, 88, 34, 21);
                MoveAndSize(FindControl<NumericUpDown>(ivs, "NUD_IV3"), 152, 22, 54, 24);
                MoveAndSize(FindControl<NumericUpDown>(ivs, "NUD_IV4"), 152, 56, 54, 24);
                MoveAndSize(FindControl<NumericUpDown>(ivs, "NUD_IV5"), 152, 90, 54, 24);
            }
        }

        if (FindControl<TabPage>(form, "tabPage2") is { } randTab)
        {
            if (FindControl<GroupBox>(randTab, "GB_Tweak") is { } tweak)
                MoveAndSize(tweak, 8, 52, 360, 225);
            MoveAndSize(FindControl<CheckBox>(randTab, "CHK_ReplaceMega"), 8, 300, 350, 22);
            MoveAndSize(FindControl<Label>(randTab, "L_Mega"), 26, 324, 240, 40);
        }
    }


    private static void FixItemEditor(Form form)
    {
        EnsureClientSize(form, 460, 460);
        form.MinimumSize = new Size(460, 460);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        MoveAndSize(FindControl<Button>(form, "B_FixEconomy"), 12, 96, 110, 30);
        MoveAndSize(FindControl<Button>(form, "B_Table"), 130, 96, 110, 30);

        if (FindControl<RichTextBox>(form, "RTB") is { } desc)
            MoveAndSize(desc, 12, 38, form.ClientSize.Width - 24, 52);

        if (FindControl<PropertyGrid>(form, "Grid") is { } grid)
        {
            grid.Location = new Point(12, 132);
            grid.Size = new Size(form.ClientSize.Width - 24, form.ClientSize.Height - 144);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }
    }

    private static void FixTrainerEditor(Form form)
    {
        // Keep labels aligned with the tops of their input boxes. Centering the
        // labels in taller controls made them look lower than the fields.
        MakeTextControlsReadable(form);
        foreach (Label label in FindControls<Label>(form))
        {
            if (label.Text.EndsWith(":", StringComparison.Ordinal))
            {
                label.AutoSize = true;
                label.TextAlign = ContentAlignment.MiddleRight;
                label.BackColor = Color.Transparent;
                label.Location = new Point(Math.Max(0, label.Left - 8), Math.Max(0, label.Top - 6));
            }
        }
    }


    private static void FixShinyRateEditor(Form form)
    {
        EnsureClientSize(form, 470, 320);
        form.MinimumSize = new Size(460, 300);
        form.MaximumSize = Size.Empty;
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        MoveAndSize(FindControl<GroupBox>(form, "GB_Rerolls"), 12, 122, 205, 64);
        MoveAndSize(FindControl<NumericUpDown>(form, "NUD_Rerolls"), 58, 24, 70, 24);
        MoveAndSize(FindControl<Label>(form, "L_Overall"), 136, 27, 62, 18);

        MoveAndSize(FindControl<GroupBox>(form, "GB_RerollHelper"), 235, 122, 205, 64);
        MoveAndSize(FindControl<Label>(form, "L_RerollOverall"), 20, 24, 60, 18);
        MoveAndSize(FindControl<NumericUpDown>(form, "NUD_Rate"), 86, 20, 75, 24);
        MoveAndSize(FindControl<Label>(form, "L_RerollCount"), 86, 44, 90, 18);

        MoveAndSize(FindControl<Label>(form, "label1"), 12, 195, 300, 58);
        MoveAndSize(FindControl<CheckBox>(form, "CHK_EverythingShiny"), 330, 200, 110, 42);
        MoveAndSize(FindControl<Button>(form, "B_RestoreOriginal"), 12, 270, 180, 30);
        MoveAndSize(FindControl<Button>(form, "B_Cancel"), 265, 270, 82, 30);
        MoveAndSize(FindControl<Button>(form, "B_Save"), 358, 270, 82, 30);
    }


    private static void FixGen7TrainerEditor(Form form)
    {
        EnsureClientSize(form, 740, 610);
        form.MinimumSize = new Size(720, 540);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        // Gen 7 Trainer Editor uses very tight fixed pages. Keep labels slightly
        // higher/left, but do not push every label aggressively or FlowLayout rows
        // drift away from their inputs.
        foreach (Label label in FindControls<Label>(form))
        {
            if (label.Text.EndsWith(":", StringComparison.Ordinal))
            {
                label.AutoSize = true;
                label.BackColor = Color.Transparent;
                label.Location = new Point(Math.Max(0, label.Left - 6), Math.Max(0, label.Top - 4));
            }
        }

        if (FindControl<TabControl>(form, "TC_rand") is { } randTabs)
            MoveAndSize(randTabs, 8, 58, 405, 194);
        MoveAndSize(FindControl<Button>(form, "B_Randomize"), 108, 10, 110, 30);
        MoveAndSize(FindControl<Button>(form, "B_Dump"), 232, 10, 118, 30);

        if (FindControl<TabPage>(form, "Tab_PKM2") is { } movesTab)
        {
            movesTab.AutoScroll = true;
            MoveAndSize(FindControl<Label>(movesTab, "L_Moves"), 8, 8, 48, 20);
            MoveAndSize(FindControl<ComboBox>(movesTab, "CB_Moves"), 64, 6, 150, 24);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_ForceHighPower"), 8, 36, 220, 22);
            MoveAndSize(FindControl<NumericUpDown>(movesTab, "NUD_ForceHighPower"), 232, 34, 50, 24);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_Damage"), 8, 64, 220, 22);
            MoveAndSize(FindControl<NumericUpDown>(movesTab, "NUD_Damage"), 232, 62, 50, 24);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_STAB"), 8, 92, 220, 22);
            MoveAndSize(FindControl<NumericUpDown>(movesTab, "NUD_STAB"), 232, 90, 50, 24);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_NoFixedDamage"), 8, 120, 340, 22);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_RandomItems"), 8, 146, 170, 22);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_RandomAbilities"), 8, 168, 230, 22);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_MaxDiffPKM"), 250, 146, 90, 22);
            MoveAndSize(FindControl<CheckBox>(movesTab, "CHK_MaxAI"), 250, 168, 120, 22);
        }

        if (FindControl<TabPage>(form, "Tab_Trainer1") is { } trainerTab)
        {
            trainerTab.AutoScroll = true;
            MoveAndSize(FindControl<CheckBox>(trainerTab, "CHK_ForceFullyEvolved"), 8, 76, 168, 22);
            MoveAndSize(FindControl<NumericUpDown>(trainerTab, "NUD_ForceFullyEvolved"), 180, 74, 54, 24);
            MoveAndSize(FindControl<CheckBox>(trainerTab, "CHK_6PKM"), 8, 104, 235, 22);
            MoveAndSize(FindControl<CheckBox>(trainerTab, "CHK_ReplaceMega"), 8, 128, 255, 22);
        }
    }

    private static void FixMoveEditor7(Form form)
    {
        EnsureClientSize(form, 760, 680);
        form.MinimumSize = new Size(740, 650);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        int right = Math.Max(520, form.ClientSize.Width - 168);
        MoveAndSize(FindControl<Button>(form, "B_RandAll"), right, 10, 140, 32);
        MoveAndSize(FindControl<Button>(form, "B_Metronome"), right, 48, 140, 32);
        MoveAndSize(FindControl<Button>(form, "B_BalanceMoves"), right, 86, 140, 32);
        MoveAndSize(FindControl<Button>(form, "B_Table"), right, 132, 140, 32);
        MoveAndSize(FindControl<GroupBox>(form, "groupBox1"), right, 176, 140, 78);
        MoveAndSize(FindControl<CheckedListBox>(form, "CLB_Flags"), right, 264, 155, Math.Max(310, form.ClientSize.Height - 278));

        // Put Gen 7 Z-Move/Afflict Refresh controls above the stat box so they
        // no longer sit on top of the stat stages.
        MoveAndSize(FindControl<Label>(form, "L_Refresh"), 282, 286, 100, 20);
        MoveAndSize(FindControl<ComboBox>(form, "CB_AfflictRefresh"), 382, 282, 116, 24);
        MoveAndSize(FindControl<Label>(form, "label1"), 282, 314, 70, 20);
        MoveAndSize(FindControl<NumericUpDown>(form, "NUD_RefreshAfflictPercent"), 382, 310, 56, 24);
        MoveAndSize(FindControl<Label>(form, "L_ZMove"), 12, 286, 60, 20);
        MoveAndSize(FindControl<ComboBox>(form, "CB_ZMove"), 74, 282, 160, 24);
        MoveAndSize(FindControl<Label>(form, "L_ZPower"), 12, 314, 60, 20);
        MoveAndSize(FindControl<NumericUpDown>(form, "NUD_ZPower"), 74, 310, 56, 24);
        MoveAndSize(FindControl<Label>(form, "L_ZEffect"), 142, 314, 58, 20);
        MoveAndSize(FindControl<ComboBox>(form, "CB_ZEffect"), 204, 310, 170, 24);

        if (FindControl<GroupBox>(form, "GB_Stat") is { } statBox)
        {
            MoveAndSize(statBox, 12, 352, 360, 142);
            EnsureChildLabel(statBox, "L_StatNameHeader", "Stat", 36, 20);
            EnsureChildLabel(statBox, "L_StatStageHeader", "Stage", 175, 20);
            EnsureChildLabel(statBox, "L_StatChanceHeader", "%", 262, 20);
            MoveAndSize(FindControl<Label>(statBox, "L_Stage1"), 8, 44, 20, 18);
            MoveAndSize(FindControl<Label>(statBox, "L_Stage2"), 8, 74, 20, 18);
            MoveAndSize(FindControl<Label>(statBox, "L_Stage3"), 8, 104, 20, 18);
            MoveAndSize(FindControl<ComboBox>(statBox, "CB_Stat1"), 30, 40, 130, 24);
            MoveAndSize(FindControl<ComboBox>(statBox, "CB_Stat2"), 30, 70, 130, 24);
            MoveAndSize(FindControl<ComboBox>(statBox, "CB_Stat3"), 30, 100, 130, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_Stat1"), 175, 40, 68, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_Stat2"), 175, 70, 68, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_Stat3"), 175, 100, 68, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_StatP1"), 262, 40, 76, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_StatP2"), 262, 70, 76, 24);
            MoveAndSize(FindControl<NumericUpDown>(statBox, "NUD_StatP3"), 262, 100, 76, 24);
        }

        if (FindControl<RichTextBox>(form, "RTB") is { } description)
            MoveAndSize(description, 12, 510, Math.Max(450, right - 24), Math.Max(130, form.ClientSize.Height - 522));
    }

    private static void FixEggMoveEditor7(Form form)
    {
        EnsureClientSize(form, 690, 440);
        form.MinimumSize = new Size(660, 420);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        MakeTextControlsReadable(form);

        MoveAndSize(FindControl<Button>(form, "B_Dump"), 292, 10, 128, 30);
        MoveAndSize(FindControl<Button>(form, "B_RandAll"), 430, 10, 128, 30);
        MoveAndSize(FindControl<Button>(form, "B_Metronome"), 430, 46, 128, 30);
        FindControl<Button>(form, "B_Dump")?.BringToFront();

        // Some Gen7 helper controls sit between Dump and Randomize; give them a
        // visible row instead of leaving them half-hidden under the sprite.
        foreach (var label in FindControls<Label>(form).Where(z => z.Text.Contains("Reference", StringComparison.OrdinalIgnoreCase)))
            MoveAndSize(label, 292, 50, 70, 20);
        foreach (var button in FindControls<Button>(form).Where(z => z.Text.Equals("goto", StringComparison.OrdinalIgnoreCase)))
            MoveAndSize(button, 360, 46, 58, 28);

        if (FindControl<GroupBox>(form, "groupBox1") is { } options)
        {
            MoveAndSize(options, 430, 92, 205, 178);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_HMs"), 16, 28, 160, 22);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_STAB"), 16, 64, 160, 22);
            MoveAndSize(FindControl<Label>(options, "L_STAB"), 40, 90, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_STAB"), 128, 86, 64, 24);
            MoveAndSize(FindControl<CheckBox>(options, "CHK_Expand"), 16, 122, 160, 22);
            MoveAndSize(FindControl<Label>(options, "L_Moves"), 40, 148, 70, 18);
            MoveAndSize(FindControl<NumericUpDown>(options, "NUD_Moves"), 128, 144, 64, 24);
        }
    }

    private static void FixStaticEncounterEditor7(Form form)
    {
        EnsureClientSize(form, 760, 660);
        form.MinimumSize = new Size(720, 620);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        if (FindControl<TabControl>(form, "TC_Tabs") is { } tabs)
            MoveAndSize(tabs, tabs.Left, tabs.Top, 500, 540);

        if (FindControl<TabPage>(form, "Tab_Gifts") is { } gifts)
        {
            MoveAndSize(FindControl<Label>(gifts, "L_GSpecies"), 82, 20, 80, 20);
            MoveAndSize(FindControl<ComboBox>(gifts, "CB_GSpecies"), 168, 16, 190, 24);
            MoveAndSize(FindControl<Label>(gifts, "L_GLevel"), 82, 47, 80, 20);
            MoveAndSize(FindControl<NumericUpDown>(gifts, "NUD_GLevel"), 168, 44, 54, 24);
            MoveAndSize(FindControl<Label>(gifts, "L_GForm"), 82, 74, 80, 20);
            MoveAndSize(FindControl<NumericUpDown>(gifts, "NUD_GForm"), 168, 70, 54, 24);
            MoveAndSize(FindControl<Label>(gifts, "L_GAbility"), 82, 101, 80, 20);
            MoveAndSize(FindControl<ComboBox>(gifts, "CB_GAbility"), 168, 98, 190, 24);
            MoveAndSize(FindControl<Label>(gifts, "L_GHeldItem"), 82, 128, 80, 20);
            MoveAndSize(FindControl<ComboBox>(gifts, "CB_GHeldItem"), 168, 125, 190, 24);
            MoveAndSize(FindControl<Label>(gifts, "L_GNature"), 82, 155, 80, 20);
            MoveAndSize(FindControl<ComboBox>(gifts, "CB_GNature"), 168, 152, 190, 24);
            MoveAndSize(FindControl<Label>(gifts, "L_SpecialMove"), 82, 182, 80, 20);
            MoveAndSize(FindControl<ComboBox>(gifts, "CB_SpecialMove"), 168, 179, 190, 24);
            MoveAndSize(FindControl<CheckBox>(gifts, "CHK_G_Lock"), 190, 220, 120, 22);
            MoveAndSize(FindControl<CheckBox>(gifts, "CHK_GIV3"), 190, 246, 120, 22);
            MoveAndSize(FindControl<CheckBox>(gifts, "CHK_IsEgg"), 190, 272, 120, 22);
        }

        if (FindControl<TabPage>(form, "Tab_Trades") is { } trades)
        {
            foreach (var button in FindControls<Button>(trades))
            {
                if (button.Text.Contains("accept any", StringComparison.OrdinalIgnoreCase))
                    button.Text = "Trades accept any Pokémon";
                if (button.Text.Contains("random offer", StringComparison.OrdinalIgnoreCase))
                    button.Text = "Any request + random offer";
                button.Width = Math.Max(button.Width, 230);
            }
        }

        if (FindControl<TabPage>(form, "Tab_Randomizer") is { } rand)
        {
            if (FindControl<GroupBox>(rand, "GB_Rand") is { } pool)
                MoveAndSize(pool, 12, 62, 360, 210);
            if (FindControl<GroupBox>(rand, "GB_Tweak") is { } tweak)
                MoveAndSize(tweak, 12, 292, 360, 170);
            MoveAndSize(FindControl<CheckBox>(rand, "CHK_ForceFullyEvolved"), 18, 86, 190, 22);
            MoveAndSize(FindControl<NumericUpDown>(rand, "NUD_ForceFullyEvolved"), 220, 84, 54, 24);
            MoveAndSize(FindControl<Button>(rand, "B_RandAll"), 104, 482, 150, 32);
            MoveAndSize(FindControl<Button>(rand, "B_Starters"), 104, 522, 150, 32);
        }
    }

    private static void FixPersonalEditor7(Form form)
    {
        MakeTextControlsReadable(form);
        if (FindControl<CheckBox>(form, "CHK_Regional" ) is { } regional)
            MoveAndSize(regional, 250, 382, 140, 22);
        if (FindControl<CheckBox>(form, "CHK_RegionalVariant") is { } regionalVariant)
            MoveAndSize(regionalVariant, 250, 382, 150, 22);
    }

    private static void FixTMEditor7(Form form)
    {
        EnsureClientSize(form, 340, 440);
        form.MinimumSize = new Size(330, 420);
        MakeTextControlsReadable(form);
        MoveAndSize(FindControl<Button>(form, "B_Rand"), 62, 4, 112, 30);
        MoveAndSize(FindControl<Button>(form, "B_Randomize"), 62, 4, 112, 30);
    }

    private static void FixGen7WildEditor(Form form)
    {
        EnsureClientSize(form, 1420, 760);
        form.MinimumSize = new Size(1280, 720);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MaximizeBox = true;
        form.AutoScroll = true;
        MakeTextControlsReadable(form);

        // Gen7 wild encounters are too dense for the minimal theme. Give the
        // randomizer options a stable left-bottom strip and make the encounter
        // panels breathe instead of overlapping the bottom controls.
        if (FindControl<GroupBox>(form, "GB_Tweak") is { } tweak)
            MoveAndSize(tweak, 28, 540, 360, 205);
        foreach (var group in FindControls<GroupBox>(form))
        {
            if (group.Text.Contains("Extra Randomization", StringComparison.OrdinalIgnoreCase))
                MoveAndSize(group, 28, 540, 360, 205);
        }
    }

    private static void EnsureClientSize(Form form, int width, int height)
    {
        int w = Math.Max(form.ClientSize.Width, width);
        int h = Math.Max(form.ClientSize.Height, height);
        if (w != form.ClientSize.Width || h != form.ClientSize.Height)
            form.ClientSize = new Size(w, h);
    }

    private static void MakeTextControlsReadable(Control root)
    {
        foreach (Control control in EnumerateControls(root))
        {
            switch (control)
            {
                case CheckBox checkBox:
                    checkBox.MaximumSize = Size.Empty;
                    break;
                case RadioButton radioButton:
                    radioButton.MaximumSize = Size.Empty;
                    break;
                case Label label when label.AutoSize:
                    label.MaximumSize = Size.Empty;
                    break;
            }
        }
    }

    private static void SetDependentEnabled(Control root, string checkName, params string[] dependentNames)
    {
        if (FindControl<CheckBox>(root, checkName) is not { } check)
            return;

        void Update()
        {
            foreach (string name in dependentNames)
                if (FindControl<Control>(root, name) is { } dependent)
                    dependent.Enabled = check.Checked;
        }

        check.CheckedChanged -= DependentCheckChanged;
        check.CheckedChanged += DependentCheckChanged;
        Update();

        void DependentCheckChanged(object? sender, EventArgs e) => Update();
    }

    private static Label? FindLabelByText(Control root, string text)
    {
        return FindControls<Label>(root)
            .FirstOrDefault(label => string.Equals(label.Text, text, StringComparison.Ordinal));
    }

    private static Label EnsureChildLabel(Control parent, string name, string text, int x, int y)
    {
        if (FindControl<Label>(parent, name) is { } existing)
        {
            existing.Text = text;
            existing.Location = new Point(x, y);
            existing.AutoSize = true;
            existing.BackColor = Color.Transparent;
            existing.ForeColor = MutedText;
            return existing;
        }

        var label = new Label
        {
            Name = name,
            Text = text,
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = MutedText,
            Location = new Point(x, y),
        };
        parent.Controls.Add(label);
        label.BringToFront();
        return label;
    }

    private static void MoveAndSize(Control? control, int x, int y, int width, int height)
    {
        if (control == null)
            return;
        control.Location = new Point(x, y);
        control.Size = new Size(width, height);
    }

    private static T? FindControl<T>(Control root, string name) where T : Control
    {
        foreach (Control control in EnumerateControls(root))
            if (control is T typed && string.Equals(control.Name, name, StringComparison.Ordinal))
                return typed;
        return null;
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control control in EnumerateControls(root))
            if (control is T typed)
                yield return typed;
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in EnumerateControls(child))
                yield return nested;
        }
    }

    private static void StyleControl(Control control)
    {
        control.Font = UiFont;

        switch (control)
        {
            case Form form:
                form.BackColor = Background;
                form.ForeColor = Text;
                form.StartPosition = form.StartPosition == FormStartPosition.Manual
                    ? FormStartPosition.Manual
                    : FormStartPosition.CenterParent;
                form.MinimumSize = new Size(Math.Min(form.Width, 760), Math.Min(form.Height, 420));
                break;

            case Button button:
                StyleButton(button);
                break;

            case Label label:
                label.ForeColor = label.ForeColor == Color.Red ? Danger : Text;
                label.BackColor = Color.Transparent;
                break;

            case CheckBox checkBox:
                checkBox.ForeColor = Text;
                checkBox.BackColor = Color.Transparent;
                checkBox.FlatStyle = FlatStyle.System;
                break;

            case RadioButton radioButton:
                radioButton.ForeColor = Text;
                radioButton.BackColor = Color.Transparent;
                radioButton.FlatStyle = FlatStyle.System;
                break;

            case GroupBox groupBox:
                groupBox.ForeColor = Text;
                groupBox.BackColor = Background;
                groupBox.Font = UiFontBold;
                break;

            case TextBox textBox:
                textBox.BackColor = textBox.ReadOnly ? SurfaceAlt : Surface;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case RichTextBox richTextBox:
                richTextBox.BackColor = richTextBox.ReadOnly ? SurfaceAlt : Surface;
                richTextBox.ForeColor = Text;
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case CheckedListBox checkedListBox:
                checkedListBox.BackColor = Surface;
                checkedListBox.ForeColor = Text;
                checkedListBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ListBox listBox:
                listBox.BackColor = Surface;
                listBox.ForeColor = Text;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = Surface;
                comboBox.ForeColor = Text;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;

            case NumericUpDown numeric:
                numeric.BackColor = Surface;
                numeric.ForeColor = Text;
                break;

            case DataGridView grid:
                StyleGrid(grid);
                break;

            case TabControl tabControl:
                tabControl.BackColor = Background;
                tabControl.SizeMode = TabSizeMode.Normal;
                tabControl.Appearance = TabAppearance.Normal;
                break;

            case TabPage tabPage:
                tabPage.BackColor = Background;
                tabPage.ForeColor = Text;
                tabPage.Padding = new Padding(10);
                break;

            case FlowLayoutPanel flow:
                flow.BackColor = Background;
                flow.Padding = new Padding(10);
                break;

            case Panel panel:
                panel.BackColor = Background;
                break;

            case MenuStrip menu:
                menu.BackColor = Surface;
                menu.ForeColor = Text;
                menu.Renderer = new ModernToolStripRenderer();
                break;

            case ToolStrip toolStrip:
                toolStrip.BackColor = Surface;
                toolStrip.ForeColor = Text;
                toolStrip.Renderer = new ModernToolStripRenderer();
                break;
        }
    }

    private static void StyleButton(Button button)
    {
        if (button.Text.StartsWith("Add rare", StringComparison.OrdinalIgnoreCase))
        {
            button.Text = "Add Rare Candies";
            button.Width = Math.Max(button.Width, 148);
        }

        bool compactButton = button.Width <= 48 || button.Text.Trim().Length <= 2;

        if (compactButton)
        {
            // Small utility buttons such as "x", browse buttons, and compact
            // editor controls are positioned very tightly in the legacy UI.
            // Do not enlarge them, or they overlap nearby controls.
            button.FlatStyle = FlatStyle.System;
            button.BackColor = SystemColors.Control;
            button.ForeColor = Text;
            button.Font = UiFont;
            button.Padding = Padding.Empty;
            return;
        }

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = AccentBorder;
        button.FlatAppearance.MouseOverBackColor = AccentSoft;
        button.FlatAppearance.MouseDownBackColor = AccentHover;
        button.BackColor = Surface;
        button.ForeColor = Text;
        button.Font = UiFontBold;
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(8, 2, 8, 2);
        if (button.Height < 28)
            button.Height = 28;

        button.MouseEnter += (_, _) =>
        {
            if (button.Enabled)
            {
                button.BackColor = AccentSoft;
                button.FlatAppearance.BorderColor = Accent;
            }
        };
        button.MouseLeave += (_, _) =>
        {
            if (button.Enabled)
            {
                button.BackColor = Surface;
                button.FlatAppearance.BorderColor = AccentBorder;
            }
        };
        button.EnabledChanged += (_, _) =>
        {
            button.BackColor = button.Enabled ? Surface : SurfaceAlt;
            button.ForeColor = button.Enabled ? Text : MutedText;
            button.FlatAppearance.BorderColor = button.Enabled ? AccentBorder : Border;
        };
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = Border;
        grid.RowHeadersVisible = grid.RowHeadersVisible && grid.Width > 500;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = UiFontBold;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = AccentSoft;
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = SurfaceAlt;
    }

    private sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
    {
        public ModernToolStripRenderer() : base(new ModernColorTable()) { }
    }

    private sealed class ModernColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => AccentSoft;
        public override Color MenuItemBorder => Border;
        public override Color MenuBorder => Border;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color ToolStripBorder => Border;
        public override Color ToolStripGradientBegin => Surface;
        public override Color ToolStripGradientMiddle => Surface;
        public override Color ToolStripGradientEnd => Surface;
    }
}
