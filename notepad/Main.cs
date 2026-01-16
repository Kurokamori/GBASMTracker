using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GBZ80AsmMetrics.PluginInfrastructure;
using GBZ80AsmMetrics.Core;
using GBZ80AsmMetrics.Forms;

namespace GBZ80AsmMetrics
{
    /// <summary>
    /// Main plugin entry point for Notepad++ GB Z80 Assembly Metrics plugin
    /// </summary>
    class Main
    {
        #region Plugin Info
        internal const string PluginName = "GBZ80 ASM Metrics";
        #endregion

        #region Menu Items
        private static int _cmdToggleMetrics = 0;
        private static int _cmdToggleStartPoint = 1;
        private static int _cmdClearStartPoints = 2;
        private static int _cmdShowLineInfo = 3;
        private static int _cmdTogglePanel = 4;
        private static int _cmdRescanWorkspace = 5;
        private static int _cmdSettings = 6;
        private static int _cmdSeparator = 7;
        private static int _cmdAbout = 8;
        #endregion

        #region Fields
        private static MetricsEngine _metricsEngine;
        private static SettingsManager _settings;
        private static MetricsPanel _metricsPanel;
        private static bool _isMetricsEnabled = true;
        private static bool _isPanelRegistered = false;
        private static bool _isPanelVisible = false;
        private static Timer _updateTimer;
        private static string _lastDocContent = "";
        private static int _startPointMarker = 20; // Marker number for start point
        private static System.Collections.Generic.Dictionary<int, LineInfo> _cachedLineInfo = new System.Collections.Generic.Dictionary<int, LineInfo>();
        private static string _lastScannedFolder = "";
        private static FileSystemWatcher _fileWatcher;
        private static Timer _rescanTimer;
        #endregion

        #region Plugin Entry Points

        /// <summary>
        /// Initialize menu commands - called from setInfo
        /// </summary>
        internal static void CommandMenuInit()
        {
            // Initialize settings
            string configDir = GetPluginConfigDir();
            _settings = new SettingsManager(Path.Combine(configDir, "GBZ80AsmMetrics.ini"));
            _settings.Load();
            _isMetricsEnabled = _settings.Enabled;

            // Initialize metrics engine
            _metricsEngine = new MetricsEngine(_settings);

            // Setup timer for debounced updates
            _updateTimer = new Timer();
            _updateTimer.Interval = 150; // 150ms debounce
            _updateTimer.Tick += OnUpdateTimerTick;

            // Register menu commands
            PluginBase.SetCommand(0, "Toggle Metrics Display", ToggleMetricsDisplay);
            PluginBase.SetCommand(1, "Toggle Start Point", ToggleStartPoint,
                new ShortcutKey(true, false, true, Keys.M));
            PluginBase.SetCommand(2, "Clear All Start Points", ClearAllStartPoints);
            PluginBase.SetCommand(3, "Show Line Info", ShowLineInfo,
                new ShortcutKey(true, false, true, Keys.I));
            PluginBase.SetCommand(4, "Toggle Metrics Panel", ToggleMetricsPanel,
                new ShortcutKey(true, true, false, Keys.M));
            PluginBase.SetCommand(5, "Rescan Workspace", RescanWorkspace);
            PluginBase.SetCommand(6, "Settings...", ShowSettings);
            PluginBase.SetCommand(7, "-", null); // Separator
            PluginBase.SetCommand(8, "About", ShowAbout);
        }

        /// <summary>
        /// Set toolbar icons - called from beNotified on NPPN_TBMODIFICATION
        /// </summary>
        internal static void SetToolBarIcons()
        {
            // No toolbar icons for now
        }

        /// <summary>
        /// Plugin cleanup - called from beNotified on NPPN_SHUTDOWN
        /// </summary>
        internal static void PluginCleanUp()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _rescanTimer?.Stop();
            _rescanTimer?.Dispose();
            _fileWatcher?.Dispose();
            _metricsPanel?.Dispose();
        }

