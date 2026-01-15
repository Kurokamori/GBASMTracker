import { ParsedLine, MacroDefinition } from './types';

export class MacroRegistry {
  private macros: Map<string, MacroDefinition> = new Map();

  clear(): void {
    this.macros.clear();
  }

  register(macro: MacroDefinition): void {
    this.macros.set(macro.name.toUpperCase(), macro);
  }

  get(name: string): MacroDefinition | undefined {
    return this.macros.get(name.toUpperCase());
  }

  has(name: string): boolean {
    return this.macros.has(name.toUpperCase());
  }

  getAll(): MacroDefinition[] {
    return Array.from(this.macros.values());
  }
}

export class RGBDSParser {
  private static readonly DIRECTIVES_WITH_BYTES = ['DB', 'DW', 'DL', 'DS'];
  private static readonly SECTION_DIRECTIVES = ['SECTION', 'INCLUDE', 'INCBIN', 'EQU', 'SET', 'EQUS', 'MACRO', 'ENDM', 'IF', 'ELSE', 'ELIF', 'ENDC', 'REPT', 'ENDR', 'EXPORT', 'GLOBAL', 'PURGE', 'OPT', 'PUSHO', 'POPO', 'PUSHS', 'POPS', 'FAIL', 'WARN', 'ASSERT', 'STATIC_ASSERT'];
  private static readonly PREDEF_KEYWORDS: { [key: string]: 'predef' | 'predef_jump' } = {
    'PREDEF': 'predef',
    'PREDEF_JUMP': 'predef_jump',
    'FARCALL': 'predef',        // Alternative name used in some projects
    'CALLFAR': 'predef',        // Alternative name
    'HOMECALL': 'predef',       // Another variant
  };

  private macroRegistry: MacroRegistry;

  constructor(macroRegistry?: MacroRegistry) {
    this.macroRegistry = macroRegistry || new MacroRegistry();
  }

  getMacroRegistry(): MacroRegistry {
    return this.macroRegistry;
  }

  parseLine(line: string, lineNumber: number): ParsedLine {
    const result: ParsedLine = {
      lineNumber,
      operands: [],
      isDirective: false,
      raw: line
    };

    // Remove comments first
    let workingLine = this.removeComments(line);

    // Extract comment for storage
    const commentMatch = line.match(/;(.*)$/);
    if (commentMatch) {
      result.comment = commentMatch[1].trim();
    }

    // Trim whitespace
    workingLine = workingLine.trim();

    // Empty line after removing comments
    if (!workingLine) {
      return result;
    }

    // Check for label
    const labelMatch = workingLine.match(/^(\w+:|\.[\w.]+:?)/);
    if (labelMatch) {
      result.label = labelMatch[1].replace(/:$/, '');
      workingLine = workingLine.slice(labelMatch[0].length).trim();
    }

    // If nothing left after label, return
    if (!workingLine) {
      return result;
    }

    // Parse instruction or directive
    const parts = this.splitInstruction(workingLine);
    if (parts.length === 0) {
      return result;
    }

    const mnemonic = parts[0].toUpperCase();

    // Check if it's a directive
    if (this.isDirective(mnemonic)) {
      result.isDirective = true;
      result.instruction = mnemonic;

      // Calculate bytes for data directives
      if (RGBDSParser.DIRECTIVES_WITH_BYTES.includes(mnemonic)) {
        result.directiveBytes = this.calculateDirectiveBytes(mnemonic, parts.slice(1).join(' '));
      }

      return result;
    }

    // Check if it's a macro call
    if (this.macroRegistry.has(mnemonic)) {
      result.isMacroCall = true;
      result.macroName = mnemonic;
      result.instruction = mnemonic;

      // Parse operands (macro arguments)
      if (parts.length > 1) {
        const operandsStr = parts.slice(1).join(' ');
        result.operands = this.parseOperands(operandsStr);
      }

      return result;
    }

    // Check if it's a predef call (predef, predef_jump, farcall, etc.)
    const predefType = RGBDSParser.PREDEF_KEYWORDS[mnemonic];
    if (predefType) {
      result.isPredefCall = true;
      result.predefType = predefType;
      result.instruction = mnemonic;

      // Parse the function name operand
      if (parts.length > 1) {
        const operandsStr = parts.slice(1).join(' ');
        result.operands = this.parseOperands(operandsStr);
      }

      return result;
    }

    // It's an instruction
    result.instruction = mnemonic;

    // Parse operands
    if (parts.length > 1) {
      const operandsStr = parts.slice(1).join(' ');
      result.operands = this.parseOperands(operandsStr);
    }

    return result;
  }

  private removeComments(line: string): string {
    // Handle semicolon comments (most common)
    let result = line;

    // Find semicolon that's not inside a string
    let inString = false;
    let stringChar = '';

    for (let i = 0; i < result.length; i++) {
      const char = result[i];

      if (!inString && (char === '"' || char === "'")) {
        inString = true;
        stringChar = char;
      } else if (inString && char === stringChar) {
        inString = false;
      } else if (!inString && char === ';') {
        result = result.slice(0, i);
        break;
      }
    }

    return result;
  }

