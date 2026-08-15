using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CrosshairFree.Core;
using CrosshairFree.Models;
using CrosshairFree.Rendering;

namespace CrosshairFree
{
    public partial class MainWindow : Window
    {
        private CrosshairOverlayWindow? _overlay;
        private List<CrosshairConfig> _profiles;
        private KeybindConfig _keybinds;
        private int _activeProfileIndex = 0; // 0 = AR, 1 = Shotgun
        private bool _isUpdatingUi = true;

        private IntPtr _keyboardHookId = IntPtr.Zero;
        private NativeWin32.LowLevelKeyboardProc _keyboardHookProc;
        private DispatcherTimer? _memTrimTimer;

        public MainWindow()
        {
            _isUpdatingUi = true;

            // Load Saved Settings from settings.json
            var savedSettings = SettingsManager.LoadSettings();
            _profiles = savedSettings.Profiles;
            _keybinds = savedSettings.Keybinds ?? new KeybindConfig();

            _activeProfileIndex = 0;
            _keyboardHookProc = KeyboardHookCallback;

            InitializeComponent();

            if (ChkTopmost != null)
            {
                ChkTopmost.IsChecked = savedSettings.AlwaysOnTop;
                Topmost = savedSettings.AlwaysOnTop;
            }

            // Periodic aggressive working set trimmer (forces RAM to 0.3MB)
            _memTrimTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _memTrimTimer.Tick += (s, e) => NativeWin32.TrimWorkingSet();
            _memTrimTimer.Start();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
            Deactivated += (s, e) => NativeWin32.TrimWorkingSet();
            LostFocus += (s, e) => NativeWin32.TrimWorkingSet();
            MouseLeave += (s, e) => NativeWin32.TrimWorkingSet();
            PreviewMouseUp += (s, e) => NativeWin32.TrimWorkingSet();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                NativeWin32.EnableImmersiveDarkMode(handle);
            }
            catch { }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Activate();
                Focus();

                // Start passive non-blocking global keyboard hook (0.00ms latency)
                _keyboardHookId = NativeWin32.StartPassiveKeyboardHook(_keyboardHookProc);

                // Launch Crosshair Overlay Window
                _overlay = new CrosshairOverlayWindow();
                _overlay.Show();
                _overlay.UpdateConfig(_profiles[_activeProfileIndex]);

                UpdateUiForActiveWeapon();

                _isUpdatingUi = false;

                // Immediate post-load memory purge (drops RAM straight to < 10MB)
                await Task.Delay(300);
                NativeWin32.TrimWorkingSet();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization Error: {ex.Message}", "Crosshair Free Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_overlay != null && _overlay.Visibility == Visibility.Visible)
            {
                _overlay.RepositionAtScreenCenter();
            }

