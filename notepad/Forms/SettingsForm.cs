using System;
using System.Drawing;
using System.Windows.Forms;
using GBZ80AsmMetrics.Core;

namespace GBZ80AsmMetrics.Forms
{
    /// <summary>
    /// Settings configuration dialog
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly SettingsManager _settings;

        // Controls
        private CheckBox _chkShowByteCount;
        private CheckBox _chkShowCycleCount;
        private CheckBox _chkShowCumulative;
        private CheckBox _chkAssumeBranchTaken;
        private NumericUpDown _numPredefBytes;
        private NumericUpDown _numPredefCycles;
        private NumericUpDown _numPredefJumpBytes;
        private NumericUpDown _numPredefJumpCycles;
        private Button _btnOK;
        private Button _btnCancel;
        private Button _btnReset;

        public SettingsForm(SettingsManager settings)
        {
            _settings = settings;
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "GB Z80 ASM Metrics - Settings";
            this.Size = new Size(400, 450);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);

            // Display Options Group
            var grpDisplay = new GroupBox
            {
                Text = "Display Options",
                Location = new Point(12, 12),
                Size = new Size(360, 120)
            };

            _chkShowByteCount = new CheckBox
            {
                Text = "Show byte count",
                Location = new Point(12, 25),
                AutoSize = true
            };

            _chkShowCycleCount = new CheckBox
            {
                Text = "Show cycle count",
                Location = new Point(12, 50),
                AutoSize = true
            };

            _chkShowCumulative = new CheckBox
            {
                Text = "Show cumulative totals",
                Location = new Point(12, 75),
                AutoSize = true
            };

            grpDisplay.Controls.AddRange(new Control[] { _chkShowByteCount, _chkShowCycleCount, _chkShowCumulative });

            // Calculation Options Group
            var grpCalc = new GroupBox
            {
                Text = "Calculation Options",
                Location = new Point(12, 140),
                Size = new Size(360, 60)
            };

            _chkAssumeBranchTaken = new CheckBox
            {
                Text = "Assume branches are taken (use higher cycle count)",
                Location = new Point(12, 25),
                AutoSize = true
            };

            grpCalc.Controls.Add(_chkAssumeBranchTaken);

            // PREDEF Settings Group
            var grpPredef = new GroupBox
            {
                Text = "PREDEF/FARCALL Settings",
                Location = new Point(12, 208),
                Size = new Size(360, 140)
            };

            var lblPredefBytes = new Label { Text = "PREDEF bytes:", Location = new Point(12, 28), AutoSize = true };
            _numPredefBytes = new NumericUpDown { Location = new Point(150, 25), Size = new Size(60, 23), Minimum = 1, Maximum = 100 };

            var lblPredefCycles = new Label { Text = "PREDEF cycles:", Location = new Point(12, 58), AutoSize = true };
            _numPredefCycles = new NumericUpDown { Location = new Point(150, 55), Size = new Size(60, 23), Minimum = 1, Maximum = 500 };

            var lblPredefJumpBytes = new Label { Text = "PREDEF_JUMP bytes:", Location = new Point(12, 88), AutoSize = true };
            _numPredefJumpBytes = new NumericUpDown { Location = new Point(150, 85), Size = new Size(60, 23), Minimum = 1, Maximum = 100 };

            var lblPredefJumpCycles = new Label { Text = "PREDEF_JUMP cycles:", Location = new Point(12, 118), AutoSize = true };
            _numPredefJumpCycles = new NumericUpDown { Location = new Point(150, 115), Size = new Size(60, 23), Minimum = 1, Maximum = 500 };

            // Help text
            var lblHelp = new Label
            {
                Text = "Default: 8B/44c (ld a + ld hl + call)\nJump: 8B/36c (ld a + ld hl + jp)",
                Location = new Point(220, 28),
                Size = new Size(130, 80),
                ForeColor = SystemColors.GrayText
            };

            grpPredef.Controls.AddRange(new Control[]
            {
                lblPredefBytes, _numPredefBytes,
                lblPredefCycles, _numPredefCycles,
                lblPredefJumpBytes, _numPredefJumpBytes,
                lblPredefJumpCycles, _numPredefJumpCycles,
                lblHelp
            });

            // Buttons
            _btnOK = new Button
            {
                Text = "OK",
                Location = new Point(200, 365),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            _btnOK.Click += BtnOK_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(290, 365),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            _btnReset = new Button
            {
                Text = "Reset Defaults",
                Location = new Point(12, 365),
                Size = new Size(100, 30)
            };
            _btnReset.Click += BtnReset_Click;

            this.Controls.AddRange(new Control[] { grpDisplay, grpCalc, grpPredef, _btnOK, _btnCancel, _btnReset });
            this.AcceptButton = _btnOK;
            this.CancelButton = _btnCancel;
        }

        private void LoadSettings()
        {
            _chkShowByteCount.Checked = _settings.ShowByteCount;
            _chkShowCycleCount.Checked = _settings.ShowCycleCount;
            _chkShowCumulative.Checked = _settings.ShowCumulative;
            _chkAssumeBranchTaken.Checked = _settings.AssumeBranchTaken;
            _numPredefBytes.Value = _settings.PredefBytes;
            _numPredefCycles.Value = _settings.PredefCycles;
            _numPredefJumpBytes.Value = _settings.PredefJumpBytes;
            _numPredefJumpCycles.Value = _settings.PredefJumpCycles;
        }

        private void SaveSettings()
        {
            _settings.ShowByteCount = _chkShowByteCount.Checked;
            _settings.ShowCycleCount = _chkShowCycleCount.Checked;
            _settings.ShowCumulative = _chkShowCumulative.Checked;
            _settings.AssumeBranchTaken = _chkAssumeBranchTaken.Checked;
            _settings.PredefBytes = (int)_numPredefBytes.Value;
            _settings.PredefCycles = (int)_numPredefCycles.Value;
            _settings.PredefJumpBytes = (int)_numPredefJumpBytes.Value;
            _settings.PredefJumpCycles = (int)_numPredefJumpCycles.Value;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            _chkShowByteCount.Checked = true;
            _chkShowCycleCount.Checked = true;
            _chkShowCumulative.Checked = true;
            _chkAssumeBranchTaken.Checked = true;
            _numPredefBytes.Value = 8;
            _numPredefCycles.Value = 44;
            _numPredefJumpBytes.Value = 8;
            _numPredefJumpCycles.Value = 36;
        }
    }
}
