import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { StartPointManager } from './services/startPointManager';
import { MetricsDecorationProvider } from './providers/metricsDecorationProvider';
import { parser, routineRegistry } from './parser/rgbdsParser';

let startPointManager: StartPointManager;
let decorationProvider: MetricsDecorationProvider;
let workspaceScanned = false;

// Scan all assembly files in the workspace to find routine definitions
async function scanWorkspaceForRoutines(): Promise<void> {
  const workspaceFolders = vscode.workspace.workspaceFolders;
  if (!workspaceFolders) return;

  // Clear existing routines before scanning
  parser.clearAll();

  for (const folder of workspaceFolders) {
    await scanDirectoryForRoutines(folder.uri.fsPath);
  }

  workspaceScanned = true;
  console.log(`Scanned workspace, found ${routineRegistry.getAll().length} routines with documentation`);
}

// Recursively scan a directory for assembly files
async function scanDirectoryForRoutines(dirPath: string): Promise<void> {
  try {
    const entries = fs.readdirSync(dirPath, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = path.join(dirPath, entry.name);

      if (entry.isDirectory()) {
        // Skip common non-source directories
        if (!['node_modules', '.git', 'build', 'dist', 'obj', 'bin'].includes(entry.name)) {
          await scanDirectoryForRoutines(fullPath);
        }
      } else if (entry.isFile()) {
        const ext = path.extname(entry.name).toLowerCase();
        if (['.asm', '.s', '.inc'].includes(ext)) {
          try {
            const content = fs.readFileSync(fullPath, 'utf8');
            const lines = content.split(/\r?\n/);
            parser.parseDocument(lines, path.dirname(fullPath), fullPath);
          } catch (e) {
            // Skip files that can't be read
          }
        }
      }
    }
  } catch (e) {
    // Skip directories that can't be read
  }
}