        /// <summary>
        /// Handle notifications - called from beNotified
        /// </summary>
        internal static void OnNotification(ScNotification notification)
        {
            switch (notification.Header.Code)
            {
                case (uint)NppMsg.NPPN_READY:
                    OnReady();
                    break;

                case (uint)NppMsg.NPPN_BUFFERACTIVATED:
                    OnBufferActivated();
                    break;

                case (uint)NppMsg.NPPN_FILESAVED:
                    if (_isMetricsEnabled && IsAssemblyFile())
                    {
                        QueueMetricsUpdate();
                    }
                    break;

                case (uint)SciMsg.SCN_MODIFIED:
                    if (_isMetricsEnabled && IsAssemblyFile())
                    {
                        QueueMetricsUpdate();
                    }
                    break;

                case (uint)SciMsg.SCN_UPDATEUI:
                    if (_isMetricsEnabled && IsAssemblyFile())
                    {
                        UpdatePanelForCurrentLine();
                    }
                    break;
            }
        }

        /// <summary>
        /// Called when Notepad++ is ready
        /// </summary>
        private static void OnReady()
        {
            // Setup marker for start point
            SetupMarkers();

            // Scan workspace for routine definitions
            if (IsAssemblyFile())
            {
                ScanWorkspaceForRoutines();
            }

            // Initial update
            if (_isMetricsEnabled && IsAssemblyFile())
            {
                QueueMetricsUpdate();
            }
        }

        #endregion

        #region Command Handlers

        private static void ToggleMetricsDisplay()
        {
            _isMetricsEnabled = !_isMetricsEnabled;
            _settings.Enabled = _isMetricsEnabled;
            _settings.Save();

            if (_isMetricsEnabled && IsAssemblyFile())
            {
                QueueMetricsUpdate();
            }
            else
            {
                ClearAllAnnotations();
            }

            UpdateMenuCheckmark();
        }

        private static void ToggleStartPoint()
        {
            if (!IsAssemblyFile()) return;

            int currentLine = GetCurrentLine();
            string filePath = GetCurrentFilePath();

            if (_metricsEngine.HasStartPoint(filePath) && _metricsEngine.GetStartPoint(filePath) == currentLine)
            {
                _metricsEngine.ClearStartPoint(filePath);
                ClearStartPointMarker(currentLine);
            }
            else
            {
                // Clear old marker if exists
                int? oldStartPoint = _metricsEngine.GetStartPoint(filePath);
                if (oldStartPoint.HasValue)
                {
                    ClearStartPointMarker(oldStartPoint.Value);
                }

                _metricsEngine.SetStartPoint(filePath, currentLine);
                SetStartPointMarker(currentLine);
            }

            if (_isMetricsEnabled)
            {
                _lastDocContent = ""; // Force refresh with new start point
                QueueMetricsUpdate();
            }
        }

        private static void ClearAllStartPoints()
        {
            _metricsEngine.ClearAllStartPoints();
            ClearAllStartPointMarkers();

            if (_isMetricsEnabled && IsAssemblyFile())
            {
                _lastDocContent = ""; // Force refresh after clearing start points
                QueueMetricsUpdate();
            }
        }