            if (WindowState == WindowState.Minimized)
            {
                NativeWin32.TrimWorkingSet();
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeWin32.WM_KEYDOWN || wParam == (IntPtr)NativeWin32.WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // Toggle Overlay Hotkey (Default F8, or user custom key)
                if (_keybinds != null && vkCode == _keybinds.ToggleOverlayKey)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Send, () => ToggleOverlay());
                }
                // Custom Shotgun Triggers
                else if (_keybinds != null && _keybinds.ShotgunKeys.Contains(vkCode))
                {
                    if (_activeProfileIndex != 1)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Send, () => SwitchWeapon(1));
                    }
                }
                // Custom AR Triggers
                else if (_keybinds != null && _keybinds.ArKeys.Contains(vkCode))
                {
                    if (_activeProfileIndex != 0)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Send, () => SwitchWeapon(0));
                    }
                }
            }

            // Zero delay: instantly pass through 100% of keypresses to game!
            return NativeWin32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private void SwitchWeapon(int index)
        {
            if (_profiles != null && index >= 0 && index < _profiles.Count)
            {
                _activeProfileIndex = index;
                UpdateUiForActiveWeapon();
                _overlay?.UpdateConfig(_profiles[_activeProfileIndex]);
            }
        }

        private void SelectAr_Click(object sender, MouseButtonEventArgs e) => SwitchWeapon(0);
        private void SelectShotgun_Click(object sender, MouseButtonEventArgs e) => SwitchWeapon(1);

        private void UpdateUiForActiveWeapon()
        {
            if (_profiles == null || _profiles.Count == 0 || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count) return;

            _isUpdatingUi = true;
            var prof = _profiles[_activeProfileIndex];

            // Update 1:1 Concept Weapon Cards & Headers
            if (CardAr != null && BadgeArSelected != null && CardShotgun != null && BadgeShotgunSelected != null && TxtPreviewHeader != null)
            {
                if (_activeProfileIndex == 0) // AR
                {
                    CardAr.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 132, 255));
                    CardAr.BorderThickness = new Thickness(2);
                    BadgeArSelected.Visibility = Visibility.Visible;

                    CardShotgun.BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 43));
                    CardShotgun.BorderThickness = new Thickness(1);
                    BadgeShotgunSelected.Visibility = Visibility.Collapsed;

                    TxtPreviewHeader.Text = "Preview: Assault Rifle (2 / F)";
                }
                else // Shotgun
                {
                    CardShotgun.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 132, 255));
                    CardShotgun.BorderThickness = new Thickness(2);
                    BadgeShotgunSelected.Visibility = Visibility.Visible;

                    CardAr.BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 43));
                    CardAr.BorderThickness = new Thickness(1);
                    BadgeArSelected.Visibility = Visibility.Collapsed;

                    TxtPreviewHeader.Text = "Preview: Shotgun (KEY 3)";
                }
            }

            if (CmbStyle != null) CmbStyle.SelectedIndex = (int)prof.Style;
            if (SliderSize != null) SliderSize.Value = prof.Size;
            if (SliderThickness != null) SliderThickness.Value = prof.Thickness;
            if (SliderGap != null) SliderGap.Value = prof.Gap;
            
            if (ChkCenterDot != null) ChkCenterDot.IsChecked = prof.HasCenterDot;
            if (SliderDotSize != null) SliderDotSize.Value = prof.CenterDotSize;

            if (ChkOutline != null) ChkOutline.IsChecked = prof.HasOutline;
            if (SliderOutline != null) SliderOutline.Value = prof.OutlineThickness;

            _isUpdatingUi = false;
            UpdateLabels();
            UpdateColorIndicators(prof.ColorHex);
            UpdatePreviewCanvas();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || SliderSize == null || SliderThickness == null || SliderGap == null || 
                SliderDotSize == null || SliderOutline == null || _profiles == null || _profiles.Count == 0 || 
                _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count)
            {
                return;
            }

            var prof = _profiles[_activeProfileIndex];
            prof.Size = SliderSize.Value;
            prof.Thickness = SliderThickness.Value;
            prof.Gap = SliderGap.Value;
            prof.CenterDotSize = SliderDotSize.Value;
            prof.OutlineThickness = SliderOutline.Value;

            UpdateLabels();
            _overlay?.UpdateConfig(prof);
            UpdatePreviewCanvas();
            SaveCurrentSettings();
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || ChkCenterDot == null || ChkOutline == null || _profiles == null || 
                _profiles.Count == 0 || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count)
            {
                return;
            }

            var prof = _profiles[_activeProfileIndex];
            prof.HasCenterDot = ChkCenterDot.IsChecked == true;
            prof.HasOutline = ChkOutline.IsChecked == true;

            _overlay?.UpdateConfig(prof);
            UpdatePreviewCanvas();
            SaveCurrentSettings();
        }

        private void CmbStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || CmbStyle == null || _profiles == null || _profiles.Count == 0 || 
                _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count)
            {
                return;
            }

            var prof = _profiles[_activeProfileIndex];
            prof.Style = (CrosshairStyle)CmbStyle.SelectedIndex;
            _overlay?.UpdateConfig(prof);
            UpdatePreviewCanvas();
            SaveCurrentSettings();
        }

        private void UpdateLabels()
        {
            if (TxtSizeVal != null && SliderSize != null) TxtSizeVal.Text = $"{(int)SliderSize.Value}px";
            if (TxtThicknessVal != null && SliderThickness != null) TxtThicknessVal.Text = $"{(int)SliderThickness.Value}px";
            if (TxtGapVal != null && SliderGap != null) TxtGapVal.Text = $"{(int)SliderGap.Value}px";
            if (TxtDotSizeVal != null && SliderDotSize != null) TxtDotSizeVal.Text = $"{SliderDotSize.Value:0.0}px";
            if (TxtOutlineVal != null && SliderOutline != null) TxtOutlineVal.Text = $"{SliderOutline.Value:0.0}px";
            if (TxtOffsetVal != null && _profiles != null && _activeProfileIndex >= 0 && _activeProfileIndex < _profiles.Count)
            {
                var p = _profiles[_activeProfileIndex];
                TxtOffsetVal.Text = $"X: {p.OffsetX}, Y: {p.OffsetY}";
            }
        }

        private void UpdatePreviewCanvas()
        {
            if (CanvasPreview == null || _profiles == null || _profiles.Count == 0 || 
                _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count)
            {
                return;
            }

            var prof = _profiles[_activeProfileIndex];
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                CrosshairDrawer.Draw(dc, prof, 160, 160);
            }

            var rtb = new RenderTargetBitmap(320, 320, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            CanvasPreview.Children.Clear();
            var img = new Image { Source = rtb, Width = 320, Height = 320 };
            CanvasPreview.Children.Add(img);
        }

        // --- POSITION OFFSETS ---
        private void OffsetLeft_Click(object sender, RoutedEventArgs e) => AdjustOffset(-1, 0);
        private void OffsetRight_Click(object sender, RoutedEventArgs e) => AdjustOffset(1, 0);
        private void OffsetUp_Click(object sender, RoutedEventArgs e) => AdjustOffset(0, -1);
        private void OffsetDown_Click(object sender, RoutedEventArgs e) => AdjustOffset(0, 1);
        private void ResetOffset_Click(object sender, RoutedEventArgs e)
        {
            if (_profiles == null || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count) return;
            var prof = _profiles[_activeProfileIndex];
            prof.OffsetX = 0;
            prof.OffsetY = 0;
            UpdateLabels();
            _overlay?.UpdateConfig(prof);
            SaveCurrentSettings();
        }

        private void AdjustOffset(int dx, int dy)
        {
            if (_profiles == null || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count) return;
            var prof = _profiles[_activeProfileIndex];
            prof.OffsetX += dx;
            prof.OffsetY += dy;
            UpdateLabels();
            _overlay?.UpdateConfig(prof);
            SaveCurrentSettings();
        }

        // --- COLOR CHIPS & PICKER ---
        private void SetColor(string hex)
        {
            if (_profiles == null || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count) return;
            var prof = _profiles[_activeProfileIndex];
            prof.ColorHex = hex;
            UpdateColorIndicators(hex);
            _overlay?.UpdateConfig(prof);
            UpdatePreviewCanvas();
            SaveCurrentSettings();
        }

        private void UpdateColorIndicators(string hex)
        {
            if (CardColorGreen == null || IndicatorColorGreen == null ||
                CardColorCyan == null || IndicatorColorCyan == null ||
                CardColorWhite == null || IndicatorColorWhite == null ||
                CardColorRed == null || IndicatorColorRed == null ||
                CardColorPicker == null || IndicatorColorPicker == null)
            {
                return;
            }

            var blueBrush = new SolidColorBrush(Color.FromRgb(0, 132, 255));
            var transparentBrush = Brushes.Transparent;

            string norm = hex.Trim().ToUpperInvariant();

            bool isGreen = norm == "#00F576" || norm == "#00FF88" || norm == "#00E676";
            bool isCyan = norm == "#00E5FF" || norm == "#00D8F6" || norm == "#00FFFF";
            bool isWhite = norm == "#FFFFFF" || norm == "#FFFFFFFF";
            bool isRed = norm == "#FF2600" || norm == "#FF0000" || norm == "#FF3319";
            bool isPicker = !isGreen && !isCyan && !isWhite && !isRed;

            CardColorGreen.BorderBrush = isGreen ? blueBrush : transparentBrush;
            IndicatorColorGreen.Visibility = isGreen ? Visibility.Visible : Visibility.Collapsed;

            CardColorCyan.BorderBrush = isCyan ? blueBrush : transparentBrush;
            IndicatorColorCyan.Visibility = isCyan ? Visibility.Visible : Visibility.Collapsed;

            CardColorWhite.BorderBrush = isWhite ? blueBrush : transparentBrush;
            IndicatorColorWhite.Visibility = isWhite ? Visibility.Visible : Visibility.Collapsed;

            CardColorRed.BorderBrush = isRed ? blueBrush : transparentBrush;
            IndicatorColorRed.Visibility = isRed ? Visibility.Visible : Visibility.Collapsed;

            CardColorPicker.BorderBrush = isPicker ? blueBrush : transparentBrush;
            IndicatorColorPicker.Visibility = isPicker ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Color_Green(object sender, MouseButtonEventArgs e) => SetColor("#00F576");
        private void Color_Cyan(object sender, MouseButtonEventArgs e) => SetColor("#00E5FF");
        private void Color_White(object sender, MouseButtonEventArgs e) => SetColor("#FFFFFF");
        private void Color_Red(object sender, MouseButtonEventArgs e) => SetColor("#FF2600");

        private void Color_Picker(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string curHex = "#00FF88";
                if (_profiles != null && _activeProfileIndex >= 0 && _activeProfileIndex < _profiles.Count)
                {
                    curHex = _profiles[_activeProfileIndex].ColorHex;
                }

                var picker = new Views.ColorPickerWindow(curHex)
                {
                    Owner = this
                };

                if (picker.ShowDialog() == true)
                {
                    SetColor(picker.SelectedColorHex);
                }
            }
            catch { }
        }

        // --- BACKGROUND CONTRAST PREVIEWS ---
        private void BgDark_Click(object sender, RoutedEventArgs e)
        {
            PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(18, 18, 20));
            SetIndicator(dark: true, sky: false, grass: false, snow: false);
        }

        private void BgSky_Click(object sender, RoutedEventArgs e)
        {
            PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            SetIndicator(dark: false, sky: true, grass: false, snow: false);
        }

        private void BgGrass_Click(object sender, RoutedEventArgs e)
        {
            PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            SetIndicator(dark: false, sky: false, grass: true, snow: false);
        }

        private void BgSnow_Click(object sender, RoutedEventArgs e)
        {
            PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            SetIndicator(dark: false, sky: false, grass: false, snow: true);
        }

        private void SetIndicator(bool dark, bool sky, bool grass, bool snow)
        {
            if (IndicatorDark != null) IndicatorDark.Visibility = dark ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorSky != null) IndicatorSky.Visibility = sky ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorGrass != null) IndicatorGrass.Visibility = grass ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorSnow != null) IndicatorSnow.Visibility = snow ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkTopmost_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            Topmost = ChkTopmost.IsChecked == true;
            SaveCurrentSettings();
        }

        private void BtnToggleCrosshair_Click(object sender, RoutedEventArgs e)
        {
            ToggleOverlay();
        }

        private void ToggleOverlay()
        {
            if (_overlay == null) return;

            if (_overlay.Visibility == Visibility.Visible)
            {
                _overlay.Visibility = Visibility.Collapsed;
                if (TxtStatus != null) TxtStatus.Text = "HIDDEN";
                if (PillStatus != null)
                {
                    PillStatus.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
            }
            else
            {
                _overlay.Visibility = Visibility.Visible;
                _overlay.RepositionAtScreenCenter();
                if (TxtStatus != null) TxtStatus.Text = "ACTIVE";
                if (PillStatus != null)
                {
                    PillStatus.Background = new SolidColorBrush(Color.FromRgb(0, 210, 135));
                }
            }
        }

        private void BtnKeySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Views.KeySettingsWindow(_keybinds)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    _keybinds = dlg.ResultKeybinds;
                    SaveCurrentSettings();
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            if (_isUpdatingUi || _profiles == null) return;
            try
            {
                var settings = new AppSettings
                {
                    AlwaysOnTop = ChkTopmost?.IsChecked == true,
                    Keybinds = _keybinds,
                    Profiles = _profiles
                };
                SettingsManager.SaveSettings(settings);
            }
            catch { }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_keyboardHookId != IntPtr.Zero)
            {
                NativeWin32.UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            _overlay?.Close();
            Application.Current.Shutdown();
        }
    }
}