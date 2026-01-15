export interface ParsedLine {
  lineNumber: number;
  label?: string;
  instruction?: string;
  operands: string[];
  comment?: string;
  isDirective: boolean;
  directiveBytes?: number;
  isMacroCall?: boolean;
  macroName?: string;
  isPredefCall?: boolean;
  predefType?: 'predef' | 'predef_jump';
  raw: string;
}

export interface MacroDefinition {
  name: string;
  startLine: number;
  endLine: number;
  bytes: number;
  cycles: number[];  // [min, max] for conditionals, or [exact] if known
  instructions: ParsedLine[];
}

export interface OpcodeInfo {
  opcode: number;
  mnemonic: string;
  bytes: number;
  cycles: number[];
  flags: FlagEffect;
  operands: string[];
}

export interface FlagEffect {
  Z: string;  // '-' | '0' | '1' | 'Z'
  N: string;  // '-' | '0' | '1'
  H: string;  // '-' | '0' | '1' | 'H'
  C: string;  // '-' | '0' | '1' | 'C'
}

export interface LineMetrics {
  bytes: number;
  cycles: number;
  cumulativeBytes: number;
  cumulativeCycles: number;
  opcode?: OpcodeInfo;
}

export interface ExpandedInfo {
  hexBytes: string;
  flags: FlagEffect;
  cycleInfo: string;
}

export interface RoutineArgument {
  register: string;     // e.g., "a", "bc", "hl", "de"
  description: string;  // e.g., "Pokemon ID", "source address"
}

export interface RoutineDefinition {
  name: string;
  filePath: string;
  lineNumber: number;
  arguments: RoutineArgument[];
  description?: string;  // Optional general description of the routine
}
