using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GBZ80AsmMetrics.Core
{
    /// <summary>
    /// Parser for RGBDS Game Boy assembly syntax
    /// </summary>
    public class RGBDSParser
    {
        private static readonly string[] DirectivesWithBytes = { "DB", "DW", "DL", "DS" };
        private static readonly string[] SectionDirectives = {
            "SECTION", "INCLUDE", "INCBIN", "EQU", "SET", "EQUS",
            "MACRO", "ENDM", "IF", "ELSE", "ELIF", "ENDC", "REPT", "ENDR",
            "EXPORT", "GLOBAL", "PURGE", "OPT", "PUSHO", "POPO",
            "PUSHS", "POPS", "FAIL", "WARN", "ASSERT", "STATIC_ASSERT"
        };
        private static readonly Dictionary<string, PredefType> PredefKeywords = new Dictionary<string, PredefType>(StringComparer.OrdinalIgnoreCase)
        {
            { "PREDEF", PredefType.Predef },
            { "PREDEF_JUMP", PredefType.PredefJump },
            { "FARCALL", PredefType.Predef },
            { "CALLFAR", PredefType.Predef },
            { "HOMECALL", PredefType.Predef }
        };

        private readonly MacroRegistry _macroRegistry;

        public RGBDSParser(MacroRegistry macroRegistry = null)
        {
            _macroRegistry = macroRegistry ?? new MacroRegistry();
        }

        public MacroRegistry MacroRegistry => _macroRegistry;

        /// <summary>
        /// Parse a single line of assembly code
        /// </summary>
        public ParsedLine ParseLine(string line, int lineNumber)
        {
            var result = new ParsedLine
            {
                LineNumber = lineNumber,
                Operands = new List<string>(),
                IsDirective = false,
                Raw = line
            };

            // Remove comments first
            string workingLine = RemoveComments(line);

            // Extract comment for storage
            var commentMatch = Regex.Match(line, @";(.*)$");
            if (commentMatch.Success)
            {
                result.Comment = commentMatch.Groups[1].Value.Trim();
            }

            workingLine = workingLine.Trim();

            // Empty line after removing comments
            if (string.IsNullOrEmpty(workingLine))
            {
                return result;
            }

            // Check for label
            var labelMatch = Regex.Match(workingLine, @"^(\w+:|\.[\w.]+:?)");
            if (labelMatch.Success)
            {
                result.Label = labelMatch.Value.TrimEnd(':');
                workingLine = workingLine.Substring(labelMatch.Length).Trim();
            }

            // If nothing left after label, return
            if (string.IsNullOrEmpty(workingLine))
            {
                return result;
            }

            // Parse instruction or directive
            var parts = SplitInstruction(workingLine);
            if (parts.Count == 0)
            {
                return result;
            }

            string mnemonic = parts[0].ToUpperInvariant();

            // Check if it's a directive
            if (IsDirective(mnemonic))
            {
                result.IsDirective = true;
                result.Instruction = mnemonic;

                if (DirectivesWithBytes.Contains(mnemonic))
                {
                    string argsStr = parts.Count > 1 ? string.Join(" ", parts.Skip(1)) : "";
                    result.DirectiveBytes = CalculateDirectiveBytes(mnemonic, argsStr);
                }

                return result;
            }

            // Check if it's a macro call
            if (_macroRegistry.Has(mnemonic))
            {
                result.IsMacroCall = true;
                result.MacroName = mnemonic;
                result.Instruction = mnemonic;

                if (parts.Count > 1)
                {
                    string operandsStr = string.Join(" ", parts.Skip(1));
                    result.Operands = ParseOperands(operandsStr);
                }

                return result;
            }

            // Check if it's a predef call
            if (PredefKeywords.TryGetValue(mnemonic, out var predefType))
            {
                result.IsPredefCall = true;
                result.PredefType = predefType;
                result.Instruction = mnemonic;

                if (parts.Count > 1)
                {
                    string operandsStr = string.Join(" ", parts.Skip(1));
                    result.Operands = ParseOperands(operandsStr);
                }

                return result;
            }

            // It's an instruction
            result.Instruction = mnemonic;

            if (parts.Count > 1)
            {
                string operandsStr = string.Join(" ", parts.Skip(1));
                result.Operands = ParseOperands(operandsStr);
            }

            return result;
        }

        private string RemoveComments(string line)
        {
            string result = line;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < result.Length; i++)
            {
                char c = result[i];

                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                }
                else if (inString && c == stringChar)
                {
                    inString = false;
                }
                else if (!inString && c == ';')
                {
                    return result.Substring(0, i);
                }
            }

            return result;
        }

        private List<string> SplitInstruction(string line)
        {
            var parts = new List<string>();
            string current = "";
            int inBracket = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                    current += c;
                }
                else if (inString && c == stringChar)
                {
                    inString = false;
                    current += c;
                }
                else if (!inString)
                {
                    if (c == '[' || c == '(')
                    {
                        inBracket++;
                        current += c;
                    }
                    else if (c == ']' || c == ')')
                    {
                        inBracket--;
                        current += c;
                    }
                    else if ((c == ' ' || c == '\t') && inBracket == 0 && parts.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(current))
                        {
                            parts.Add(current);
                            current = "";
                        }
                    }
                    else
                    {
                        current += c;
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                parts.Add(current.Trim());
            }

            return parts;
        }

        private List<string> ParseOperands(string operandsStr)
        {
            var operands = new List<string>();
            string current = "";
            int inBracket = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < operandsStr.Length; i++)
            {
                char c = operandsStr[i];

                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                    current += c;
                }
                else if (inString && c == stringChar)
                {
                    inString = false;
                    current += c;
                }
                else if (!inString)
                {
                    if (c == '[' || c == '(')
                    {
                        inBracket++;
                        current += c;
                    }
                    else if (c == ']' || c == ')')
                    {
                        inBracket--;
                        current += c;
                    }
                    else if (c == ',' && inBracket == 0)
                    {
                        if (!string.IsNullOrWhiteSpace(current))
                        {
                            operands.Add(current.Trim());
                        }
                        current = "";
                    }
                    else
                    {
                        current += c;
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                operands.Add(current.Trim());
            }

            return operands;
        }

        private bool IsDirective(string mnemonic)
        {
            return DirectivesWithBytes.Contains(mnemonic) || SectionDirectives.Contains(mnemonic);
        }

        private int CalculateDirectiveBytes(string directive, string args)
        {
            switch (directive)
            {
                case "DB": return CountDBBytes(args);
                case "DW": return CountDWBytes(args);
                case "DL": return CountDLBytes(args);
                case "DS": return CountDSBytes(args);
                default: return 0;
            }
        }

        private int CountDBBytes(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return 0;

            var items = ParseOperands(args);
            int bytes = 0;

            foreach (var item in items)
            {
                string trimmed = item.Trim();

                // String literal
                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                {
                    bytes += trimmed.Length - 2;
                }
                else
                {
                    bytes += 1;
                }
            }

            return bytes;
        }

        private int CountDWBytes(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return 0;
            return ParseOperands(args).Count * 2;
        }

        private int CountDLBytes(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return 0;
            return ParseOperands(args).Count * 4;
        }

        private int CountDSBytes(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return 0;

            var parts = ParseOperands(args);
            if (parts.Count == 0) return 0;

            return ParseNumericValue(parts[0].Trim());
        }

        private int ParseNumericValue(string value)
        {
            value = value.Trim();

            // Hexadecimal $FF or 0xFF
            if (value.StartsWith("$"))
            {
                if (int.TryParse(value.Substring(1), System.Globalization.NumberStyles.HexNumber, null, out int result))
                    return result;
                return 0;
            }
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int result))
                    return result;
                return 0;
            }

            // Binary %10101010
            if (value.StartsWith("%"))
            {
                try { return Convert.ToInt32(value.Substring(1), 2); }
                catch { return 0; }
            }

            // Octal &777
            if (value.StartsWith("&"))
            {
                try { return Convert.ToInt32(value.Substring(1), 8); }
                catch { return 0; }
            }

            // Decimal
            if (int.TryParse(value, out int dec))
                return dec;

            return 0;
        }

        /// <summary>
        /// Parse entire document to extract macro definitions
        /// </summary>
        public void ParseDocument(string[] lines, string baseDir = null)
        {
            _macroRegistry.Clear();
            ParseDocumentInternal(lines, baseDir, new HashSet<string>());
        }

        private void ParseDocumentInternal(string[] lines, string baseDir, HashSet<string> parsedFiles)
        {
            bool inMacro = false;
            string currentMacroName = "";
            int macroStartLine = 0;
            var macroLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string workingLine = RemoveComments(line).Trim();

                // Check for INCLUDE directive
                var includeMatch = Regex.Match(workingLine, @"^INCLUDE\s+[""']?([^""'\s]+)[""']?", RegexOptions.IgnoreCase);
                if (includeMatch.Success && !string.IsNullOrEmpty(baseDir))
                {
                    string includePath = includeMatch.Groups[1].Value;
                    ParseIncludedFile(includePath, baseDir, parsedFiles);
                    continue;
                }

                // Check for MACRO definition start
                var macroMatch = Regex.Match(workingLine, @"^(\w+):\s*MACRO\b", RegexOptions.IgnoreCase);
                if (!macroMatch.Success)
                {
                    macroMatch = Regex.Match(workingLine, @"^MACRO\s+(\w+)", RegexOptions.IgnoreCase);
                }

                if (macroMatch.Success && !inMacro)
                {
                    inMacro = true;
                    currentMacroName = macroMatch.Groups[1].Value.ToUpperInvariant();
                    macroStartLine = i;
                    macroLines.Clear();
                    continue;
                }

                // Check for ENDM
                if (Regex.IsMatch(workingLine, @"^\s*ENDM\b", RegexOptions.IgnoreCase) && inMacro)
                {
                    var macroDef = CalculateMacroMetrics(currentMacroName, macroStartLine, i, macroLines.ToArray());
                    _macroRegistry.Register(macroDef);

                    inMacro = false;
                    currentMacroName = "";
                    macroLines.Clear();
                    continue;
                }

                if (inMacro)
                {
                    macroLines.Add(line);
                }
            }
        }

        private void ParseIncludedFile(string includePath, string baseDir, HashSet<string> parsedFiles)
        {
            try
            {
                string fullPath = Path.Combine(baseDir, includePath);

                // Avoid circular includes
                if (parsedFiles.Contains(fullPath))
                    return;

                if (!File.Exists(fullPath))
                {
                    // Try common include directories
                    var altPaths = new[]
                    {
                        Path.Combine(baseDir, "inc", includePath),
                        Path.Combine(baseDir, "include", includePath),
                        Path.Combine(baseDir, "src", includePath)
                    };

                    foreach (var altPath in altPaths)
                    {
                        if (File.Exists(altPath))
                        {
                            fullPath = altPath;
                            break;
                        }
                    }
                }

                if (!File.Exists(fullPath))
                    return;

                parsedFiles.Add(fullPath);

                string content = File.ReadAllText(fullPath);
                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                string newBaseDir = Path.GetDirectoryName(fullPath);

                ParseDocumentInternal(lines, newBaseDir, parsedFiles);
            }
            catch
            {
                // Silently ignore errors
            }
        }

        private MacroDefinition CalculateMacroMetrics(string name, int startLine, int endLine, string[] lines)
        {
            var db = OpcodeDatabase.Instance;

            int totalBytes = 0;
            int minCycles = 0;
            int maxCycles = 0;
            var instructions = new List<ParsedLine>();

            for (int i = 0; i < lines.Length; i++)
            {
                var parsed = ParseLine(lines[i], startLine + 1 + i);
                instructions.Add(parsed);

                if (parsed.Instruction != null && !parsed.IsDirective && !parsed.IsMacroCall)
                {
                    var opcode = db.Lookup(parsed.Instruction, parsed.Operands.ToArray());
                    if (opcode != null)
                    {
                        totalBytes += opcode.Bytes;
                        minCycles += opcode.Cycles[opcode.Cycles.Length - 1];
                        maxCycles += opcode.Cycles[0];
                    }
                }
                else if (parsed.IsDirective && parsed.DirectiveBytes.HasValue)
                {
                    totalBytes += parsed.DirectiveBytes.Value;
                }
                else if (parsed.IsMacroCall && parsed.MacroName != null)
                {
                    var nestedMacro = _macroRegistry.Get(parsed.MacroName);
                    if (nestedMacro != null)
                    {
                        totalBytes += nestedMacro.Bytes;
                        minCycles += nestedMacro.Cycles[nestedMacro.Cycles.Length - 1];
                        maxCycles += nestedMacro.Cycles[0];
                    }
                }
            }

            return new MacroDefinition
            {
                Name = name,
                StartLine = startLine,
                EndLine = endLine,
                Bytes = totalBytes,
                Cycles = minCycles == maxCycles ? new[] { minCycles } : new[] { maxCycles, minCycles },
                Instructions = instructions
            };
        }
    }

    /// <summary>
    /// Registry for macro definitions
    /// </summary>
    public class MacroRegistry
    {
        private readonly Dictionary<string, MacroDefinition> _macros = new Dictionary<string, MacroDefinition>(StringComparer.OrdinalIgnoreCase);

        public void Clear() => _macros.Clear();

        public void Register(MacroDefinition macro)
        {
            _macros[macro.Name] = macro;
        }

        public MacroDefinition Get(string name)
        {
            _macros.TryGetValue(name, out var macro);
            return macro;
        }

        public bool Has(string name) => _macros.ContainsKey(name);

        public IEnumerable<MacroDefinition> GetAll() => _macros.Values;
    }

    /// <summary>
    /// Represents a parsed line of assembly code
    /// </summary>
    public class ParsedLine
    {
        public int LineNumber { get; set; }
        public string Label { get; set; }
        public string Instruction { get; set; }
        public List<string> Operands { get; set; }
        public string Comment { get; set; }
        public bool IsDirective { get; set; }
        public int? DirectiveBytes { get; set; }
        public bool IsMacroCall { get; set; }
        public string MacroName { get; set; }
        public bool IsPredefCall { get; set; }
        public PredefType? PredefType { get; set; }
        public string Raw { get; set; }
    }

    /// <summary>
    /// Macro definition with calculated metrics
    /// </summary>
    public class MacroDefinition
    {
        public string Name { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int Bytes { get; set; }
        public int[] Cycles { get; set; }
        public List<ParsedLine> Instructions { get; set; }
    }

    /// <summary>
    /// Type of PREDEF call
    /// </summary>
    public enum PredefType
    {
        Predef,
        PredefJump
    }
}
