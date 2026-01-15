using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GBZ80AsmMetrics.Core
{
    /// <summary>
    /// Manages plugin settings using INI file format
    /// </summary>
    public class SettingsManager
    {
        private readonly string _iniPath;

        #region Win32 API for INI files
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
        #endregion

        // Settings properties
        public bool Enabled { get; set; } = true;
        public bool ShowByteCount { get; set; } = true;
        public bool ShowCycleCount { get; set; } = true;
        public bool ShowCumulative { get; set; } = true;
        public bool AssumeBranchTaken { get; set; } = true;
        public int PredefBytes { get; set; } = 8;
        public int PredefCycles { get; set; } = 44;
        public int PredefJumpBytes { get; set; } = 8;
        public int PredefJumpCycles { get; set; } = 36;

        public SettingsManager(string iniPath)
        {
            _iniPath = iniPath;
        }

        /// <summary>
        /// Load settings from INI file
        /// </summary>
        public void Load()
        {
            if (!File.Exists(_iniPath))
            {
                // Create default settings file
                Save();
                return;
            }

            Enabled = GetBool("General", "Enabled", true);
            ShowByteCount = GetBool("Display", "ShowByteCount", true);
            ShowCycleCount = GetBool("Display", "ShowCycleCount", true);
            ShowCumulative = GetBool("Display", "ShowCumulative", true);
            AssumeBranchTaken = GetBool("Calculation", "AssumeBranchTaken", true);
            PredefBytes = GetInt("Predef", "PredefBytes", 8);
            PredefCycles = GetInt("Predef", "PredefCycles", 44);
            PredefJumpBytes = GetInt("Predef", "PredefJumpBytes", 8);
            PredefJumpCycles = GetInt("Predef", "PredefJumpCycles", 36);
        }

        /// <summary>
        /// Save settings to INI file
        /// </summary>
        public void Save()
        {
            SetBool("General", "Enabled", Enabled);
            SetBool("Display", "ShowByteCount", ShowByteCount);
            SetBool("Display", "ShowCycleCount", ShowCycleCount);
            SetBool("Display", "ShowCumulative", ShowCumulative);
            SetBool("Calculation", "AssumeBranchTaken", AssumeBranchTaken);
            SetInt("Predef", "PredefBytes", PredefBytes);
            SetInt("Predef", "PredefCycles", PredefCycles);
            SetInt("Predef", "PredefJumpBytes", PredefJumpBytes);
            SetInt("Predef", "PredefJumpCycles", PredefJumpCycles);
        }

        #region INI File Helpers

        private string GetString(string section, string key, string defaultValue)
        {
            var sb = new StringBuilder(512);
            GetPrivateProfileString(section, key, defaultValue, sb, sb.Capacity, _iniPath);
            return sb.ToString();
        }

        private void SetString(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, _iniPath);
        }

        private bool GetBool(string section, string key, bool defaultValue)
        {
            string value = GetString(section, key, defaultValue ? "1" : "0");
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private void SetBool(string section, string key, bool value)
        {
            SetString(section, key, value ? "1" : "0");
        }

        private int GetInt(string section, string key, int defaultValue)
        {
            string value = GetString(section, key, defaultValue.ToString());
            if (int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }

        private void SetInt(string section, string key, int value)
        {
            SetString(section, key, value.ToString());
        }

        #endregion
    }
}