  private splitInstruction(line: string): string[] {
    const parts: string[] = [];
    let current = '';
    let inBracket = 0;
    let inString = false;
    let stringChar = '';

    for (let i = 0; i < line.length; i++) {
      const char = line[i];

      if (!inString && (char === '"' || char === "'")) {
        inString = true;
        stringChar = char;
        current += char;
      } else if (inString && char === stringChar) {
        inString = false;
        current += char;
      } else if (!inString) {
        if (char === '[' || char === '(') {
          inBracket++;
          current += char;
        } else if (char === ']' || char === ')') {
          inBracket--;
          current += char;
        } else if ((char === ' ' || char === '\t') && inBracket === 0 && parts.length === 0) {
          // Space after mnemonic
          if (current) {
            parts.push(current);
            current = '';
          }
        } else {
          current += char;
        }
      } else {
        current += char;
      }
    }

    if (current.trim()) {
      parts.push(current.trim());
    }

    return parts;
  }

  private parseOperands(operandsStr: string): string[] {
    const operands: string[] = [];
    let current = '';
    let inBracket = 0;
    let inString = false;
    let stringChar = '';

    for (let i = 0; i < operandsStr.length; i++) {
      const char = operandsStr[i];

      if (!inString && (char === '"' || char === "'")) {
        inString = true;
        stringChar = char;
        current += char;
      } else if (inString && char === stringChar) {
        inString = false;
        current += char;
      } else if (!inString) {
        if (char === '[' || char === '(') {
          inBracket++;
          current += char;
        } else if (char === ']' || char === ')') {
          inBracket--;
          current += char;
        } else if (char === ',' && inBracket === 0) {
          if (current.trim()) {
            operands.push(current.trim());
          }
          current = '';
        } else {
          current += char;
        }
      } else {
        current += char;
      }
    }

    if (current.trim()) {
      operands.push(current.trim());
    }

    return operands;
  }

  private isDirective(mnemonic: string): boolean {
    return RGBDSParser.DIRECTIVES_WITH_BYTES.includes(mnemonic) ||
           RGBDSParser.SECTION_DIRECTIVES.includes(mnemonic);
  }

  private calculateDirectiveBytes(directive: string, args: string): number {
    switch (directive) {
      case 'DB':
        return this.countDBBytes(args);
      case 'DW':
        return this.countDWBytes(args);
      case 'DL':
        return this.countDLBytes(args);
      case 'DS':
        return this.countDSBytes(args);
      default:
        return 0;
    }
  }

  private countDBBytes(args: string): number {
    if (!args.trim()) return 0;

    // Split by comma, but handle strings
    const items = this.parseOperands(args);
    let bytes = 0;

    for (const item of items) {
      const trimmed = item.trim();

      // String literal
      if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
        // Count characters in string (excluding quotes)
        bytes += trimmed.length - 2;
      } else {
        // Single byte value
        bytes += 1;
      }
    }

