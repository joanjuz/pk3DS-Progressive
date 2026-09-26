using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms;

/// <summary>
/// Visible progress surface for Batch ROM Builder.
/// Keeps overall ROM progress separate from the currently running operation.
/// </summary>
internal sealed class BatchRomProgressDialog : Form
{
    private readonly int TotalRoms;
    private readonly ProgressBar SourceBuildProgress;

    private readonly Label L_Overall = new();
    private readonly ProgressBar PB_Overall = new();

    private readonly Label L_CurrentRom = new();
    private readonly Label L_Seed = new();
    private readonly Label L_File = new();
    private readonly Label L_Stage = new();
    private readonly ProgressBar PB_Current = new();

    private readonly RichTextBox RTB_Log = new();
    private readonly Label L_CancelInfo = new();
    private readonly Button B_Cancel = new();
    private readonly Timer ProgressMirrorTimer = new();

    private volatile bool CancelWasRequested;
    private bool AllowWindowClose;
    private bool MirrorBuildProgress;

    internal bool CancelRequested => CancelWasRequested;

    internal BatchRomProgressDialog(int totalRoms, ProgressBar sourceBuildProgress)
    {
        TotalRoms = Math.Max(1, totalRoms);
        SourceBuildProgress = sourceBuildProgress;

        Text = "Batch ROM Builder - Progress";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        ClientSize = new Size(720, 500);

        int left = 18;
        int width = ClientSize.Width - 36;
        int y = 16;

        L_Overall.SetBounds(left, y, width, 22);
        L_Overall.Text = $"Overall progress: 0 / {TotalRoms} ROMs complete";
        Controls.Add(L_Overall);

        y += 26;
        PB_Overall.SetBounds(left, y, width, 22);
        PB_Overall.Minimum = 0;
        PB_Overall.Maximum = TotalRoms;
        PB_Overall.Value = 0;
        Controls.Add(PB_Overall);

        y += 38;
        L_CurrentRom.SetBounds(left, y, width, 22);
        L_CurrentRom.Text = $"Current ROM: waiting / {TotalRoms}";
        Controls.Add(L_CurrentRom);

        y += 24;
        L_Seed.SetBounds(left, y, width, 22);
        L_Seed.Text = "Seed: waiting";
        Controls.Add(L_Seed);

        y += 24;
        L_File.SetBounds(left, y, width, 22);
        L_File.Text = "Output: waiting";
        Controls.Add(L_File);

        y += 30;
        L_Stage.SetBounds(left, y, width, 38);
        L_Stage.AutoEllipsis = true;
        L_Stage.Text = "Stage: waiting";
        Controls.Add(L_Stage);

        y += 42;
        PB_Current.SetBounds(left, y, width, 22);
        PB_Current.Style = ProgressBarStyle.Marquee;
        PB_Current.MarqueeAnimationSpeed = 25;
        Controls.Add(PB_Current);

        y += 36;
        var logLabel = new Label
        {
            Text = "Live log",
            AutoSize = true,
            Left = left,
            Top = y,
        };
        Controls.Add(logLabel);

        y += 22;
        RTB_Log.SetBounds(left, y, width, 190);
        RTB_Log.ReadOnly = true;
        RTB_Log.WordWrap = false;
        RTB_Log.DetectUrls = false;
        RTB_Log.BackColor = SystemColors.Window;
        Controls.Add(RTB_Log);

        y += 200;
        L_CancelInfo.SetBounds(left, y + 5, 500, 40);
        L_CancelInfo.Text = "Cancel stops safely between operations. A .3DS rebuild already in progress is allowed to finish.";
        Controls.Add(L_CancelInfo);

        B_Cancel.Text = "Cancel";
        B_Cancel.SetBounds(ClientSize.Width - 118, y, 100, 30);
        B_Cancel.Click += (_, _) => RequestCancel();
        Controls.Add(B_Cancel);

        ProgressMirrorTimer.Interval = 150;
        ProgressMirrorTimer.Tick += (_, _) => MirrorSourceProgress();
        ProgressMirrorTimer.Start();
    }

    internal void BeginRom(int index, int total, int seed, string outputFile)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            SetCurrentIndeterminate();

            PB_Overall.Maximum = Math.Max(1, total);
            PB_Overall.Value = Math.Clamp(index - 1, 0, PB_Overall.Maximum);