export function activate(context: vscode.ExtensionContext) {
  console.log('GB Assembly Metrics extension activated');

  // Initialize services
  startPointManager = new StartPointManager();
  decorationProvider = new MetricsDecorationProvider(startPointManager);

  // Scan workspace for routines on activation
  scanWorkspaceForRoutines();

  // Register commands
  context.subscriptions.push(
    vscode.commands.registerCommand('gbAsmMetrics.toggleStartPoint', () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && isGBZ80Document(editor.document)) {
        const line = editor.selection.active.line;
        startPointManager.toggleStartPoint(editor.document.uri, line);
      }
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand('gbAsmMetrics.clearAllStartPoints', () => {
      startPointManager.clearAll();
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand('gbAsmMetrics.toggleExpand', () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && isGBZ80Document(editor.document)) {
        const line = editor.selection.active.line;
        decorationProvider.toggleExpandLine(editor.document.uri, line);
      }
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand('gbAsmMetrics.toggleMetrics', () => {
      const config = vscode.workspace.getConfiguration('gbAsmMetrics');
      const current = config.get<boolean>('enabled', true);
      config.update('enabled', !current, vscode.ConfigurationTarget.Global);
    })
  );

  // Listen for start point changes
  context.subscriptions.push(
    startPointManager.onDidChange(() => {
      const editor = vscode.window.activeTextEditor;
      if (editor && isGBZ80Document(editor.document)) {
        decorationProvider.updateDecorations(editor);
      }
    })
  );

  // Listen for document changes
  context.subscriptions.push(
    vscode.workspace.onDidChangeTextDocument(event => {
      const editor = vscode.window.activeTextEditor;
      if (editor && editor.document === event.document && isGBZ80Document(event.document)) {
        decorationProvider.updateDecorations(editor);
      }
    })
  );

  // Listen for active editor changes
  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(editor => {
      if (editor && isGBZ80Document(editor.document)) {
        decorationProvider.updateDecorations(editor);
      }
    })
  );

  // Listen for configuration changes
  context.subscriptions.push(
    vscode.workspace.onDidChangeConfiguration(event => {
      if (event.affectsConfiguration('gbAsmMetrics')) {
        const editor = vscode.window.activeTextEditor;
        if (editor && isGBZ80Document(editor.document)) {
          decorationProvider.updateDecorations(editor);
        }
      }
    })
  );

  // Listen for selection changes (for gutter click detection)
  context.subscriptions.push(
    vscode.window.onDidChangeTextEditorSelection(event => {
      if (!isGBZ80Document(event.textEditor.document)) {
        return;
      }

      // Detect potential gutter click (single click at column 0)
      if (event.kind === vscode.TextEditorSelectionChangeKind.Mouse &&
          event.selections.length === 1) {
        const selection = event.selections[0];
        if (selection.isEmpty && selection.active.character === 0) {
          // This might be a gutter click - toggle start point
          // We use a small delay to distinguish from regular cursor placement
          setTimeout(() => {
            const currentSelection = event.textEditor.selection;
            if (currentSelection.isEmpty &&
                currentSelection.active.line === selection.active.line &&
                currentSelection.active.character === 0) {
              // Still at the same position, likely a gutter click
              // Note: This is a heuristic since VSCode doesn't provide direct gutter click events
            }
          }, 100);
        }
      }
    })
  );

  // Initial decoration update for active editor
  const activeEditor = vscode.window.activeTextEditor;
  if (activeEditor && isGBZ80Document(activeEditor.document)) {
    decorationProvider.updateDecorations(activeEditor);
  }

  // Register hover provider for detailed info
  context.subscriptions.push(
    vscode.languages.registerHoverProvider(
      { language: 'gbz80', scheme: 'file' },
      {
        provideHover(document, position) {
          return createHover(document, position);
        }
      }
    )
  );

  // Watch for assembly file changes to rescan workspace for routines
  const fileWatcher = vscode.workspace.createFileSystemWatcher('**/*.{asm,s,inc}');

  // Debounce rescan to avoid excessive scanning
  let rescanTimeout: NodeJS.Timeout | undefined;
  const debouncedRescan = () => {
    if (rescanTimeout) {
      clearTimeout(rescanTimeout);
    }
    rescanTimeout = setTimeout(() => {
      scanWorkspaceForRoutines();
    }, 500);
  };

  fileWatcher.onDidCreate(debouncedRescan);
  fileWatcher.onDidDelete(debouncedRescan);
  fileWatcher.onDidChange(debouncedRescan);

  context.subscriptions.push(fileWatcher);

  // Cleanup
  context.subscriptions.push({
    dispose: () => {
      startPointManager.dispose();
      decorationProvider.dispose();
    }
  });
}

function isGBZ80Document(document: vscode.TextDocument): boolean {
  // Check by language ID or file extension
  if (document.languageId === 'gbz80') {
    return true;
  }

  const ext = document.fileName.toLowerCase();
  return ext.endsWith('.asm') || ext.endsWith('.s') || ext.endsWith('.inc');
}

function createHover(document: vscode.TextDocument, position: vscode.Position): vscode.Hover | undefined {
  const { opcodeDatabase } = require('./opcodes/opcodeData');

  const line = document.lineAt(position.line);
  const parsed = parser.parseLine(line.text, position.line);

  if (!parsed.instruction) {
    return undefined;
  }

  const md = new vscode.MarkdownString();

  // Check if this is a call/jump instruction that references a documented routine
  const callInstructions = ['CALL', 'JP', 'JR', 'RST'];
  const isCallInstruction = callInstructions.includes(parsed.instruction.toUpperCase());

  if (isCallInstruction && parsed.operands.length > 0) {
    // Get the target label (last operand for conditional calls)
    const targetLabel = parsed.operands[parsed.operands.length - 1];

    // Check if this is a documented routine
    const routine = routineRegistry.get(targetLabel);
    if (routine && (routine.arguments.length > 0 || routine.description)) {
      md.appendMarkdown(`## ${targetLabel}\n\n`);

      if (routine.description) {
        md.appendMarkdown(`*${routine.description}*\n\n`);
      }

      if (routine.arguments.length > 0) {
        md.appendMarkdown(`### Arguments\n\n`);
        md.appendCodeblock(
          routine.arguments.map(arg => `${arg.register.toUpperCase()}: ${arg.description}`).join('\n'),
          'plaintext'
        );
        md.appendMarkdown(`\n`);
      }

      if (routine.filePath) {
        const relativePath = vscode.workspace.asRelativePath(routine.filePath);
        md.appendMarkdown(`---\n\n`);
        md.appendMarkdown(`*Defined in ${relativePath}:${routine.lineNumber + 1}*\n\n`);
      }
    }
  }

  // Check for memory references in operands (e.g., [wCurPartySpecies], [hSomething])
  for (const operand of parsed.operands) {
    // Match memory references like [label] or [label + offset]
    const memMatch = operand.match(/\[([a-zA-Z_]\w*)/);
    if (memMatch) {
      const labelName = memMatch[1];
      const routine = routineRegistry.get(labelName);
      if (routine && (routine.arguments.length > 0 || routine.description)) {
        md.appendMarkdown(`## ${labelName}\n\n`);

        if (routine.description) {
          md.appendMarkdown(`*${routine.description}*\n\n`);
        }

        if (routine.arguments.length > 0) {
          md.appendMarkdown(`### Details\n\n`);
          md.appendCodeblock(
            routine.arguments.map(arg => `${arg.register.toUpperCase()}: ${arg.description}`).join('\n'),
            'plaintext'
          );
          md.appendMarkdown(`\n`);
        }

        if (routine.filePath) {
          const relativePath = vscode.workspace.asRelativePath(routine.filePath);
          md.appendMarkdown(`---\n\n`);
          md.appendMarkdown(`*Defined in ${relativePath}:${routine.lineNumber + 1}*\n\n`);
        }
      }
    }
  }

  // Check if this is a predef call with a documented routine
  if (parsed.isPredefCall && parsed.operands.length > 0) {
    const targetLabel = parsed.operands[0];
    const routine = routineRegistry.get(targetLabel);
    if (routine && (routine.arguments.length > 0 || routine.description)) {
      md.appendMarkdown(`## ${targetLabel} (PREDEF)\n\n`);

      if (routine.description) {
        md.appendMarkdown(`*${routine.description}*\n\n`);
      }

      if (routine.arguments.length > 0) {
        md.appendMarkdown(`### Arguments\n\n`);
        md.appendCodeblock(
          routine.arguments.map(arg => `${arg.register.toUpperCase()}: ${arg.description}`).join('\n'),
          'plaintext'
        );
        md.appendMarkdown(`\n`);
      }

      if (routine.filePath) {
        const relativePath = vscode.workspace.asRelativePath(routine.filePath);
        md.appendMarkdown(`---\n\n`);
        md.appendMarkdown(`*Defined in ${relativePath}:${routine.lineNumber + 1}*\n\n`);
      }
    }
  }

  // Get opcode info for regular instructions
  const metrics = decorationProvider.getLineMetrics(document.uri, position.line);
  const opcode = metrics?.opcode;

  if (opcode) {
    const isCB = opcodeDatabase.isCBPrefixedInstruction(parsed.instruction);
    const hexBytes = opcodeDatabase.getOpcodeHex(opcode, isCB);

    // Add separator if we already have routine info
    if (md.value.length > 0) {
      md.appendMarkdown(`---\n\n`);
    }

    md.appendMarkdown(`## ${parsed.instruction} ${parsed.operands.join(', ')}\n\n`);
    md.appendMarkdown(`**Opcode:** \`${hexBytes}\`\n\n`);
    md.appendMarkdown(`**Size:** ${opcode.bytes} byte(s)\n\n`);

    if (opcode.cycles.length > 1) {
      md.appendMarkdown(`**Cycles:** ${opcode.cycles[0]} (branch taken) / ${opcode.cycles[1]} (not taken)\n\n`);
    } else {
      md.appendMarkdown(`**Cycles:** ${opcode.cycles[0]}\n\n`);
    }

    md.appendMarkdown(`### Flags Affected\n\n`);
    md.appendMarkdown(`| Z | N | H | C |\n`);
    md.appendMarkdown(`|:---:|:---:|:---:|:---:|\n`);
    md.appendMarkdown(`| ${opcode.flags.Z} | ${opcode.flags.N} | ${opcode.flags.H} | ${opcode.flags.C} |\n\n`);

    md.appendMarkdown(`---\n\n`);
    md.appendMarkdown(`*Z = Zero, N = Subtract, H = Half-carry, C = Carry*\n\n`);
    md.appendMarkdown(`*- = unchanged, 0 = reset, 1 = set, letter = affected*`);
  }

  // Return hover only if we have content
  if (md.value.length > 0) {
    return new vscode.Hover(md);
  }

  return undefined;
}

export function deactivate() {
  console.log('GB Assembly Metrics extension deactivated');
}