        private static void ShowLineInfo()
        {
            if (!IsAssemblyFile())
            {
                MessageBox.Show("Please open an assembly file (.asm, .s, .inc) to view line info.",
                    PluginName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int currentLine = GetCurrentLine();
            string lineText = GetLineText(currentLine);
            var lineInfo = _metricsEngine.GetLineInfo(lineText, currentLine, GetCurrentFilePath());

            if (lineInfo == null || !lineInfo.HasMetrics)
            {
                MessageBox.Show("No instruction information available for this line.",
                    PluginName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new LineInfoForm(lineInfo))
            {
                form.ShowDialog();
            }
        }

        private static void ToggleMetricsPanel()
        {
            try
            {
                if (_metricsPanel == null)
                {
                    _metricsPanel = new MetricsPanel();
                }

                if (!_isPanelRegistered)
                {
                    RegisterDockablePanel(_metricsPanel);
                    _isPanelRegistered = true;
                }

                if (_isPanelVisible)
                {
                    Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_DMMHIDE, 0, _metricsPanel.Handle);
                    _isPanelVisible = false;
                }
                else
                {
                    Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_DMMSHOW, 0, _metricsPanel.Handle);
                    _isPanelVisible = true;
                    UpdatePanelForCurrentLine();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling panel: {ex.Message}", PluginName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowSettings()
        {
            using (var form = new SettingsForm(_settings))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _settings.Save();
                    if (_isMetricsEnabled && IsAssemblyFile())
                    {
                        QueueMetricsUpdate();
                    }
                }
            }
        }

        private static void RescanWorkspace()
        {
            if (!IsAssemblyFile())
            {
                MessageBox.Show("Please open an assembly file (.asm, .s, .inc) first.",
                    PluginName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Force rescan by clearing the last scanned folder
            _lastScannedFolder = "";
            ScanWorkspaceForRoutines();

            int routineCount = _metricsEngine?.GetRoutineCount() ?? 0;
            MessageBox.Show($"Workspace scanned!\n\nProject root: {_lastScannedFolder}\nDocumented routines found: {routineCount}",
                PluginName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowAbout()
        {
            int routineCount = _metricsEngine?.GetRoutineCount() ?? 0;
            string scanInfo = string.IsNullOrEmpty(_lastScannedFolder)
                ? "No workspace scanned yet"
                : $"Workspace: {_lastScannedFolder}\nDocumented routines found: {routineCount}";

            MessageBox.Show(
                "GB Z80 Assembly Metrics Plugin\n" +
                "Version 1.0.0\n\n" +
                "Displays byte counts, cycle counts, and opcode information\n" +
                "for Game Boy Z80 assembly code.\n\n" +
                "Features:\n" +
                "- Inline metrics display (bytes/cycles)\n" +
                "- Cumulative counting from start point\n" +
                "- Detailed instruction information\n" +
                "- Macro and PREDEF support\n" +
                "- Full RGBDS syntax support\n" +
                "- Routine argument documentation\n\n" +
                "Keyboard Shortcuts:\n" +
                "Ctrl+Shift+M - Toggle start point\n" +
                "Ctrl+Shift+I - Show line info\n" +
                "Ctrl+Alt+M - Toggle metrics panel\n\n" +
                "--- Scan Status ---\n" + scanInfo,
                "About " + PluginName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #region Helper Methods

        private static string GetPluginConfigDir()
        {
            var sb = new StringBuilder(Win32.MAX_PATH);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETPLUGINSCONFIGDIR, Win32.MAX_PATH, sb);
            string configDir = sb.ToString();

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            return configDir;
        }

        /// <summary>
        /// Scan the project root (and subfolders) for assembly files to find routine definitions
        /// </summary>
        private static void ScanWorkspaceForRoutines()
        {
            string filePath = GetCurrentFilePath();
            if (string.IsNullOrEmpty(filePath))
                return;

            string folder = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(folder))
                return;

            // Find the project root by looking for common markers
            string projectRoot = FindProjectRoot(folder);

            // Only rescan if project root changed
            if (projectRoot == _lastScannedFolder)
                return;

            _lastScannedFolder = projectRoot;

            // Clear existing registries and scan
            _metricsEngine?.ClearAllRegistries();
            int fileCount = ScanDirectoryForRoutines(projectRoot);

            // Setup file watcher for this folder
            SetupFileWatcher(projectRoot);

            // Get routine count for debugging
            int routineCount = _metricsEngine?.GetRoutineCount() ?? 0;
            System.Diagnostics.Debug.WriteLine($"Scanned workspace: {projectRoot}, {fileCount} files, {routineCount} routines");
        }

        /// <summary>
        /// Find the project root by looking for common markers (.git, Makefile, etc.)
        /// </summary>
        private static string FindProjectRoot(string startDir)
        {
            string current = startDir;
            string lastValidDir = startDir;

            while (!string.IsNullOrEmpty(current))
            {
                try
                {
                    // Check for common project root markers
                    if (Directory.Exists(Path.Combine(current, ".git")) ||
                        File.Exists(Path.Combine(current, "Makefile")) ||
                        File.Exists(Path.Combine(current, "makefile")) ||
                        File.Exists(Path.Combine(current, ".gitignore")) ||
                        File.Exists(Path.Combine(current, "main.asm")) ||
                        File.Exists(Path.Combine(current, "game.asm")) ||
                        Directory.Exists(Path.Combine(current, "src")) ||
                        Directory.Exists(Path.Combine(current, "engine")) ||
                        Directory.Exists(Path.Combine(current, "home")) ||
                        Directory.Exists(Path.Combine(current, "data")))
                    {
                        return current;
                    }

                    lastValidDir = current;
                    string parent = Path.GetDirectoryName(current);

                    // Stop if we've reached the root
                    if (parent == current || string.IsNullOrEmpty(parent))
                        break;

                    current = parent;
                }
                catch
                {
                    break;
                }
            }

            // If no project root found, use the starting directory
            return lastValidDir;
        }

        /// <summary>
        /// Recursively scan a directory for assembly files
        /// </summary>
        /// <returns>Number of files scanned</returns>
        private static int ScanDirectoryForRoutines(string dirPath)
        {
            int count = 0;
            try
            {
                var entries = Directory.GetFileSystemEntries(dirPath);

                foreach (var entry in entries)
                {
                    try
                    {
                        if (Directory.Exists(entry))
                        {
                            // Skip common non-source directories
                            string dirName = Path.GetFileName(entry);
                            if (dirName != "node_modules" && dirName != ".git" &&
                                dirName != "build" && dirName != "dist" &&
                                dirName != "obj" && dirName != "bin")
                            {
                                count += ScanDirectoryForRoutines(entry);
                            }
                        }
                        else if (File.Exists(entry))
                        {
                            string ext = Path.GetExtension(entry).ToLowerInvariant();
                            if (ext == ".asm" || ext == ".s" || ext == ".inc")
                            {
                                string content = File.ReadAllText(entry);
                                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                                _metricsEngine?.ParseDocument(lines, Path.GetDirectoryName(entry), entry);
                                count++;
                            }
                        }
                    }
                    catch
                    {
                        // Skip files/dirs that can't be accessed
                    }
                }
            }
            catch
            {
                // Skip directories that can't be read
            }
            return count;
        }

        /// <summary>
        /// Setup file watcher to detect changes to assembly files
        /// </summary>
        private static void SetupFileWatcher(string folder)
        {
            // Dispose existing watcher
            _fileWatcher?.Dispose();

            try
            {
                _fileWatcher = new FileSystemWatcher(folder)
                {
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                };

                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged;
                _fileWatcher.Deleted += OnFileChanged;
                _fileWatcher.Renamed += OnFileRenamed;

                _fileWatcher.EnableRaisingEvents = true;

                // Setup rescan timer
                if (_rescanTimer == null)
                {
                    _rescanTimer = new Timer();
                    _rescanTimer.Interval = 500; // 500ms debounce
                    _rescanTimer.Tick += OnRescanTimerTick;
                }
            }
            catch
            {
                // Unable to create watcher, continue without it
            }
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext == ".asm" || ext == ".s" || ext == ".inc")
            {
                // Debounce rescan
                _rescanTimer?.Stop();
                _rescanTimer?.Start();
            }
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            string oldExt = Path.GetExtension(e.OldFullPath).ToLowerInvariant();
            string newExt = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (oldExt == ".asm" || oldExt == ".s" || oldExt == ".inc" ||
                newExt == ".asm" || newExt == ".s" || newExt == ".inc")
            {
                // Debounce rescan
                _rescanTimer?.Stop();
                _rescanTimer?.Start();
            }
        }

        private static void OnRescanTimerTick(object sender, EventArgs e)
        {
            _rescanTimer?.Stop();
            _lastScannedFolder = ""; // Force rescan
            ScanWorkspaceForRoutines();
        }

        private static IntPtr GetCurrentScintilla()
        {
            return PluginBase.GetCurrentScintilla();
        }

        private static string GetCurrentFilePath()
        {
            var sb = new StringBuilder(Win32.MAX_PATH);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETFULLCURRENTPATH, Win32.MAX_PATH, sb);
            return sb.ToString();
        }

        private static bool IsAssemblyFile()
        {
            string filePath = GetCurrentFilePath().ToLowerInvariant();
            return filePath.EndsWith(".asm") || filePath.EndsWith(".s") || filePath.EndsWith(".inc");
        }

        private static int GetCurrentLine()
        {
            IntPtr scintilla = GetCurrentScintilla();
            int pos = (int)Win32.SendMessage(scintilla, SciMsg.SCI_GETCURRENTPOS, 0, 0);
            return (int)Win32.SendMessage(scintilla, SciMsg.SCI_LINEFROMPOSITION, pos, 0);
        }

        private static int GetLineCount()
        {
            IntPtr scintilla = GetCurrentScintilla();
            return (int)Win32.SendMessage(scintilla, SciMsg.SCI_GETLINECOUNT, 0, 0);
        }

        private static string GetLineText(int lineNumber)
        {
            IntPtr scintilla = GetCurrentScintilla();
            int lineLength = (int)Win32.SendMessage(scintilla, SciMsg.SCI_LINELENGTH, lineNumber, 0);

            if (lineLength <= 0) return string.Empty;

            byte[] buffer = new byte[lineLength + 1];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Win32.SendMessage(scintilla, SciMsg.SCI_GETLINE, lineNumber, handle.AddrOfPinnedObject());
                return Encoding.UTF8.GetString(buffer, 0, lineLength).TrimEnd('\r', '\n', '\0');
            }
            finally
            {
                handle.Free();
            }
        }

        private static string GetAllText()
        {
            IntPtr scintilla = GetCurrentScintilla();
            int length = (int)Win32.SendMessage(scintilla, SciMsg.SCI_GETLENGTH, 0, 0);

            if (length <= 0) return string.Empty;

            byte[] buffer = new byte[length + 1];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Win32.SendMessage(scintilla, SciMsg.SCI_GETTEXT, length + 1, handle.AddrOfPinnedObject());
                return Encoding.UTF8.GetString(buffer, 0, length).TrimEnd('\0');
            }
            finally
            {
                handle.Free();
            }
        }

        private static void SetupMarkers()
        {
            IntPtr scintilla = GetCurrentScintilla();

            // Setup start point marker (yellow background)
            Win32.SendMessage(scintilla, SciMsg.SCI_MARKERDEFINE, _startPointMarker, (int)SciMsg.SC_MARK_BACKGROUND);
            Win32.SendMessage(scintilla, SciMsg.SCI_MARKERSETBACK, _startPointMarker, 0x00FFFF); // Yellow background
        }

        private static void SetStartPointMarker(int line)
        {
            IntPtr scintilla = GetCurrentScintilla();
            Win32.SendMessage(scintilla, SciMsg.SCI_MARKERADD, line, _startPointMarker);
        }

        private static void ClearStartPointMarker(int line)
        {
            IntPtr scintilla = GetCurrentScintilla();
            Win32.SendMessage(scintilla, SciMsg.SCI_MARKERDELETE, line, _startPointMarker);
        }

        private static void ClearAllStartPointMarkers()
        {
            IntPtr scintilla = GetCurrentScintilla();
            Win32.SendMessage(scintilla, SciMsg.SCI_MARKERDELETEALL, _startPointMarker, 0);
        }

        #endregion

        #region Metrics Display

        private static void QueueMetricsUpdate()
        {
            _updateTimer.Stop();
            _updateTimer.Start();
        }

        private static void OnUpdateTimerTick(object sender, EventArgs e)
        {
            _updateTimer.Stop();
            UpdateMetricsDisplay();
        }

        private static void UpdateMetricsDisplay()
        {
            if (!_isMetricsEnabled || !IsAssemblyFile()) return;

            try
            {
                IntPtr scintilla = GetCurrentScintilla();
                string filePath = GetCurrentFilePath();
                string content = GetAllText();

                // Skip if content hasn't changed
                if (content == _lastDocContent) return;
                _lastDocContent = content;

                // Parse document and calculate metrics
                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                string baseDir = Path.GetDirectoryName(filePath);

                // Parse document for macros
                _metricsEngine.ParseDocument(lines, baseDir);

                // Enable EOL annotations (appear at end of line, same line as code)
                Win32.SendMessage(scintilla, SciMsg.SCI_EOLANNOTATIONSETVISIBLE, (int)SciMsg.EOLANNOTATION_STANDARD, 0);

                // Setup EOL annotation style (style 40)
                Win32.SendMessage(scintilla, SciMsg.SCI_STYLESETFORE, 40, 0x808080); // Gray text
                Win32.SendMessage(scintilla, SciMsg.SCI_STYLESETITALIC, 40, 1);
                Win32.SendMessage(scintilla, SciMsg.SCI_EOLANNOTATIONSETSTYLEOFFSET, 40, 0);

                // Get start point for this file
                int? startPoint = _metricsEngine.GetStartPoint(filePath);
                int cumulativeBytes = 0;
                int cumulativeCycles = 0;
                bool counting = !startPoint.HasValue;

                // Clear cached line info
                _cachedLineInfo.Clear();

                // First pass: collect line data and find max line length for alignment
                var linesWithMetrics = new System.Collections.Generic.List<(int lineNumber, int lineLength, LineInfo info, bool counting)>();
                int maxLineLength = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    // Check for start point
                    if (startPoint.HasValue && i == startPoint.Value)
                    {
                        counting = true;
                    }

                    var lineInfo = _metricsEngine.GetLineInfo(lines[i], i, filePath);

                    if (lineInfo != null && lineInfo.HasMetrics)
                    {
                        if (counting)
                        {
                            cumulativeBytes += lineInfo.Bytes;
                            cumulativeCycles += lineInfo.Cycles;
                            lineInfo.CumulativeBytes = cumulativeBytes;
                            lineInfo.CumulativeCycles = cumulativeCycles;
                        }

                        // Cache the line info for panel use
                        _cachedLineInfo[i] = lineInfo;

                        // Track line length for alignment
                        int lineLength = lines[i].Length;
                        linesWithMetrics.Add((i, lineLength, lineInfo, counting));
                        if (lineLength > maxLineLength)
                        {
                            maxLineLength = lineLength;
                        }
                    }
                    else
                    {
                        ClearLineAnnotation(scintilla, i);
                    }
                }

                // Second pass: set annotations with aligned padding
                const int minPadding = 4; // Minimum spaces between line content and metrics

                foreach (var (lineNumber, lineLength, lineInfo, isCounting) in linesWithMetrics)
                {
                    string annotation = FormatAnnotation(lineInfo, isCounting);
                    int paddingSpaces = maxLineLength - lineLength + minPadding;
                    SetLineAnnotation(scintilla, lineNumber, annotation, paddingSpaces);
                }
            }
            catch (Exception ex)
            {
                // Silently fail - don't disrupt user experience
                System.Diagnostics.Debug.WriteLine($"Error updating metrics: {ex.Message}");
            }
        }

        private static string FormatAnnotation(LineInfo info, bool counting)
        {
            var parts = new System.Collections.Generic.List<string>();

            // Format: bytes | cumulative bytes | cycles | cumulative cycles
            if (_settings.ShowByteCount && info.Bytes > 0)
            {
                parts.Add($"{info.Bytes}B");
                if (_settings.ShowCumulative && counting)
                {
                    parts.Add($"{info.CumulativeBytes}B");
                }
            }

            if (_settings.ShowCycleCount && info.Cycles > 0)
            {
                parts.Add($"{info.Cycles}c");
                if (_settings.ShowCumulative && counting)
                {
                    parts.Add($"{info.CumulativeCycles}c");
                }
            }

            if (info.IsMacroCall)
            {
                parts.Add("[macro]");
            }

            if (info.IsPredefCall)
            {
                parts.Add("[predef]");
            }

            return string.Join(" | ", parts);
        }

        private static void SetLineAnnotation(IntPtr scintilla, int line, string text, int paddingSpaces = 4)
        {
            if (string.IsNullOrEmpty(text))
            {
                ClearLineAnnotation(scintilla, line);
                return;
            }

            // Add calculated padding before the annotation for visual separation and alignment
            string padding = new string(' ', paddingSpaces);
            string paddedText = padding + text;
            byte[] textBytes = Encoding.UTF8.GetBytes(paddedText + "\0");
            GCHandle handle = GCHandle.Alloc(textBytes, GCHandleType.Pinned);
            try
            {
                Win32.SendMessage(scintilla, SciMsg.SCI_EOLANNOTATIONSETTEXT, line, handle.AddrOfPinnedObject());
                Win32.SendMessage(scintilla, SciMsg.SCI_EOLANNOTATIONSETSTYLE, line, 40);
            }
            finally
            {
                handle.Free();
            }
        }

        private static void ClearLineAnnotation(IntPtr scintilla, int line)
        {
            Win32.SendMessage(scintilla, SciMsg.SCI_EOLANNOTATIONSETTEXT, line, IntPtr.Zero);
        }

        private static void ClearAllAnnotations()
        {
            IntPtr scintilla = GetCurrentScintilla();
            Win32.SendMessage(scintilla, SciMsg.SCI_EOLANNOTATIONCLEARALL, 0, 0);
        }

        #endregion

        #region Panel Support

        private static void RegisterDockablePanel(MetricsPanel panel)
        {
            NppTbData tbData = new NppTbData();
            tbData.hClient = panel.Handle;
            tbData.pszName = "GB Z80 Metrics";
            tbData.dlgID = _cmdTogglePanel;
            tbData.uMask = NppTbMsg.DWS_DF_CONT_RIGHT | NppTbMsg.DWS_ICONTAB;
            tbData.hIconTab = 0;
            tbData.pszModuleName = PluginName;

            IntPtr ptrData = Marshal.AllocHGlobal(Marshal.SizeOf(tbData));
            Marshal.StructureToPtr(tbData, ptrData, false);

            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_DMMREGASDCKDLG, 0, ptrData);

            Marshal.FreeHGlobal(ptrData);
        }