            L_Overall.Text = $"Overall progress: {index - 1} / {total} ROMs complete";
            L_CurrentRom.Text = $"Current ROM: {index} / {total}";
            L_Seed.Text = $"Seed: {seed}";
            L_File.Text = $"Output: {outputFile}";
            L_Stage.Text = "Stage: preparing";
            AppendLogCore($"--- ROM {index}/{total}: {outputFile} ---");
            AppendLogCore($"Seed = {seed}");
        });
    }

    internal void SetStage(string stage)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            SetCurrentIndeterminate();
            L_Stage.Text = $"Stage: {stage}";
        });
    }

    internal void ReportAction(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            SetCurrentIndeterminate();
            L_Stage.Text = $"Stage: {message}";
            AppendLogCore(message);
        });
    }

    internal void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        RunOnUi(() => AppendLogCore(message));
    }

    internal void BeginRebuild(string outputFile)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = true;
            PB_Current.Style = ProgressBarStyle.Blocks;
            PB_Current.MarqueeAnimationSpeed = 0;
            PB_Current.Minimum = 0;
            PB_Current.Maximum = 100;
            PB_Current.Value = 0;
            L_Stage.Text = $"Stage: rebuilding {outputFile}";
            AppendLogCore($"Rebuilding {outputFile}...");
        });
    }

    internal void EndRebuild()
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            PB_Current.Style = ProgressBarStyle.Blocks;
            PB_Current.MarqueeAnimationSpeed = 0;
            PB_Current.Minimum = 0;
            PB_Current.Maximum = 100;
            PB_Current.Value = 100;
            AppendLogCore("ROM rebuild finished.");
        });
    }

    internal void CompleteRom(int index, string outputFile)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            PB_Overall.Value = Math.Clamp(index, 0, PB_Overall.Maximum);
            L_Overall.Text = $"Overall progress: {index} / {TotalRoms} ROMs complete";
            L_Stage.Text = $"Stage: complete - {outputFile}";
            PB_Current.Style = ProgressBarStyle.Blocks;
            PB_Current.MarqueeAnimationSpeed = 0;
            PB_Current.Minimum = 0;
            PB_Current.Maximum = 100;
            PB_Current.Value = 100;
            AppendLogCore($"Complete: {outputFile}");
        });
    }

    internal void MarkCompleted(int completed)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            PB_Overall.Value = Math.Clamp(completed, 0, PB_Overall.Maximum);
            L_Overall.Text = $"Overall progress: {completed} / {TotalRoms} ROMs complete";
            L_Stage.Text = "Stage: batch complete";
            B_Cancel.Enabled = false;
            AppendLogCore("Batch completed successfully.");
        });
    }

    internal void MarkCancelled(int completed, int total)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            PB_Overall.Value = Math.Clamp(completed, 0, PB_Overall.Maximum);
            L_Overall.Text = $"Overall progress: {completed} / {total} ROMs complete";
            L_Stage.Text = "Stage: cancelled";
            B_Cancel.Enabled = false;
            L_CancelInfo.Text = "Batch cancelled. Completed ROMs were kept.";
            AppendLogCore("Batch cancelled by user.");
        });
    }

    internal void MarkFailed(string message)
    {
        RunOnUi(() =>
        {
            MirrorBuildProgress = false;
            L_Stage.Text = "Stage: FAILED";
            B_Cancel.Enabled = false;
            L_CancelInfo.Text = message ?? "Batch build failed.";
            AppendLogCore("FAILED: " + (message ?? "Unknown error."));
        });
    }

    internal void AllowClose()
    {
        AllowWindowClose = true;
        ProgressMirrorTimer.Stop();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowWindowClose && e.CloseReason == CloseReason.UserClosing)
        {
            RequestCancel();
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private void RequestCancel()
    {
        if (CancelWasRequested)
            return;

        CancelWasRequested = true;
        B_Cancel.Enabled = false;
        B_Cancel.Text = "Cancel requested";
        L_CancelInfo.Text = "Cancel requested. pk3DS will stop at the next safe boundary; an active .3DS rebuild will finish first.";
        AppendLogCore("Cancel requested by user.");
    }

    private void SetCurrentIndeterminate()
    {
        PB_Current.Style = ProgressBarStyle.Marquee;
        PB_Current.MarqueeAnimationSpeed = 25;
    }

    private void MirrorSourceProgress()
    {
        if (!MirrorBuildProgress || SourceBuildProgress is null || SourceBuildProgress.IsDisposed)
            return;

        try
        {
            int sourceMin = SourceBuildProgress.Minimum;
            int sourceMax = SourceBuildProgress.Maximum;
            int sourceValue = SourceBuildProgress.Value;

            if (sourceMax <= sourceMin)
                return;

            PB_Current.Style = ProgressBarStyle.Blocks;
            PB_Current.MarqueeAnimationSpeed = 0;
            PB_Current.Minimum = sourceMin;
            PB_Current.Maximum = sourceMax;
            PB_Current.Value = Math.Clamp(sourceValue, sourceMin, sourceMax);
        }
        catch
        {
            // Progress feedback must never be able to abort a ROM build.
        }
    }

    private void AppendLogCore(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        RTB_Log.AppendText(line + Environment.NewLine);
        RTB_Log.SelectionStart = RTB_Log.TextLength;
        RTB_Log.ScrollToCaret();
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }
}
