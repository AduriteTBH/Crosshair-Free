using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace CrosshairFree.Models
{
    public enum CrosshairStyle
    {
        CrossAndDot = 0,
        ShotgunCircle = 1,
        ShotgunQuadrant = 2,
        ShotgunOctagon = 3,
        ShotgunDoubleRing = 4,
        ShotgunDiamondBloom = 5,
        ShotgunCrossDots = 6,
        ShotgunTriBloom = 7,
        ClassicCross = 8,
        DotOnly = 9,
        CrossAndCircle = 10,
        TStyle = 11,
        TacticalChevron = 12,
        Diamond = 13,
        BoxSquare = 14,
        TriPoint = 15,
        DotWithCircle = 16,
        XCross = 17,
        HollowSquare = 18,
        ApexTriDot = 19,
        ValorantClassic = 20,
        CyberDot = 21,
        SniperCrosshair = 22,
        Heart = 23,
        ShotgunCrossRing = 24,
        ShotgunHexagon = 25,
        Cs2Precision = 26,
        OverwatchTriTick = 27,
        DoubleChevron = 28,
        Bullseye = 29
    }

    public class CrosshairConfig
    {
        public string Name { get; set; } = "Assault Rifle (AR)";
        public string KeyLabel { get; set; } = "2 / F";
        
        public CrosshairStyle Style { get; set; } = CrosshairStyle.CrossAndDot;
        public double Size { get; set; } = 10;
        public double Thickness { get; set; } = 2;
        public double Gap { get; set; } = 5;
        
        public string ColorHex { get; set; } = "#00FF88";
        public bool HasOutline { get; set; } = true;
        public string OutlineColorHex { get; set; } = "#000000";
        public double OutlineThickness { get; set; } = 1.0;
        
        public bool HasCenterDot { get; set; } = true;
        public double CenterDotSize { get; set; } = 2.5;
        
        public double Opacity { get; set; } = 1.0;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;

        public CrosshairConfig Clone()
        {
            return (CrosshairConfig)MemberwiseClone();
        }
    }

    public class KeybindConfig
    {
        public List<int> ArKeys { get; set; } = new List<int>
        {
            0x32, 0x62, // '2', NumPad 2
            0x46,       // 'F'
            0x10, 0xA0, 0xA1, // Shift, LShift, RShift
            0x31, 0x61, // '1', NumPad 1
            0x34, 0x64, // '4', NumPad 4
            0x35, 0x65, // '5', NumPad 5
            0x36, 0x66, // '6', NumPad 6
            0x51, 0x5A, 0x58, 0x43 // Q, Z, X, C
        };

        public List<int> ShotgunKeys { get; set; } = new List<int>
        {
            0x33, 0x63 // '3', NumPad 3
        };

        public int ToggleOverlayKey { get; set; } = 0x77; // F8

        public KeybindConfig Clone()
        {
            return new KeybindConfig
            {
                ArKeys = new List<int>(ArKeys),
                ShotgunKeys = new List<int>(ShotgunKeys),
                ToggleOverlayKey = ToggleOverlayKey
            };
        }
    }

    public class AppSettings
    {
        public bool AlwaysOnTop { get; set; } = true;
        public bool SystemTray { get; set; } = true;
        public KeybindConfig Keybinds { get; set; } = new KeybindConfig();
        public List<CrosshairConfig> Profiles { get; set; } = new List<CrosshairConfig>();
    }

    public static class SettingsManager
    {
        public static AppSettings LoadSettings()
        {
            // 1. Try local settings.json in the same folder as .exe
            try
            {
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(localPath))
                {
                    string json = File.ReadAllText(localPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null && settings.Profiles != null && settings.Profiles.Count >= 2)
                    {
                        if (settings.Keybinds == null) settings.Keybinds = new KeybindConfig();
                        return settings;
                    }
                }
            }
            catch { }

            // 2. Try AppData roaming folder fallback
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appDataPath = Path.Combine(appData, "CrosshairFree", "settings.json");
                if (File.Exists(appDataPath))
                {
                    string json = File.ReadAllText(appDataPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null && settings.Profiles != null && settings.Profiles.Count >= 2)
                    {
                        if (settings.Keybinds == null) settings.Keybinds = new KeybindConfig();
                        return settings;
                    }
                }
            }
            catch { }

            // 3. Fallback to fresh defaults
            return new AppSettings
            {
                AlwaysOnTop = true,
                Keybinds = new KeybindConfig(),
                Profiles = CrosshairPresets.GetDefaultWeaponProfiles()
            };
        }

        public static void SaveSettings(AppSettings settings)
        {
            if (settings == null) return;

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);

            // 1. Save directly next to .exe
            try
            {
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                File.WriteAllText(localPath, json);
            }
            catch { }

            // 2. Dual-save to AppData as bulletproof backup
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "CrosshairFree");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                string appDataPath = Path.Combine(folder, "settings.json");
                File.WriteAllText(appDataPath, json);
            }
            catch { }
        }
    }

    public static class CrosshairPresets
    {
        public static List<CrosshairConfig> GetDefaultWeaponProfiles()
        {
            return new List<CrosshairConfig>
            {
                new CrosshairConfig
                {
                    Name = "Assault Rifle (AR)",
                    KeyLabel = "2 / F",
                    Style = CrosshairStyle.CrossAndDot,
                    Size = 10,
                    Thickness = 2,
                    Gap = 5,
                    ColorHex = "#00FF88",
                    HasOutline = true,
                    HasCenterDot = true,
                    CenterDotSize = 2.5,
                    Opacity = 1.0
                },
                new CrosshairConfig
                {
                    Name = "Shotgun",
                    KeyLabel = "KEY 3",
                    Style = CrosshairStyle.ShotgunCircle,
                    Size = 16,
                    Thickness = 2,
                    Gap = 0,
                    ColorHex = "#FF3366",
                    HasOutline = true,
                    HasCenterDot = true,
                    CenterDotSize = 3.0,
                    Opacity = 1.0
                }
            };
        }
    }

    public static class KeybindHelper
    {
        public static string GetKeyName(int vkCode)
        {
            if (vkCode >= 0x30 && vkCode <= 0x39) return ((char)vkCode).ToString();
            if (vkCode >= 0x41 && vkCode <= 0x5A) return ((char)vkCode).ToString();
            if (vkCode >= 0x60 && vkCode <= 0x69) return $"Num {vkCode - 0x60}";
            if (vkCode >= 0x70 && vkCode <= 0x87) return $"F{vkCode - 0x6F}";

            return vkCode switch
            {
                0x10 => "Shift",
                0xA0 => "L-Shift",
                0xA1 => "R-Shift",
                0x11 => "Ctrl",
                0xA2 => "L-Ctrl",
                0xA3 => "R-Ctrl",
                0x12 => "Alt",
                0xA4 => "L-Alt",
                0xA5 => "R-Alt",
                0x20 => "Space",
                0x09 => "Tab",
                0x14 => "Caps Lock",
                0x08 => "Backspace",
                0x0D => "Enter",
                0x1B => "Esc",
                0x2D => "Insert",
                0x2E => "Delete",
                0x24 => "Home",
                0x23 => "End",
                0x21 => "Page Up",
                0x22 => "Page Down",
                0xC0 => "` (Tilde)",
                0xBA => ";",
                0xDE => "'",
                0xBC => ",",
                0xBE => ".",
                0xBF => "/",
                0xDB => "[",
                0xDD => "]",
                0xDC => "\\",
                0xBD => "-",
                0xBB => "=",
                _ => $"VK_0x{vkCode:X2}"
            };
        }
    }
}
