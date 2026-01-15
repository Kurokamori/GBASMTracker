import * as vscode from 'vscode';
import { parser, macroRegistry } from '../parser/rgbdsParser';
import { opcodeDatabase } from '../opcodes/opcodeData';
import { StartPointManager } from '../services/startPointManager';
import { LineMetrics, OpcodeInfo, MacroDefinition } from '../parser/types';

export class MetricsDecorationProvider {
  private metricsDecorationType: vscode.TextEditorDecorationType;
  private startPointDecorationType: vscode.TextEditorDecorationType;
  private expandedDecorationType: vscode.TextEditorDecorationType;
  private expandedLines: Map<string, Set<number>> = new Map();
  private debounceTimer: NodeJS.Timeout | undefined;
  private lineMetricsCache: Map<string, Map<number, LineMetrics>> = new Map();

  constructor(private startPointManager: StartPointManager) {
    // Inline metrics decoration (right side)
    this.metricsDecorationType = vscode.window.createTextEditorDecorationType({
      after: {
        margin: '0 0 0 2em',
      },
      rangeBehavior: vscode.DecorationRangeBehavior.ClosedOpen
    });

    // Start point line decoration
    this.startPointDecorationType = vscode.window.createTextEditorDecorationType({
      backgroundColor: new vscode.ThemeColor('editor.findMatchHighlightBackground'),
      isWholeLine: true,
      gutterIconPath: vscode.Uri.file(''),
      overviewRulerColor: new vscode.ThemeColor('editorOverviewRuler.findMatchForeground'),
      overviewRulerLane: vscode.OverviewRulerLane.Center
    });

    // Expanded details decoration
    this.expandedDecorationType = vscode.window.createTextEditorDecorationType({
      after: {
        margin: '0 0 0 1em',
        fontStyle: 'italic',
      }
    });
  }

  updateDecorations(editor: vscode.TextEditor): void {
    // Debounce rapid updates
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }

