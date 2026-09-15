using System;
using System.Drawing;
using System.Windows.Forms;

namespace pk3DS.WinForms;

internal sealed class TrainerRandomizeProgress : IDisposable
{
    private readonly Form form;
    private readonly ProgressBar progressBar;
    private readonly Label label;
    private int lastValue = -1;

    private TrainerRandomizeProgress(IWin32Window owner, string title, int maximum)
    {
        progressBar = new ProgressBar
        {
            Left = 12,
            Top = 42,
            Width = 420,
            Height = 22,
            Minimum = 0,
            Maximum = Math.Max(1, maximum),
            Value = 0,
            Style = ProgressBarStyle.Continuous,
        };

        label = new Label
        {
            Left = 12,
            Top = 14,
            Width = 420,
            Height = 18,
            Text = "Preparing...",
        };

        form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(444, 78),
            ControlBox = false,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
        };

        form.Controls.Add(label);
        form.Controls.Add(progressBar);

        if (BatchRuntime.IsActive)
            return;

        form.Show(owner);
        form.Refresh();
        Application.DoEvents();
    }

    public static TrainerRandomizeProgress Show(IWin32Window owner, string title, int maximum)
        => new(owner, title, maximum);

    public void Report(int current, int total, string trainerName)
    {
        total = Math.Max(1, total);
        current = Math.Clamp(current, 0, total);

        if (current == lastValue)
            return;

        lastValue = current;
        if (BatchRuntime.IsActive)
        {
            if (current == total || current == 1 || current % 50 == 0)
            {
                string name = string.IsNullOrWhiteSpace(trainerName) ? string.Empty : $" ({trainerName})";
                BatchRuntime.Log($"Trainers: {current}/{total}{name}");
            }
            return;
        }

        progressBar.Maximum = total;
        progressBar.Value = Math.Min(current, progressBar.Maximum);
        label.Text = string.IsNullOrWhiteSpace(trainerName)
            ? $"Randomizing trainers... {current}/{total}"
            : $"Randomizing {trainerName}... {current}/{total}";

        form.Refresh();
        Application.DoEvents();
    }

    public void Dispose()
    {
        if (!BatchRuntime.IsActive)
            form.Close();
        form.Dispose();
    }
}
