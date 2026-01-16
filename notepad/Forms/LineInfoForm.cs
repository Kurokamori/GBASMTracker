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
        private TableLayoutPanel _table;

        public LineInfoForm(LineInfo lineInfo)
        {
            _lineInfo = lineInfo;
            InitializeComponent();
            PopulateInfo();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // LineInfoForm
            // 
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(350, 500);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(500, 600);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(320, 200);
            this.Name = "LineInfoForm";
            this.Padding = new System.Windows.Forms.Padding(0, 0, 0, 50);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Instruction Details";
            this.Load += new System.EventHandler(this.LineInfoForm_Load);
            this.ResumeLayout(false);

        }

        private void PopulateInfo()
        {
            _table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            // Set column widths
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;

            // Title (instruction name)
            string title = GetTitle();
            var lblTitle = CreateValueLabel(title);
            lblTitle.Font = new Font("Consolas", 12F, FontStyle.Bold);
            lblTitle.Margin = new Padding(3, 6, 3, 12);
            _table.Controls.Add(lblTitle, 0, row);
            _table.SetColumnSpan(lblTitle, 2);
            row++;

            // Separator
            row = AddSeparator(row);

            // Type
            string typeText = GetTypeText();
            row = AddRow("Type:", typeText, row);

            // Bytes
            row = AddRow("Bytes:", $"{_lineInfo.Bytes}B", row);

            // Cycles
            row = AddRow("Cycles:", GetCyclesText(), row);

            // Cumulative (if available)
            if (_lineInfo.CumulativeBytes > 0 || _lineInfo.CumulativeCycles > 0)
            {
                row = AddRow("Σ Bytes:", $"{_lineInfo.CumulativeBytes}B", row);
                row = AddRow("Σ Cycles:", $"{_lineInfo.CumulativeCycles}c", row);
            }

            // Opcode specific info
            if (_lineInfo.Opcode != null)
            {
                row = AddSeparator(row);
                row = AddRow("Opcode:", _lineInfo.OpcodeHex, row);

                // Flags
                if (_lineInfo.Opcode.Flags != null)
                {
                    var f = _lineInfo.Opcode.Flags;
                    row = AddRow("Flags:", $"Z:{f.Z}  N:{f.N}  H:{f.H}  C:{f.C}", row);
                }
            }

            // Macro specific info
            if (_lineInfo.IsMacroCall)
            {
                row = AddSeparator(row);
                row = AddRow("Macro:", _lineInfo.MacroName, row);
                row = AddRow("Cycle Range:", $"{_lineInfo.MacroCyclesMin}c - {_lineInfo.MacroCyclesMax}c", row);
                row = AddRow("Instructions:", _lineInfo.MacroInstructionCount.ToString(), row);
            }

            // Predef specific info
            if (_lineInfo.IsPredefCall)
            {
                row = AddSeparator(row);
                row = AddRow("Predef Type:", _lineInfo.PredefTypeLabel, row);
                row = AddRow("Function:", _lineInfo.PredefName, row);
                row = AddRow("Composition:", GetPredefComposition(), row);
            }

            // Arguments (for call/jump instructions to documented routines)
            if (_lineInfo.TargetRoutine != null)
            {
                row = AddSeparator(row);

                var routine = _lineInfo.TargetRoutine;
                var parts = new System.Collections.Generic.List<string>();

                if (routine.Description != null)
                {
                    parts.Add(routine.Description);
                }

                foreach (var arg in routine.Arguments)
                {
                    parts.Add($"{arg.Register.ToUpperInvariant()}: {arg.Description}");
                }

                string argsText = string.Join("\n", parts);

                var argsTitle = CreateTitleLabel("Arguments:");
                _table.Controls.Add(argsTitle, 0, row);
                _table.SetColumnSpan(argsTitle, 2);
                row++;

                var argsValue = CreateValueLabel(argsText);
                argsValue.Font = new Font("Consolas", 9F);
                argsValue.Margin = new Padding(12, 3, 3, 6);
                _table.Controls.Add(argsValue, 0, row);
                _table.SetColumnSpan(argsValue, 2);
                row++;
            }

            this.Controls.Add(_table);

            // OK button
            var btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnOK.Location = new Point(this.ClientSize.Width - 95, this.ClientSize.Height - 45);
            this.Controls.Add(btnOK);
            this.AcceptButton = btnOK;

            // Reposition button after auto-size
            this.Layout += (s, e) =>
            {
                btnOK.Location = new Point(this.ClientSize.Width - 95, this.ClientSize.Height - 45);
            };
        }

        private int AddRow(string label, string value, int row)
        {
            _table.Controls.Add(CreateTitleLabel(label), 0, row);
            var valueLabel = CreateValueLabel(value);
            valueLabel.Font = new Font("Consolas", 9F);
            _table.Controls.Add(valueLabel, 1, row);
            return row + 1;
        }

        private int AddSeparator(int row)
        {
            var sep = new Panel
            {
                Height = 2,
                Dock = DockStyle.Top,
                BackColor = SystemColors.ControlDark,
                Margin = new Padding(0, 8, 0, 8)
            };
            _table.Controls.Add(sep, 0, row);
            _table.SetColumnSpan(sep, 2);
            return row + 1;
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
                return "Data Directive";
            }
            return "Unknown";
        }

        private string GetTypeText()
        {
            if (_lineInfo.IsMacroCall)
            {
                return "Macro";
            }
            if (_lineInfo.IsPredefCall)
            {
                return _lineInfo.PredefTypeLabel;
            }
            if (_lineInfo.IsDataDirective)
            {
                return "Data Directive";
            }
            return "Instruction";
        }

        private string GetCyclesText()
        {
            if (_lineInfo.Opcode != null && _lineInfo.Opcode.Cycles.Length > 1)
            {
                return $"{_lineInfo.Opcode.Cycles[0]}c (taken) / {_lineInfo.Opcode.Cycles[1]}c (not taken)";
            }
            if (_lineInfo.IsMacroCall && _lineInfo.MacroCyclesMin != _lineInfo.MacroCyclesMax)
            {
                return $"{_lineInfo.MacroCyclesMin}c - {_lineInfo.MacroCyclesMax}c";
            }
            return $"{_lineInfo.Cycles}c";
        }

        private string GetPredefComposition()
        {
            if (_lineInfo.PredefTypeLabel == "PREDEF_JUMP")
            {
                return "ld a,BANK + ld hl,addr + jp";
            }
            return "ld a,BANK + ld hl,addr + call";
        }

        private void LineInfoForm_Load(object sender, EventArgs e)
        {

        }
    }
}