    this.debounceTimer = setTimeout(() => {
      this.doUpdateDecorations(editor);
    }, 100);
  }

  private doUpdateDecorations(editor: vscode.TextEditor): void {
    const document = editor.document;
    const config = vscode.workspace.getConfiguration('gbAsmMetrics');

    if (!config.get<boolean>('enabled', true)) {
      editor.setDecorations(this.metricsDecorationType, []);
      editor.setDecorations(this.startPointDecorationType, []);
      editor.setDecorations(this.expandedDecorationType, []);
      return;
    }

    // Parse the entire document to extract macro definitions first
    // Pass the document's directory so INCLUDE paths can be resolved
    const allLines: string[] = [];
    for (let i = 0; i < document.lineCount; i++) {
      allLines.push(document.lineAt(i).text);
    }
    const path = require('path');
    const baseDir = path.dirname(document.uri.fsPath);
    parser.parseDocument(allLines, baseDir);

    const showBytes = config.get<boolean>('showByteCount', true);
    const showCycles = config.get<boolean>('showCycleCount', true);
    const showCumulative = config.get<boolean>('showCumulative', true);
    const assumeBranchTaken = config.get<boolean>('assumeBranchTaken', true);
    const predefBytes = config.get<number>('predefBytes', 8);
    const predefCycles = config.get<number>('predefCycles', 44);
    const predefJumpBytes = config.get<number>('predefJumpBytes', 8);
    const predefJumpCycles = config.get<number>('predefJumpCycles', 36);

    const startPointDecorations: vscode.DecorationOptions[] = [];
    const expandedDecorations: vscode.DecorationOptions[] = [];

    const startPoint = this.startPointManager.getStartPoint(document.uri);
    const expandedSet = this.expandedLines.get(document.uri.toString()) || new Set();

    let cumulativeBytes = 0;
    let cumulativeCycles = 0;
    let counting = startPoint === undefined;
    let inMacroDefinition = false;

    // Cache for this document
    const docCache = new Map<number, LineMetrics>();
    this.lineMetricsCache.set(document.uri.toString(), docCache);

    // First pass: collect line data and find max line length for alignment
    interface LineData {
      lineNumber: number;
      lineLength: number;
      bytes: number;
      cycles: number;
      cumulativeBytes: number;
      cumulativeCycles: number;
      opcode: OpcodeInfo | null;
      macroDef: MacroDefinition | undefined;
      isPredef: boolean;
      predefType: 'predef' | 'predef_jump' | undefined;
      counting: boolean;
      parsed: ReturnType<typeof parser.parseLine>;
    }
    const linesWithMetrics: LineData[] = [];
    let maxLineLength = 0;

    for (let i = 0; i < document.lineCount; i++) {
      const line = document.lineAt(i);
      const parsed = parser.parseLine(line.text, i);

      // Track if we're inside a macro definition (don't count those lines)
      if (parsed.isDirective && parsed.instruction === 'MACRO') {
        inMacroDefinition = true;
      }
      if (parsed.isDirective && parsed.instruction === 'ENDM') {
        inMacroDefinition = false;
        continue;
      }
      if (inMacroDefinition) {
        continue; // Skip lines inside macro definitions
      }

      // Check if we've reached the start point
      if (startPoint !== undefined && i === startPoint) {
        counting = true;
        startPointDecorations.push({
          range: new vscode.Range(i, 0, i, line.text.length),
        });
      }

      let bytes = 0;
      let cycles = 0;
      let opcode: OpcodeInfo | null = null;
      let macroDef: MacroDefinition | undefined = undefined;
      let isPredef = false;
      let predefType: 'predef' | 'predef_jump' | undefined = undefined;

      if (parsed.isPredefCall) {
        // Predef call - use configured bytes/cycles
        isPredef = true;
        predefType = parsed.predefType;
        if (parsed.predefType === 'predef_jump') {
          bytes = predefJumpBytes;
          cycles = predefJumpCycles;
        } else {
          bytes = predefBytes;
          cycles = predefCycles;
        }
      } else if (parsed.isMacroCall && parsed.macroName) {
        // Macro call - get metrics from macro definition
        macroDef = macroRegistry.get(parsed.macroName);
        if (macroDef) {
          bytes = macroDef.bytes;
          cycles = assumeBranchTaken ? macroDef.cycles[0] : (macroDef.cycles[macroDef.cycles.length - 1]);
        }
      } else if (parsed.instruction && !parsed.isDirective) {
        opcode = opcodeDatabase.lookup(parsed.instruction, parsed.operands);
        if (opcode) {
          bytes = opcode.bytes;
          cycles = assumeBranchTaken ? opcode.cycles[0] : (opcode.cycles[1] ?? opcode.cycles[0]);
        }
      } else if (parsed.isDirective && parsed.directiveBytes !== undefined) {
        bytes = parsed.directiveBytes;
      }

      if (counting && (bytes > 0 || cycles > 0)) {
        cumulativeBytes += bytes;
        cumulativeCycles += cycles;
      }

      // Store metrics in cache
      if (bytes > 0 || cycles > 0 || opcode || macroDef) {
        docCache.set(i, {
          bytes,
          cycles,
          cumulativeBytes: counting ? cumulativeBytes : 0,
          cumulativeCycles: counting ? cumulativeCycles : 0,
          opcode: opcode || undefined
        });
      }

      // Collect lines with metrics for second pass
      if (bytes > 0 || cycles > 0) {
        linesWithMetrics.push({
          lineNumber: i,
          lineLength: line.text.length,
          bytes,
          cycles,
          cumulativeBytes,
          cumulativeCycles,
          opcode,
          macroDef,
          isPredef,
          predefType,
          counting,
          parsed
        });
        if (line.text.length > maxLineLength) {
          maxLineLength = line.text.length;
        }
      }
    }

    // Second pass: create decorations with aligned padding
    const metricsDecorations: vscode.DecorationOptions[] = [];
    const minPadding = 4; // Minimum spaces between line content and metrics

    for (const lineData of linesWithMetrics) {
      const line = document.lineAt(lineData.lineNumber);

      // Build metrics string
      const parts: string[] = [];

      if (showBytes) {
        parts.push(`${lineData.bytes}B`);
        if (showCumulative && lineData.counting) {
          parts.push(`${lineData.cumulativeBytes}B`);
        }
      }

      if (showCycles && lineData.cycles > 0) {
        parts.push(`${lineData.cycles}c`);
        if (showCumulative && lineData.counting) {
          parts.push(`${lineData.cumulativeCycles}c`);
        }
      }

      // Add macro indicator
      if (lineData.macroDef) {
        parts.push(`[macro]`);
      }

      // Add predef indicator
      if (lineData.isPredef) {
        parts.push(`[predef]`);
      }

      if (parts.length > 0) {
        const metricsText = parts.join(' | ');
        const color = lineData.counting
          ? new vscode.ThemeColor('editorCodeLens.foreground')
          : new vscode.ThemeColor('disabledForeground');

        // Calculate padding to align all metrics
        const paddingSpaces = maxLineLength - lineData.lineLength + minPadding;
        const padding = ' '.repeat(paddingSpaces);

        metricsDecorations.push({
          range: new vscode.Range(lineData.lineNumber, line.text.length, lineData.lineNumber, line.text.length),
          renderOptions: {
            after: {
              contentText: `${padding}${metricsText}`,
              color: color,
              fontStyle: lineData.counting ? 'normal' : 'italic'
            }
          }
        });
      }

      // Handle expanded details for opcodes
      if (expandedSet.has(lineData.lineNumber) && lineData.opcode) {
        const isCB = opcodeDatabase.isCBPrefixedInstruction(lineData.parsed.instruction!);
        const hexBytes = opcodeDatabase.getOpcodeHex(lineData.opcode, isCB);
        const flags = `Z:${lineData.opcode.flags.Z} N:${lineData.opcode.flags.N} H:${lineData.opcode.flags.H} C:${lineData.opcode.flags.C}`;

        let cycleInfo = `${lineData.opcode.cycles[0]}c`;
        if (lineData.opcode.cycles.length > 1) {
          cycleInfo = `${lineData.opcode.cycles[0]}c taken / ${lineData.opcode.cycles[1]}c not taken`;
        }

        const expandedText = `[${hexBytes}] ${flags} | ${cycleInfo}`;

        expandedDecorations.push({
          range: new vscode.Range(lineData.lineNumber, line.text.length, lineData.lineNumber, line.text.length),
          renderOptions: {
            after: {
              contentText: `\n    ${expandedText}`,
              color: new vscode.ThemeColor('descriptionForeground'),
              fontStyle: 'italic'
            }
          }
        });
      }

      // Handle expanded details for macros
      if (expandedSet.has(lineData.lineNumber) && lineData.macroDef) {
        let cycleInfo = `${lineData.macroDef.cycles[0]}c`;
        if (lineData.macroDef.cycles.length > 1) {
          cycleInfo = `${lineData.macroDef.cycles[0]}c max / ${lineData.macroDef.cycles[1]}c min`;
        }

        const expandedText = `MACRO ${lineData.macroDef.name}: ${lineData.macroDef.bytes}B | ${cycleInfo} | ${lineData.macroDef.instructions.length} instructions`;

        expandedDecorations.push({
          range: new vscode.Range(lineData.lineNumber, line.text.length, lineData.lineNumber, line.text.length),
          renderOptions: {
            after: {
              contentText: `\n    ${expandedText}`,
              color: new vscode.ThemeColor('descriptionForeground'),
              fontStyle: 'italic'
            }
          }
        });
      }

      // Handle expanded details for predef calls
      if (expandedSet.has(lineData.lineNumber) && lineData.isPredef) {
        const funcName = lineData.parsed.operands.length > 0 ? lineData.parsed.operands[0] : 'unknown';
        const typeLabel = lineData.predefType === 'predef_jump' ? 'PREDEF_JUMP' : 'PREDEF';
        const expandedText = `${typeLabel} ${funcName}: ${lineData.bytes}B | ${lineData.cycles}c (ld a,BANK + ld hl,addr + ${lineData.predefType === 'predef_jump' ? 'jp' : 'call'})`;

        expandedDecorations.push({
          range: new vscode.Range(lineData.lineNumber, line.text.length, lineData.lineNumber, line.text.length),
          renderOptions: {
            after: {
              contentText: `\n    ${expandedText}`,
              color: new vscode.ThemeColor('descriptionForeground'),
              fontStyle: 'italic'
            }
          }
        });
      }
    }

    editor.setDecorations(this.metricsDecorationType, metricsDecorations);
    editor.setDecorations(this.startPointDecorationType, startPointDecorations);
    editor.setDecorations(this.expandedDecorationType, expandedDecorations);
  }

  toggleExpandLine(uri: vscode.Uri, line: number): void {
    const key = uri.toString();
    let expandedSet = this.expandedLines.get(key);

    if (!expandedSet) {
      expandedSet = new Set();
      this.expandedLines.set(key, expandedSet);
    }

    if (expandedSet.has(line)) {
      expandedSet.delete(line);
    } else {
      expandedSet.add(line);
    }

    // Trigger re-render
    const editor = vscode.window.activeTextEditor;
    if (editor && editor.document.uri.toString() === key) {
      this.updateDecorations(editor);
    }
  }

  isLineExpanded(uri: vscode.Uri, line: number): boolean {
    const expandedSet = this.expandedLines.get(uri.toString());
    return expandedSet?.has(line) ?? false;
  }

  getLineMetrics(uri: vscode.Uri, line: number): LineMetrics | undefined {
    return this.lineMetricsCache.get(uri.toString())?.get(line);
  }

  dispose(): void {
    this.metricsDecorationType.dispose();
    this.startPointDecorationType.dispose();
    this.expandedDecorationType.dispose();
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
  }
}
