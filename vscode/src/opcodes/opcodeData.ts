import { OpcodeInfo, FlagEffect } from '../parser/types';
import unprefixedOpcodes from './unprefixed.json';
import cbPrefixedOpcodes from './cbprefixed.json';

interface RawOpcodeData {
  mnemonic: string;
  bytes: number;
  cycles: number[];
  flags: FlagEffect;
  operands: string[];
}

type OpcodeTable = Record<string, RawOpcodeData>;

class OpcodeDatabase {
  private lookupMap: Map<string, OpcodeInfo> = new Map();
  private cbLookupMap: Map<string, OpcodeInfo> = new Map();

  constructor() {
    this.buildLookupMaps();
  }

  private buildLookupMaps(): void {
    // Build unprefixed lookup
    for (const [hexCode, data] of Object.entries(unprefixedOpcodes as OpcodeTable)) {
      const opcode = parseInt(hexCode, 16);
      const key = this.buildLookupKey(data.mnemonic, data.operands);
      const info: OpcodeInfo = {
        opcode,
        mnemonic: data.mnemonic,
        bytes: data.bytes,
        cycles: data.cycles,
        flags: data.flags,
        operands: data.operands
      };
      this.lookupMap.set(key, info);
    }

    // Build CB-prefixed lookup
    for (const [hexCode, data] of Object.entries(cbPrefixedOpcodes as OpcodeTable)) {
      const opcode = parseInt(hexCode, 16);
      const key = this.buildLookupKey(data.mnemonic, data.operands);
      const info: OpcodeInfo = {
        opcode,
        mnemonic: data.mnemonic,
        bytes: data.bytes,
        cycles: data.cycles,
        flags: data.flags,
        operands: data.operands
      };
      this.cbLookupMap.set(key, info);
    }
  }

  private buildLookupKey(mnemonic: string, operands: string[]): string {
    const normalizedMnemonic = mnemonic.toUpperCase();
    const normalizedOperands = operands.map(op => this.normalizeOperand(op)).join(',');
    return normalizedOperands ? `${normalizedMnemonic} ${normalizedOperands}` : normalizedMnemonic;
  }

