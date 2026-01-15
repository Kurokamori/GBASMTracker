import * as vscode from 'vscode';
import { StartPointManager } from './services/startPointManager';
import { MetricsDecorationProvider } from './providers/metricsDecorationProvider';

let startPointManager: StartPointManager;
let decorationProvider: MetricsDecorationProvider;

export function activate(context: vscode.ExtensionContext) {
  console.log('GB Assembly Metrics extension activated');

  // Initialize services
  startPointManager = new StartPointManager();
  decorationProvider = new MetricsDecorationProvider(startPointManager);

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
  const metrics = decorationProvider.getLineMetrics(document.uri, position.line);
  if (!metrics?.opcode) {
    return undefined;
  }

  const opcode = metrics.opcode;
  const { parser } = require('./parser/rgbdsParser');
  const { opcodeDatabase } = require('./opcodes/opcodeData');

  const line = document.lineAt(position.line);
  const parsed = parser.parseLine(line.text, position.line);

  if (!parsed.instruction) {
    return undefined;
  }

  const isCB = opcodeDatabase.isCBPrefixedInstruction(parsed.instruction);
  const hexBytes = opcodeDatabase.getOpcodeHex(opcode, isCB);

  const md = new vscode.MarkdownString();
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

  return new vscode.Hover(md);
}

export function deactivate() {
  console.log('GB Assembly Metrics extension deactivated');
}