    return bytes;
  }

  private countDWBytes(args: string): number {
    if (!args.trim()) return 0;
    const items = this.parseOperands(args);
    return items.length * 2;
  }

  private countDLBytes(args: string): number {
    if (!args.trim()) return 0;
    const items = this.parseOperands(args);
    return items.length * 4;
  }

  private countDSBytes(args: string): number {
    if (!args.trim()) return 0;

    // DS takes a count as first argument
    const parts = this.parseOperands(args);
    if (parts.length === 0) return 0;

    const countStr = parts[0].trim();

    // Parse the count value
    const count = this.parseNumericValue(countStr);
    return count;
  }

  private parseNumericValue(value: string): number {
    value = value.trim();

    // Hexadecimal $FF or 0xFF
    if (value.startsWith('$')) {
      return parseInt(value.slice(1), 16) || 0;
    }
    if (value.startsWith('0x') || value.startsWith('0X')) {
      return parseInt(value.slice(2), 16) || 0;
    }

    // Binary %10101010
    if (value.startsWith('%')) {
      return parseInt(value.slice(1), 2) || 0;
    }

    // Octal &777
    if (value.startsWith('&')) {
      return parseInt(value.slice(1), 8) || 0;
    }

    // Decimal
    return parseInt(value, 10) || 0;
  }
  // Parse entire document to extract macro definitions
  // baseDir is used to resolve INCLUDE paths
  parseDocument(lines: string[], baseDir?: string): void {
    this.macroRegistry.clear();
    this.parseDocumentInternal(lines, baseDir, new Set());
  }

  // Internal method that tracks already-parsed files to avoid infinite recursion
  private parseDocumentInternal(lines: string[], baseDir?: string, parsedFiles?: Set<string>): void {
    let inMacro = false;
    let currentMacroName = '';
    let macroStartLine = 0;
    let macroLines: string[] = [];

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const workingLine = this.removeComments(line).trim();

      // Check for INCLUDE directive to parse external macros
      const includeMatch = workingLine.match(/^INCLUDE\s+["']?([^"'\s]+)["']?/i);
      if (includeMatch && baseDir) {
        const includePath = includeMatch[1];
        this.parseIncludedFile(includePath, baseDir, parsedFiles || new Set());
        continue;
      }

      // Check for MACRO definition start
      // Format: MacroName: MACRO  or  MACRO MacroName
      const macroMatch = workingLine.match(/^(\w+):\s*MACRO\b/i) ||
                         workingLine.match(/^MACRO\s+(\w+)/i);

      if (macroMatch && !inMacro) {
        inMacro = true;
        currentMacroName = macroMatch[1].toUpperCase();
        macroStartLine = i;
        macroLines = [];
        continue;
      }

      // Check for ENDM
      if (/^\s*ENDM\b/i.test(workingLine) && inMacro) {
        // Calculate macro metrics
        const macroDef = this.calculateMacroMetrics(
          currentMacroName,
          macroStartLine,
          i,
          macroLines
        );
        this.macroRegistry.register(macroDef);

        inMacro = false;
        currentMacroName = '';
        macroLines = [];
        continue;
      }

      // Collect lines inside macro
      if (inMacro) {
        macroLines.push(line);
      }
    }
  }

  // Parse an included file to extract its macros
  private parseIncludedFile(includePath: string, baseDir: string, parsedFiles: Set<string>): void {
    try {
      const path = require('path');
      const fs = require('fs');

      // Resolve the include path relative to the base directory
      const fullPath = path.resolve(baseDir, includePath);

      // Avoid parsing the same file twice (circular includes)
      if (parsedFiles.has(fullPath)) {
        return;
      }
      parsedFiles.add(fullPath);

      // Check if file exists
      if (!fs.existsSync(fullPath)) {
        // Try common include directories
        const altPaths = [
          path.resolve(baseDir, 'inc', includePath),
          path.resolve(baseDir, 'include', includePath),
          path.resolve(baseDir, 'src', includePath),
        ];

        for (const altPath of altPaths) {
          if (fs.existsSync(altPath)) {
            if (!parsedFiles.has(altPath)) {
              parsedFiles.add(altPath);
              const content = fs.readFileSync(altPath, 'utf8');
              const lines = content.split(/\r?\n/);
              this.parseDocumentInternal(lines, path.dirname(altPath), parsedFiles);
            }
            return;
          }
        }
        return; // File not found, skip
      }

      // Read and parse the included file
      const content = fs.readFileSync(fullPath, 'utf8');
      const lines = content.split(/\r?\n/);
      this.parseDocumentInternal(lines, path.dirname(fullPath), parsedFiles);
    } catch (e) {
      // Silently ignore errors (file not found, permission denied, etc.)
      console.log(`Could not parse included file: ${includePath}`, e);
    }
  }

  private calculateMacroMetrics(
    name: string,
    startLine: number,
    endLine: number,
    lines: string[]
  ): MacroDefinition {
    // Import opcodeDatabase dynamically to avoid circular dependency
    const { opcodeDatabase } = require('../opcodes/opcodeData');

    let totalBytes = 0;
    let minCycles = 0;
    let maxCycles = 0;
    const instructions: ParsedLine[] = [];

    for (let i = 0; i < lines.length; i++) {
      const parsed = this.parseLine(lines[i], startLine + 1 + i);
      instructions.push(parsed);

      if (parsed.instruction && !parsed.isDirective && !parsed.isMacroCall) {
        const opcode = opcodeDatabase.lookup(parsed.instruction, parsed.operands);
        if (opcode) {
          totalBytes += opcode.bytes;
          minCycles += opcode.cycles[opcode.cycles.length - 1]; // Use min (not taken)
          maxCycles += opcode.cycles[0]; // Use max (taken)
        }
      } else if (parsed.isDirective && parsed.directiveBytes !== undefined) {
        totalBytes += parsed.directiveBytes;
      } else if (parsed.isMacroCall && parsed.macroName) {
        // Nested macro call - get its metrics
        const nestedMacro = this.macroRegistry.get(parsed.macroName);
        if (nestedMacro) {
          totalBytes += nestedMacro.bytes;
          minCycles += nestedMacro.cycles[nestedMacro.cycles.length - 1];
          maxCycles += nestedMacro.cycles[0];
        }
      }
    }

    return {
      name,
      startLine,
      endLine,
      bytes: totalBytes,
      cycles: minCycles === maxCycles ? [minCycles] : [maxCycles, minCycles],
      instructions
    };
  }
}

export const macroRegistry = new MacroRegistry();
export const parser = new RGBDSParser(macroRegistry);
