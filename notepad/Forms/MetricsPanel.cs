using System;
using System.Drawing;
using System.Windows.Forms;
using GBZ80AsmMetrics.Core;

namespace GBZ80AsmMetrics.Forms
{
    /// <summary>
    /// Dockable panel showing metrics for the current line
    /// </summary>
    public class MetricsPanel : Form
    {
        private Label _lblLine;
        private Label _lblInstruction;
        private Label _lblBytes;
        private Label _lblCycles;
        private Label _lblCumulativeBytes;
        private Label _lblCumulativeCycles;
        private Label _lblOpcode;
        private Label _lblFlags;
        private Label _lblType;
        private Label _lblArguments;
        private TableLayoutPanel _table;

        public MetricsPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "GB Z80 Metrics";
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            this.MinimumSize = new Size(200, 320);
            this.Size = new Size(280, 380);
            this.Font = new Font("Segoe UI", 9F);

            _table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 15,
                AutoScroll = true,
                Padding = new Padding(8),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            // Set column widths
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Set row heights to auto
            for (int i = 0; i < 15; i++)
            {
                _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            int row = 0;

            // Line number
            _table.Controls.Add(CreateTitleLabel("Line:"), 0, row);
            _lblLine = CreateValueLabel("-");
            _table.Controls.Add(_lblLine, 1, row);
            row++;

            // Instruction
            _table.Controls.Add(CreateTitleLabel("Instruction:"), 0, row);
            _lblInstruction = CreateValueLabel("-");
            _lblInstruction.Font = new Font("Consolas", 10F, FontStyle.Bold);
            _table.Controls.Add(_lblInstruction, 1, row);
            row++;

            // Separator
            _table.Controls.Add(CreateSeparator(), 0, row);
            _table.SetColumnSpan(_table.GetControlFromPosition(0, row), 2);
            row++;

            // Bytes
            _table.Controls.Add(CreateTitleLabel("Bytes:"), 0, row);
            _lblBytes = CreateValueLabel("-");
            _table.Controls.Add(_lblBytes, 1, row);
            row++;

            // Cumulative Bytes
            _table.Controls.Add(CreateTitleLabel("Σ Bytes:"), 0, row);
            _lblCumulativeBytes = CreateValueLabel("-");
            _table.Controls.Add(_lblCumulativeBytes, 1, row);
            row++;

            // Cycles
            _table.Controls.Add(CreateTitleLabel("Cycles:"), 0, row);
            _lblCycles = CreateValueLabel("-");
            _table.Controls.Add(_lblCycles, 1, row);
            row++;

            // Cumulative Cycles
            _table.Controls.Add(CreateTitleLabel("Σ Cycles:"), 0, row);
            _lblCumulativeCycles = CreateValueLabel("-");
            _table.Controls.Add(_lblCumulativeCycles, 1, row);
            row++;

            // Separator
            _table.Controls.Add(CreateSeparator(), 0, row);
            _table.SetColumnSpan(_table.GetControlFromPosition(0, row), 2);
            row++;

            // Type
            _table.Controls.Add(CreateTitleLabel("Type:"), 0, row);
            _lblType = CreateValueLabel("-");
            _table.Controls.Add(_lblType, 1, row);
            row++;

            // Opcode
            _table.Controls.Add(CreateTitleLabel("Opcode:"), 0, row);
            _lblOpcode = CreateValueLabel("-");
            _lblOpcode.Font = new Font("Consolas", 9F);
            _table.Controls.Add(_lblOpcode, 1, row);
            row++;

            // Flags
            _table.Controls.Add(CreateTitleLabel("Flags:"), 0, row);
            _lblFlags = CreateValueLabel("-");
            _lblFlags.Font = new Font("Consolas", 9F);
            _table.Controls.Add(_lblFlags, 1, row);
            row++;

            // Separator
            _table.Controls.Add(CreateSeparator(), 0, row);
            _table.SetColumnSpan(_table.GetControlFromPosition(0, row), 2);
            row++;

            // Arguments (for call/jump instructions to documented routines)
            // Use full row for arguments since they can be multi-line
            var argsTitle = CreateTitleLabel("Arguments:");
            _table.Controls.Add(argsTitle, 0, row);
            _table.SetColumnSpan(argsTitle, 2);
            row++;

            _lblArguments = CreateValueLabel("-");
            _lblArguments.Font = new Font("Consolas", 9F);
            _lblArguments.AutoSize = true;
            _lblArguments.MaximumSize = new Size(0, 0); // No max size limit
            _lblArguments.Dock = DockStyle.Fill;
            _table.Controls.Add(_lblArguments, 0, row);
            _table.SetColumnSpan(_lblArguments, 2);

            this.Controls.Add(_table);
        }

