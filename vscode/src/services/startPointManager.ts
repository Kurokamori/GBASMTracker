import * as vscode from 'vscode';

export class StartPointManager {
  private startPoints: Map<string, number> = new Map();
  private _onDidChange = new vscode.EventEmitter<vscode.Uri>();
  readonly onDidChange = this._onDidChange.event;

  setStartPoint(uri: vscode.Uri, line: number): void {
    this.startPoints.set(uri.toString(), line);
    this._onDidChange.fire(uri);
  }

  clearStartPoint(uri: vscode.Uri): void {
    this.startPoints.delete(uri.toString());
    this._onDidChange.fire(uri);
  }

  getStartPoint(uri: vscode.Uri): number | undefined {
    return this.startPoints.get(uri.toString());
  }

  hasStartPoint(uri: vscode.Uri): boolean {
    return this.startPoints.has(uri.toString());
  }

  toggleStartPoint(uri: vscode.Uri, line: number): void {
    const current = this.getStartPoint(uri);
    if (current === line) {
      this.clearStartPoint(uri);
    } else {
      this.setStartPoint(uri, line);
    }
  }

  clearAll(): void {
    const uris = Array.from(this.startPoints.keys());
    this.startPoints.clear();
    uris.forEach(uriString => {
      this._onDidChange.fire(vscode.Uri.parse(uriString));
    });
  }

  dispose(): void {
    this._onDidChange.dispose();
  }
}
