using System;
using System.Collections.Generic;
using System.Linq;

namespace GBZ80AsmMetrics.Core
{
    /// <summary>
    /// Main metrics calculation engine
    /// </summary>
    public class MetricsEngine
    {
        private readonly RGBDSParser _parser;
        private readonly OpcodeDatabase _opcodeDb;
        private readonly SettingsManager _settings;
        private readonly Dictionary<string, int> _startPoints;
        private readonly Dictionary<string, LineInfo[]> _lineMetricsCache;

        public MetricsEngine(SettingsManager settings)
        {
            _settings = settings;
            _parser = new RGBDSParser();
            _opcodeDb = OpcodeDatabase.Instance;
            _startPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _lineMetricsCache = new Dictionary<string, LineInfo[]>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parse a document for macro definitions
        /// </summary>
        public void ParseDocument(string[] lines, string baseDir)
        {
            _parser.ParseDocument(lines, baseDir);
        }

        /// <summary>
        /// Get line information for a specific line
        /// </summary>
        public LineInfo GetLineInfo(string lineText, int lineNumber, string filePath)
        {
            var parsed = _parser.ParseLine(lineText, lineNumber);
            var info = new LineInfo
            {
                LineNumber = lineNumber,
                HasMetrics = false,
                ParsedLine = parsed
            };

            // Skip directive-only lines (except data directives)
            if (parsed.IsDirective && !parsed.DirectiveBytes.HasValue)
            {
                return info;
            }

            // Handle PREDEF calls
            if (parsed.IsPredefCall)
            {
                info.HasMetrics = true;
                info.IsPredefCall = true;
                info.PredefName = parsed.Operands.Count > 0 ? parsed.Operands[0] : "unknown";

                if (parsed.PredefType == PredefType.PredefJump)
                {
                    info.Bytes = _settings.PredefJumpBytes;
                    info.Cycles = _settings.PredefJumpCycles;
                    info.PredefTypeLabel = "PREDEF_JUMP";
                }
                else
                {
                    info.Bytes = _settings.PredefBytes;
                    info.Cycles = _settings.PredefCycles;
                    info.PredefTypeLabel = "PREDEF";
                }

                return info;
            }

            // Handle macro calls
            if (parsed.IsMacroCall && parsed.MacroName != null)
            {
                var macro = _parser.MacroRegistry.Get(parsed.MacroName);
                if (macro != null)
                {
                    info.HasMetrics = true;
                    info.IsMacroCall = true;
                    info.MacroName = parsed.MacroName;
                    info.Bytes = macro.Bytes;
                    info.Cycles = _settings.AssumeBranchTaken ? macro.Cycles[0] : macro.Cycles[macro.Cycles.Length - 1];
                    info.MacroCyclesMin = macro.Cycles[macro.Cycles.Length - 1];
                    info.MacroCyclesMax = macro.Cycles[0];
                    info.MacroInstructionCount = macro.Instructions.Count;
                }
                return info;
            }

            // Handle regular instructions
            if (parsed.Instruction != null && !parsed.IsDirective)
            {
                var opcode = _opcodeDb.Lookup(parsed.Instruction, parsed.Operands.ToArray());
                if (opcode != null)
                {
                    info.HasMetrics = true;
                    info.Bytes = opcode.Bytes;
                    info.Cycles = _settings.AssumeBranchTaken ? opcode.Cycles[0] : (opcode.Cycles.Length > 1 ? opcode.Cycles[1] : opcode.Cycles[0]);
                    info.Opcode = opcode;
                    info.IsCBPrefixed = _opcodeDb.IsCBPrefixedInstruction(parsed.Instruction);
                    info.OpcodeHex = _opcodeDb.GetOpcodeHex(opcode, info.IsCBPrefixed);
                }
                return info;
            }

            // Handle data directives
            if (parsed.IsDirective && parsed.DirectiveBytes.HasValue)
            {
                info.HasMetrics = true;
                info.Bytes = parsed.DirectiveBytes.Value;
                info.Cycles = 0;
                info.IsDataDirective = true;
                return info;
            }

            return info;
        }

        #region Start Point Management

        public void SetStartPoint(string filePath, int line)
        {
            _startPoints[filePath] = line;
        }

        public void ClearStartPoint(string filePath)
        {
            _startPoints.Remove(filePath);
        }

        public int? GetStartPoint(string filePath)
        {
            if (_startPoints.TryGetValue(filePath, out int line))
                return line;
            return null;
        }

        public bool HasStartPoint(string filePath)
        {
            return _startPoints.ContainsKey(filePath);
        }

        public void ClearAllStartPoints()
        {
            _startPoints.Clear();
        }

        #endregion
    }

    /// <summary>
    /// Information about a single line's metrics
    /// </summary>
    public class LineInfo
    {
        public int LineNumber { get; set; }
        public bool HasMetrics { get; set; }
        public int Bytes { get; set; }
        public int Cycles { get; set; }
        public int CumulativeBytes { get; set; }
        public int CumulativeCycles { get; set; }

        // Opcode info
        public OpcodeInfo Opcode { get; set; }
        public string OpcodeHex { get; set; }
        public bool IsCBPrefixed { get; set; }

        // Macro info
        public bool IsMacroCall { get; set; }
        public string MacroName { get; set; }
        public int MacroCyclesMin { get; set; }
        public int MacroCyclesMax { get; set; }
        public int MacroInstructionCount { get; set; }

        // Predef info
        public bool IsPredefCall { get; set; }
        public string PredefName { get; set; }
        public string PredefTypeLabel { get; set; }

        // Data directive
        public bool IsDataDirective { get; set; }

        // Parsed line
        public ParsedLine ParsedLine { get; set; }
    }
}