        private Label CreateTitleLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 6, 10, 6),
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(3, 6, 3, 6)
            };
        }

        private Panel CreateSeparator()
        {
            return new Panel
            {
                Height = 2,
                Dock = DockStyle.Top,
                BackColor = SystemColors.ControlDark,
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        /// <summary>
        /// Update the panel display with information from the specified line
        /// </summary>
        public void UpdateDisplay(LineInfo info, int lineNumber)
        {
            _lblLine.Text = (lineNumber + 1).ToString();

            if (info == null || !info.HasMetrics)
            {
                ClearDisplay();
                return;
            }

            // Instruction
            if (info.ParsedLine?.Instruction != null)
            {
                var operands = info.ParsedLine.Operands;
                string operandStr = operands.Count > 0 ? " " + string.Join(", ", operands) : "";
                _lblInstruction.Text = info.ParsedLine.Instruction + operandStr;
            }
            else
            {
                _lblInstruction.Text = "-";
            }

            // Bytes and Cycles
            _lblBytes.Text = $"{info.Bytes}B";
            _lblCycles.Text = GetCyclesText(info);

            // Cumulative Bytes
            if (info.CumulativeBytes > 0)
            {
                _lblCumulativeBytes.Text = $"{info.CumulativeBytes}B";
            }
            else
            {
                _lblCumulativeBytes.Text = "-";
            }

            // Cumulative Cycles
            if (info.CumulativeCycles > 0)
            {
                _lblCumulativeCycles.Text = $"{info.CumulativeCycles}c";
            }
            else
            {
                _lblCumulativeCycles.Text = "-";
            }

            // Type
            if (info.IsMacroCall)
            {
                _lblType.Text = $"Macro ({info.MacroName})";
            }
            else if (info.IsPredefCall)
            {
                _lblType.Text = info.PredefTypeLabel;
            }
            else if (info.IsDataDirective)
            {
                _lblType.Text = "Data Directive";
            }
            else
            {
                _lblType.Text = "Instruction";
            }

            // Opcode
            if (info.OpcodeHex != null)
            {
                _lblOpcode.Text = info.OpcodeHex;
            }
            else
            {
                _lblOpcode.Text = "-";
            }

            // Flags
            if (info.Opcode?.Flags != null)
            {
                var f = info.Opcode.Flags;
                _lblFlags.Text = $"Z:{f.Z} N:{f.N} H:{f.H} C:{f.C}";
            }
            else
            {
                _lblFlags.Text = "-";
            }

            // Arguments (for call/jump instructions to documented routines)
            if (info.TargetRoutine != null)
            {
                var routine = info.TargetRoutine;
                var parts = new System.Collections.Generic.List<string>();

                if (routine.Description != null)
                {
                    parts.Add(routine.Description);
                }

                foreach (var arg in routine.Arguments)
                {
                    parts.Add($"{arg.Register.ToUpperInvariant()}: {arg.Description}");
                }

                _lblArguments.Text = string.Join("\n", parts);
            }
            else
            {
                _lblArguments.Text = "-";
            }
        }

        private void ClearDisplay()
        {
            _lblInstruction.Text = "-";
            _lblBytes.Text = "-";
            _lblCycles.Text = "-";
            _lblCumulativeBytes.Text = "-";
            _lblCumulativeCycles.Text = "-";
            _lblType.Text = "-";
            _lblOpcode.Text = "-";
            _lblFlags.Text = "-";
            _lblArguments.Text = "-";
        }

        private string GetCyclesText(LineInfo info)
        {
            if (info.Opcode != null && info.Opcode.Cycles.Length > 1)
            {
                return $"{info.Opcode.Cycles[0]}/{info.Opcode.Cycles[1]}c";
            }
            if (info.IsMacroCall && info.MacroCyclesMin != info.MacroCyclesMax)
            {
                return $"{info.MacroCyclesMax}/{info.MacroCyclesMin}c";
            }
            return $"{info.Cycles}c";
        }

        protected override void WndProc(ref Message m)
        {
            // Handle close button to just hide instead of close
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;

            if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_CLOSE)
            {
                this.Hide();
                return;
            }

            base.WndProc(ref m);
        }
    }
}