  private normalizeOperand(operand: string): string {
    return operand.toUpperCase()
      .replace(/\s+/g, '')
      .replace(/\(HL\+\)/gi, '[HL+]')
      .replace(/\(HL-\)/gi, '[HL-]')
      .replace(/\(HLI\)/gi, '[HL+]')
      .replace(/\(HLD\)/gi, '[HL-]')
      .replace(/\(([^)]+)\)/g, '[$1]');
  }

  lookup(mnemonic: string, operands: string[]): OpcodeInfo | null {
    const normalizedMnemonic = mnemonic.toUpperCase();

    // Check for CB-prefixed instructions
    const cbInstructions = ['RLC', 'RRC', 'RL', 'RR', 'SLA', 'SRA', 'SWAP', 'SRL', 'BIT', 'RES', 'SET'];
    if (cbInstructions.includes(normalizedMnemonic)) {
      return this.lookupCB(normalizedMnemonic, operands);
    }

    // Handle special cases for operand patterns
    const key = this.buildSearchKey(normalizedMnemonic, operands);
    return this.lookupMap.get(key) || this.findWithWildcard(normalizedMnemonic, operands);
  }

  private lookupCB(mnemonic: string, operands: string[]): OpcodeInfo | null {
    const key = this.buildSearchKey(mnemonic, operands);
    let result = this.cbLookupMap.get(key);
    if (result) return result;

    // For BIT/SET/RES with constant bit numbers, try all bit positions (0-7)
    if (['BIT', 'SET', 'RES'].includes(mnemonic) && operands.length === 2) {
      const register = this.normalizeSearchOperand(operands[1]);
      // Try each bit position 0-7
      for (let bit = 0; bit <= 7; bit++) {
        const tryKey = `${mnemonic} ${bit},${register}`;
        result = this.cbLookupMap.get(tryKey);
        if (result) return result;
      }
    }

    // For other CB instructions (RLC, RRC, RL, RR, SLA, SRA, SWAP, SRL) with one operand
    if (operands.length === 1) {
      const register = this.normalizeSearchOperand(operands[0]);
      const tryKey = `${mnemonic} ${register}`;
      result = this.cbLookupMap.get(tryKey);
      if (result) return result;
    }

    return null;
  }

  private buildSearchKey(mnemonic: string, operands: string[]): string {
    const normalizedOperands = operands.map(op => this.normalizeSearchOperand(op)).join(',');
    return normalizedOperands ? `${mnemonic} ${normalizedOperands}` : mnemonic;
  }

  private normalizeSearchOperand(operand: string): string {
    let normalized = operand.toUpperCase().trim();

    // Handle RGBDS functions like BANK(), HIGH(), LOW() - treat as 8-bit immediate
    if (/^(BANK|HIGH|LOW|SIZEOF|STARTOF)\s*\(/.test(normalized)) {
      return 'D8';
    }

    // Handle memory addressing variations (both parentheses and brackets)
    normalized = normalized
      .replace(/\s+/g, '')
      // Handle parentheses forms
      .replace(/\(HL\+\)/gi, '[HL+]')
      .replace(/\(HL-\)/gi, '[HL-]')
      .replace(/\(HLI\)/gi, '[HL+]')
      .replace(/\(HLD\)/gi, '[HL-]')
      .replace(/\(([^)]+)\)/g, '[$1]')
      // Handle bracket forms (for users who write [HLI] or [HLD] directly)
      .replace(/\[HL\+\]/gi, '[HL+]')
      .replace(/\[HL-\]/gi, '[HL-]')
      .replace(/\[HLI\]/gi, '[HL+]')
      .replace(/\[HLD\]/gi, '[HL-]');

    // Check if it's an immediate value (includes constants like BIT_SOMETHING)
    if (this.isImmediateValue(normalized)) {
      // Determine the type of immediate
      if (this.is16BitValue(operand)) {
        return 'D16';
      } else if (this.is8BitValue(operand)) {
        return 'D8';
      }
      // Constants starting with BIT_ are likely bit numbers (0-7)
      if (/^BIT_/.test(normalized)) {
        return 'D8';  // Bit number is 0-7, fits in immediate
      }
      // Other constants - could be 8 or 16 bit, try 8 first
      return 'D8';
    }

    // Check for memory address
    if (normalized.startsWith('[') && normalized.endsWith(']')) {
      const inner = normalized.slice(1, -1);
      // Check if it's a register indirect addressing mode - don't convert to [A16]
      const registerIndirect = ['HL', 'BC', 'DE', 'C', 'HL+', 'HL-'];
      if (registerIndirect.includes(inner)) {
        return normalized;  // Keep as [HL], [BC], [DE], [C], [HL+], [HL-]
      }
      // Memory access via constant (like [rLCDC]) - treat as 16-bit address
      if (/^[A-Z_][A-Z0-9_]*$/.test(inner)) {
        return '[A16]';
      }
      if (this.isImmediateValue(inner)) {
        if (inner.startsWith('$FF') || inner.startsWith('FF')) {
          return '[A8]';
        }
        return '[A16]';
      }
      // Any other bracketed expression (like [label + offset]) is a 16-bit address
      return '[A16]';
    }

    // Handle signed 8-bit relative (for JR)
    if (this.isRelativeAddress(operand)) {
      return 'R8';
    }

    return normalized;
  }

  private isImmediateValue(operand: string): boolean {
    // Check for various numeric formats
    return /^(\$[0-9A-Fa-f]+|%[01]+|&[0-7]+|\d+|"."|\w+)$/.test(operand) &&
           !this.isRegister(operand) &&
           !this.isCondition(operand);
  }

  private isRegister(operand: string): boolean {
    const registers = ['A', 'B', 'C', 'D', 'E', 'H', 'L', 'AF', 'BC', 'DE', 'HL', 'SP', 'PC'];
    return registers.includes(operand.toUpperCase());
  }

  private isCondition(operand: string): boolean {
    const conditions = ['Z', 'NZ', 'C', 'NC'];
    return conditions.includes(operand.toUpperCase());
  }

  private is16BitValue(operand: string): boolean {
    // If it has more than 2 hex digits or is a label, assume 16-bit
    const hexMatch = operand.match(/\$([0-9A-Fa-f]+)/);
    if (hexMatch && hexMatch[1].length > 2) {
      return true;
    }
    // Labels are typically 16-bit addresses
    if (/^[A-Za-z_]\w*$/.test(operand) && !this.isRegister(operand) && !this.isCondition(operand)) {
      return true;
    }
    return false;
  }

  private is8BitValue(operand: string): boolean {
    const hexMatch = operand.match(/\$([0-9A-Fa-f]+)/);
    if (hexMatch && hexMatch[1].length <= 2) {
      return true;
    }
    const decMatch = operand.match(/^(\d+)$/);
    if (decMatch && parseInt(decMatch[1]) <= 255) {
      return true;
    }
    return false;
  }

  private isRelativeAddress(operand: string): boolean {
    // Labels used in JR are relative addresses
    return /^\.?\w+$/.test(operand) && !this.isRegister(operand) && !this.isCondition(operand);
  }

  private findWithWildcard(mnemonic: string, operands: string[]): OpcodeInfo | null {
    // Try different operand type combinations
    const searchPatterns = this.generateSearchPatterns(mnemonic, operands);

    for (const pattern of searchPatterns) {
      const result = this.lookupMap.get(pattern);
      if (result) {
        return result;
      }
    }

    return null;
  }

  private generateSearchPatterns(mnemonic: string, operands: string[]): string[] {
    const patterns: string[] = [];

    if (operands.length === 0) {
      patterns.push(mnemonic);
    } else if (operands.length === 1) {
      const op = this.normalizeSearchOperand(operands[0]);
      patterns.push(`${mnemonic} ${op}`);
      patterns.push(`${mnemonic} D8`);
      patterns.push(`${mnemonic} D16`);
      patterns.push(`${mnemonic} A16`);  // For CALL, JP (address vs data)
      patterns.push(`${mnemonic} R8`);
      patterns.push(`${mnemonic} [A8]`);
      patterns.push(`${mnemonic} [A16]`);
      patterns.push(`${mnemonic} [HL]`);
    } else if (operands.length === 2) {
      const op1 = this.normalizeSearchOperand(operands[0]);
      const op2 = this.normalizeSearchOperand(operands[1]);
      patterns.push(`${mnemonic} ${op1},${op2}`);

      // Try with d8/d16/a16 substitutions
      patterns.push(`${mnemonic} ${op1},D8`);
      patterns.push(`${mnemonic} ${op1},D16`);
      patterns.push(`${mnemonic} ${op1},A16`);  // For conditional JP/CALL
      patterns.push(`${mnemonic} ${op1},R8`);
      patterns.push(`${mnemonic} ${op1},[A8]`);
      patterns.push(`${mnemonic} ${op1},[A16]`);
      patterns.push(`${mnemonic} [A8],${op2}`);
      patterns.push(`${mnemonic} [A16],${op2}`);
      patterns.push(`${mnemonic} [HL],D8`);

      // Special case for SP+r8
      if (op1 === 'HL' && op2.includes('SP')) {
        patterns.push(`${mnemonic} HL,SP+R8`);
      }
    }

    return patterns;
  }

  getOpcodeHex(info: OpcodeInfo, isCBPrefixed: boolean): string {
    if (isCBPrefixed) {
      return `CB ${info.opcode.toString(16).toUpperCase().padStart(2, '0')}`;
    }
    return info.opcode.toString(16).toUpperCase().padStart(2, '0');
  }

  isCBPrefixedInstruction(mnemonic: string): boolean {
    const cbInstructions = ['RLC', 'RRC', 'RL', 'RR', 'SLA', 'SRA', 'SWAP', 'SRL', 'BIT', 'RES', 'SET'];
    return cbInstructions.includes(mnemonic.toUpperCase());
  }
}

export const opcodeDatabase = new OpcodeDatabase();
