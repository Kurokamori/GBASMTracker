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

        // Valid register names for argument parsing
        private static readonly HashSet<string> ValidRegisters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "b", "c", "d", "e", "h", "l", "af", "bc", "de", "hl", "sp", "pc"
        };

        private readonly MacroRegistry _macroRegistry;
        private readonly RoutineRegistry _routineRegistry;

        public RGBDSParser(MacroRegistry macroRegistry = null, RoutineRegistry routineRegistry = null)
        {
            _macroRegistry = macroRegistry ?? new MacroRegistry();
            _routineRegistry = routineRegistry ?? new RoutineRegistry();
        }

        public MacroRegistry MacroRegistry => _macroRegistry;
        public RoutineRegistry RoutineRegistry => _routineRegistry;

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
        /// Parse entire document to extract macro and routine definitions
        /// </summary>
        public void ParseDocument(string[] lines, string baseDir = null, string filePath = null)
        {
            _macroRegistry.Clear();
            // Note: We don't clear routineRegistry here as it may contain routines from other files
            ParseDocumentInternal(lines, baseDir, new HashSet<string>(), filePath);
        }

        /// <summary>
        /// Clear all registries (call before scanning workspace)
        /// </summary>
        public void ClearAll()
        {
            _macroRegistry.Clear();
            _routineRegistry.Clear();
        }

        /// <summary>
        /// Parse argument documentation from comment lines
        /// </summary>
        private (List<RoutineArgument> args, string description) ParseArgumentComments(List<string> commentLines)
        {
            var args = new List<RoutineArgument>();
            string description = null;

            foreach (var comment in commentLines)
            {
                var trimmed = comment.Trim();

                // Check for "Input: a = value, hl = pointer" pattern
                var inputMatch = Regex.Match(trimmed, @"(?:inputs?|args?|arguments?|params?|parameters?):\s*(.+)", RegexOptions.IgnoreCase);
                if (inputMatch.Success)
                {
                    // Parse multiple arguments from one line
                    var argsStr = inputMatch.Groups[1].Value;
                    var argParts = argsStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in argParts)
                    {
                        var argMatch = Regex.Match(part.Trim(), @"([a-z]{1,2})(?:\s*[:=\-]\s*)(.+)", RegexOptions.IgnoreCase);
                        if (argMatch.Success)
                        {
                            var reg = argMatch.Groups[1].Value.ToLowerInvariant();
                            if (ValidRegisters.Contains(reg))
                            {
                                args.Add(new RoutineArgument { Register = reg, Description = argMatch.Groups[2].Value.Trim() });
                            }
                        }
                    }
                    continue;
                }

                // Check for "Register a: description" or just "a: description" pattern
                var regMatch = Regex.Match(trimmed, @"^(?:register\s+)?([a-z]{1,2})(?:\s*[:=\-]\s*)(.+)", RegexOptions.IgnoreCase);
                if (regMatch.Success)
                {
                    var reg = regMatch.Groups[1].Value.ToLowerInvariant();
                    if (ValidRegisters.Contains(reg))
                    {
                        args.Add(new RoutineArgument { Register = reg, Description = regMatch.Groups[2].Value.Trim() });
                        continue;
                    }
                }

                // Check for "@param" or "param" style documentation
                var paramMatch = Regex.Match(trimmed, @"@?param\s+([a-z]{1,2})(?:\s*[:=\-]?\s*)(.+)", RegexOptions.IgnoreCase);
                if (paramMatch.Success)
                {
                    var reg = paramMatch.Groups[1].Value.ToLowerInvariant();
                    if (ValidRegisters.Contains(reg))
                    {
                        args.Add(new RoutineArgument { Register = reg, Description = paramMatch.Groups[2].Value.Trim() });
                        continue;
                    }
                }

                // If no argument pattern matched, and this is a non-trivial comment, use as description
                if (description == null && trimmed.Length > 0 && !Regex.IsMatch(trimmed, @"^[-=]+$"))
                {
                    description = trimmed;
                }
            }

            return (args, description);
        }

        private void ParseDocumentInternal(string[] lines, string baseDir, HashSet<string> parsedFiles, string filePath = null)
        {
            bool inMacro = false;
            string currentMacroName = "";
            int macroStartLine = 0;
            var macroLines = new List<string>();

            // Track comment lines for routine argument documentation
            var pendingComments = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string workingLine = RemoveComments(line).Trim();

                // Extract comment from line
                var commentMatch = Regex.Match(line, @";(.*)$");
                string comment = commentMatch.Success ? commentMatch.Groups[1].Value.Trim() : null;

                // Check for INCLUDE directive
                var includeMatch = Regex.Match(workingLine, @"^INCLUDE\s+[""']?([^""'\s]+)[""']?", RegexOptions.IgnoreCase);
                if (includeMatch.Success && !string.IsNullOrEmpty(baseDir))
                {
                    string includePath = includeMatch.Groups[1].Value;
                    ParseIncludedFile(includePath, baseDir, parsedFiles);
                    pendingComments.Clear();
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
                    pendingComments.Clear();
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

                // Collect lines inside macro
                if (inMacro)
                {
                    macroLines.Add(line);
                    continue;
                }

                // Check for label definition (routine/variable)
                // Matches: "LabelName:" or "LabelName::" with optional content after
                var labelMatch = Regex.Match(workingLine, @"^(\w+)::?(?:\s|$)");
                if (labelMatch.Success)
                {
                    var labelName = labelMatch.Groups[1].Value;

                    // Don't create routine for local labels starting with "."
                    if (!labelName.StartsWith("."))
                    {
                        // Parse any pending comments for argument documentation
                        var (args, description) = ParseArgumentComments(pendingComments);

                        // Only register if there are arguments or description documented
                        if (args.Count > 0 || description != null)
                        {
                            var routine = new RoutineDefinition
                            {
                                Name = labelName,
                                FilePath = filePath ?? "",
                                LineNumber = i,
                                Arguments = args,
                                Description = description
                            };
                            _routineRegistry.Register(routine);
                        }
                    }

                    pendingComments.Clear();
                    continue;
                }

                // If this is a comment-only line, add to pending comments
                if (string.IsNullOrEmpty(workingLine) && comment != null)
                {
                    pendingComments.Add(comment);
                }
                else if (!string.IsNullOrEmpty(workingLine))
                {
                    // Non-comment, non-label line - reset pending comments
                    pendingComments.Clear();
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

                ParseDocumentInternal(lines, newBaseDir, parsedFiles, fullPath);
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

    /// <summary>
    /// Represents an argument/parameter for a routine
    /// </summary>
    public class RoutineArgument
    {
        public string Register { get; set; }      // e.g., "a", "bc", "hl"
        public string Description { get; set; }   // e.g., "Pokemon ID"
    }

    /// <summary>
    /// Represents a routine/label definition with documented arguments
    /// </summary>
    public class RoutineDefinition
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public List<RoutineArgument> Arguments { get; set; } = new List<RoutineArgument>();
        public string Description { get; set; }
    }

    /// <summary>
    /// Registry for routine definitions with documented arguments
    /// </summary>
    public class RoutineRegistry
    {
        private readonly Dictionary<string, RoutineDefinition> _routines =
            new Dictionary<string, RoutineDefinition>(StringComparer.OrdinalIgnoreCase);

        public void Clear() => _routines.Clear();

        public void Register(RoutineDefinition routine)
        {
            _routines[routine.Name] = routine;
        }

        public RoutineDefinition Get(string name)
        {
            _routines.TryGetValue(name, out var routine);
            return routine;
        }

        public bool Has(string name) => _routines.ContainsKey(name);

        public IEnumerable<RoutineDefinition> GetAll() => _routines.Values;
    }
}