        private static void UpdatePanelForCurrentLine()
        {
            if (_metricsPanel == null || !_isPanelVisible) return;

            try
            {
                int currentLine = GetCurrentLine();

                // Use cached line info if available (has cumulative values)
                LineInfo lineInfo;
                if (_cachedLineInfo.TryGetValue(currentLine, out lineInfo))
                {
                    _metricsPanel.UpdateDisplay(lineInfo, currentLine);
                }
                else
                {
                    // Fall back to fresh calculation (no cumulative values)
                    string lineText = GetLineText(currentLine);
                    lineInfo = _metricsEngine.GetLineInfo(lineText, currentLine, GetCurrentFilePath());
                    _metricsPanel.UpdateDisplay(lineInfo, currentLine);
                }
            }
            catch
            {
                // Silently ignore errors
            }
        }

        #endregion

        #region Event Handlers

        private static void OnBufferActivated()
        {
            SetupMarkers();
            _lastDocContent = ""; // Force refresh

            if (IsAssemblyFile())
            {
                // Scan workspace for routine definitions if we're in a new folder
                ScanWorkspaceForRoutines();
            }

            if (_isMetricsEnabled && IsAssemblyFile())
            {
                // Restore start point marker if exists
                string filePath = GetCurrentFilePath();
                int? startPoint = _metricsEngine.GetStartPoint(filePath);
                if (startPoint.HasValue)
                {
                    SetStartPointMarker(startPoint.Value);
                }

                QueueMetricsUpdate();
            }
            else
            {
                ClearAllAnnotations();
            }
        }

        private static void UpdateMenuCheckmark()
        {
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_SETMENUITEMCHECK,
                PluginBase._funcItems.Items[_cmdToggleMetrics]._cmdID, _isMetricsEnabled ? 1 : 0);
        }

        #endregion
    }
}
