using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GBZ80AsmMetrics.Core;

namespace GBZ80AsmMetrics.Forms
{
    /// <summary>
    /// Dialog showing detailed information about the current instruction
    /// </summary>
    public class LineInfoForm : Form
    {
        private readonly LineInfo _lineInfo;

        public LineInfoForm(LineInfo lineInfo)
        {
            _lineInfo = lineInfo;
            InitializeComponent();
            PopulateInfo();
        }

        private void InitializeComponent()
        {
            this.Text = "Instruction Details";
            this.Size = new Size(450, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);
        }

        private void PopulateInfo()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(15)
            };

            int y = 15;

            // Title
            string title = GetTitle();
            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(15, y),
                Font = new Font("Consolas", 14F, FontStyle.Bold),
                AutoSize = true
            };
            panel.Controls.Add(lblTitle);
            y += 35;

            // Separator
            y = AddSeparator(panel, y);

            // Basic metrics
            y = AddLabelPair(panel, "Size:", $"{_lineInfo.Bytes} byte(s)", y);
            y = AddLabelPair(panel, "Cycles:", GetCyclesText(), y);

            if (_lineInfo.CumulativeBytes > 0 || _lineInfo.CumulativeCycles > 0)
            {
                y = AddLabelPair(panel, "Cumulative:", $"{_lineInfo.CumulativeBytes}B / {_lineInfo.CumulativeCycles}c", y);
            }

            // Opcode specific info
            if (_lineInfo.Opcode != null)
            {
                y = AddSeparator(panel, y);
                y = AddLabelPair(panel, "Opcode:", _lineInfo.OpcodeHex, y);

                // Flags table
                y += 15;
                y = AddFlagsTable(panel, y);
            }

            // Macro specific info
            if (_lineInfo.IsMacroCall)
            {
                y = AddSeparator(panel, y);
                y = AddLabelPair(panel, "Macro:", _lineInfo.MacroName, y);
                y = AddLabelPair(panel, "Cycle range:", $"{_lineInfo.MacroCyclesMax}c max / {_lineInfo.MacroCyclesMin}c min", y);
                y = AddLabelPair(panel, "Instructions:", _lineInfo.MacroInstructionCount.ToString(), y);
            }

            // Predef specific info
            if (_lineInfo.IsPredefCall)
            {
                y = AddSeparator(panel, y);
                y = AddLabelPair(panel, "Type:", _lineInfo.PredefTypeLabel, y);
                y = AddLabelPair(panel, "Function:", _lineInfo.PredefName, y);
                y = AddLabelPair(panel, "Composition:", GetPredefComposition(), y);
            }

            // OK button
            var btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(this.ClientSize.Width - 95, this.ClientSize.Height - 45),
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.Add(panel);
            this.Controls.Add(btnOK);
            this.AcceptButton = btnOK;
        }

        private string GetTitle()
        {
            if (_lineInfo.IsMacroCall)
            {
                return $"MACRO: {_lineInfo.MacroName}";
            }
            if (_lineInfo.IsPredefCall)
            {
                return $"{_lineInfo.PredefTypeLabel} {_lineInfo.PredefName}";
            }
            if (_lineInfo.ParsedLine?.Instruction != null)
            {
                var operands = _lineInfo.ParsedLine.Operands;
                string operandStr = operands.Count > 0 ? " " + string.Join(", ", operands) : "";
                return $"{_lineInfo.ParsedLine.Instruction}{operandStr}";
            }
            if (_lineInfo.IsDataDirective)
            {
                return $"Data Directive";
            }
            return "Unknown";
        }

        private string GetCyclesText()
        {
            if (_lineInfo.Opcode != null && _lineInfo.Opcode.Cycles.Length > 1)
            {
                return $"{_lineInfo.Opcode.Cycles[0]} (taken) / {_lineInfo.Opcode.Cycles[1]} (not taken)";
            }
            if (_lineInfo.IsMacroCall && _lineInfo.MacroCyclesMin != _lineInfo.MacroCyclesMax)
            {
                return $"{_lineInfo.MacroCyclesMax} max / {_lineInfo.MacroCyclesMin} min";
            }
            return $"{_lineInfo.Cycles}";
        }

        private string GetPredefComposition()
        {
            if (_lineInfo.PredefTypeLabel == "PREDEF_JUMP")
            {
                return "ld a,BANK + ld hl,addr + jp";
            }
            return "ld a,BANK + ld hl,addr + call";
        }

        private int AddLabelPair(Panel panel, string label, string value, int y)
        {
            var lblKey = new Label
            {
                Text = label,
                Location = new Point(15, y),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = value,
                Location = new Point(120, y),
                Font = new Font("Consolas", 9F),
                AutoSize = true
            };

            panel.Controls.Add(lblKey);
            panel.Controls.Add(lblValue);

            return y + 25;
        }

        private int AddSeparator(Panel panel, int y)
        {
            var sep = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(15, y + 5),
                Size = new Size(panel.ClientSize.Width - 50, 2)
            };
            panel.Controls.Add(sep);
            return y + 20;
        }

        private int AddFlagsTable(Panel panel, int y)
        {
            if (_lineInfo.Opcode?.Flags == null) return y;

            var flags = _lineInfo.Opcode.Flags;

            // Header
            var header = new Label
            {
                Text = "Flags Affected:",
                Location = new Point(15, y),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panel.Controls.Add(header);
            y += 25;

            // Table header
            string[] headers = { "Z", "N", "H", "C" };
            string[] values = { flags.Z, flags.N, flags.H, flags.C };
            int cellWidth = 50;
            int startX = 60;

            for (int i = 0; i < 4; i++)
            {
                var lblHeader = new Label
                {
                    Text = headers[i],
                    Location = new Point(startX + i * cellWidth, y),
                    Size = new Size(cellWidth, 20),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = SystemColors.ControlLight
                };
                panel.Controls.Add(lblHeader);
            }
            y += 20;

            for (int i = 0; i < 4; i++)
            {
                var lblValue = new Label
                {
                    Text = values[i],
                    Location = new Point(startX + i * cellWidth, y),
                    Size = new Size(cellWidth, 25),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Consolas", 10F),
                    BorderStyle = BorderStyle.FixedSingle
                };
                panel.Controls.Add(lblValue);
            }
            y += 35;

            // Legend
            var legend = new Label
            {
                Text = "- = unchanged, 0 = reset, 1 = set, letter = affected",
                Location = new Point(15, y),
                ForeColor = SystemColors.GrayText,
                AutoSize = true
            };
            panel.Controls.Add(legend);

            return y + 25;
        }
    }
}
