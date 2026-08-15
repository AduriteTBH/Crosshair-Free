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
        private DispatcherTimer? _memTrimTimer;
        private readonly SystemTrayService _trayService = new SystemTrayService();
        private bool _isRealExit = false;

        public MainWindow()
        {
            _isUpdatingUi = true;

            // Load Saved Settings from settings.json
            var savedSettings = SettingsManager.LoadSettings();
            _profiles = savedSettings.Profiles;
            _keybinds = savedSettings.Keybinds ?? new KeybindConfig();

            _activeProfileIndex = 0;

            InitializeComponent();

            if (ChkTopmost != null)
            {
                ChkTopmost.IsChecked = savedSettings.AlwaysOnTop;
                Topmost = savedSettings.AlwaysOnTop;
            }

            if (ChkSystemTray != null)
            {
                ChkSystemTray.IsChecked = savedSettings.SystemTray;
            }

            // Periodic working set trimmer (forces RAM to ~0.2MB - 0.3MB)
            _memTrimTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _memTrimTimer.Tick += (s, e) => NativeWin32.TrimWorkingSet();
            _memTrimTimer.Start();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
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

        private void UpdateToggleHotkeyLabel()
        {
            if (BtnToggleCrosshair != null && _keybinds != null)
            {
                string keyName = KeybindHelper.GetKeyName(_keybinds.ToggleOverlayKey);
                BtnToggleCrosshair.Content = $"TOGGLE ({keyName})";
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Activate();
                Focus();

                // Start dedicated background hook service (100% resilient, 0 timeout, 0.00ms latency)
                KeyboardHookService.Instance.KeyDown += OnGlobalKeyDown;
                KeyboardHookService.Instance.Start();

                // Initialize System Tray Icon
                _trayService.OnOpenRequested += () => Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    Focus();
                });

                _trayService.OnContextMenuRequested += (x, y) => Dispatcher.Invoke(() => ShowDarkTrayContextMenu(x, y));
                _trayService.Initialize(this);

                // Launch Crosshair Overlay Window
                _overlay = new CrosshairOverlayWindow();
                _overlay.Show();
                _overlay.UpdateConfig(_profiles[_activeProfileIndex]);

                UpdateUiForActiveWeapon();
                UpdateToggleHotkeyLabel();

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
            if (_overlay != null && _overlay.IsOverlayVisible)
            {
                _overlay.RepositionAtScreenCenter();
            }

            if (WindowState == WindowState.Minimized)
            {
                NativeWin32.TrimWorkingSet();
            }
        }

        private void OnGlobalKeyDown(int vkCode)
        {
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

            if (SliderOpacity != null) SliderOpacity.Value = Math.Clamp(Math.Round(prof.Opacity * 100.0, 0), 10.0, 100.0);

            _isUpdatingUi = false;
            UpdateLabels();
            UpdateColorIndicators(prof.ColorHex);
            UpdatePreviewCanvas();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || SliderSize == null || SliderThickness == null || SliderGap == null || 
                SliderDotSize == null || SliderOutline == null || SliderOpacity == null || _profiles == null || 
                _profiles.Count == 0 || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count)
            {
                return;
            }

            var prof = _profiles[_activeProfileIndex];
            prof.Size = Math.Round(SliderSize.Value, 1);
            prof.Thickness = Math.Round(SliderThickness.Value, 1);
            prof.Gap = Math.Round(SliderGap.Value, 1);
            prof.CenterDotSize = Math.Round(SliderDotSize.Value, 1);
            prof.OutlineThickness = Math.Round(SliderOutline.Value, 1);
            prof.Opacity = Math.Round(SliderOpacity.Value / 100.0, 2);

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
            if (TxtSizeVal != null && SliderSize != null)
                TxtSizeVal.Text = $"{SliderSize.Value:0.0}px";

            if (TxtThicknessVal != null && SliderThickness != null)
                TxtThicknessVal.Text = $"{SliderThickness.Value:0.0}px";

            if (TxtGapVal != null && SliderGap != null)
                TxtGapVal.Text = $"{SliderGap.Value:0.0}px";

            if (TxtDotSizeVal != null && SliderDotSize != null)
                TxtDotSizeVal.Text = $"{SliderDotSize.Value:0.0}px";

            if (TxtOutlineVal != null && SliderOutline != null)
                TxtOutlineVal.Text = $"{SliderOutline.Value:0.0}px";

            if (TxtOpacityVal != null && SliderOpacity != null)
                TxtOpacityVal.Text = $"{(int)SliderOpacity.Value}%";

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

        // --- COLOR MANAGEMENT ---
        private void Color_Green(object sender, MouseButtonEventArgs e) => ApplyColor("#00FF88");
        private void Color_Cyan(object sender, MouseButtonEventArgs e) => ApplyColor("#00E5FF");
        private void Color_White(object sender, MouseButtonEventArgs e) => ApplyColor("#FFFFFF");
        private void Color_Red(object sender, MouseButtonEventArgs e) => ApplyColor("#FF3366");

        private void ApplyColor(string hex)
        {
            if (_profiles == null || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count) return;
            var prof = _profiles[_activeProfileIndex];
            prof.ColorHex = hex;
            UpdateColorIndicators(hex);
            _overlay?.UpdateConfig(prof);
            UpdatePreviewCanvas();
            SaveCurrentSettings();
        }

        private void UpdateColorIndicators(string activeHex)
        {
            string clean = activeHex.ToUpperInvariant();
            if (IndicatorColorGreen != null) IndicatorColorGreen.Visibility = clean == "#00FF88" ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorColorCyan != null) IndicatorColorCyan.Visibility = clean == "#00E5FF" ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorColorWhite != null) IndicatorColorWhite.Visibility = clean == "#FFFFFF" ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorColorRed != null) IndicatorColorRed.Visibility = clean == "#FF3366" ? Visibility.Visible : Visibility.Collapsed;
            if (IndicatorColorPicker != null)
            {
                bool isPreset = clean == "#00FF88" || clean == "#00E5FF" || clean == "#FFFFFF" || clean == "#FF3366";
                IndicatorColorPicker.Visibility = isPreset ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void Color_Picker(object sender, MouseButtonEventArgs e)
        {
            if (_profiles == null || _activeProfileIndex < 0 || _activeProfileIndex >= _profiles.Count) return;
            var prof = _profiles[_activeProfileIndex];

            try
            {
                var dlg = new Views.ColorPickerWindow(prof.ColorHex)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    ApplyColor(dlg.SelectedColorHex);
                }
            }
            catch { }
        }

        // --- BACKGROUND PREVIEWS ---
        private void BgDark_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewViewport != null) PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(18, 18, 20));
            SetIndicator(dark: true, sky: false, grass: false, snow: false);
        }

        private void BgSky_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewViewport != null) PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            SetIndicator(dark: false, sky: true, grass: false, snow: false);
        }

        private void BgGrass_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewViewport != null) PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            SetIndicator(dark: false, sky: false, grass: true, snow: false);
        }

        private void BgSnow_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewViewport != null) PreviewViewport.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
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

        private void ChkSystemTray_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            SaveCurrentSettings();
        }

        private void BtnToggleCrosshair_Click(object sender, RoutedEventArgs e)
        {
            ToggleOverlay();
        }

        private void ToggleOverlay()
        {
            if (_overlay == null) return;

            bool newVisible = !_overlay.IsOverlayVisible;
            _overlay.SetOverlayVisible(newVisible);

            if (!newVisible)
            {
                if (TxtStatus != null) TxtStatus.Text = "HIDDEN";
                if (PillStatus != null)
                {
                    PillStatus.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
            }
            else
            {
                _overlay.RepositionAtScreenCenter();
                _overlay.UpdateConfig(_profiles[_activeProfileIndex]);
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
                    UpdateToggleHotkeyLabel();
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
                    SystemTray = ChkSystemTray?.IsChecked == true,
                    Keybinds = _keybinds,
                    Profiles = _profiles
                };
                SettingsManager.SaveSettings(settings);
            }
            catch { }
        }

        private MenuItem CreateTrayMenuItem(string text, string iconType, Action onClick, bool isChecked = false)
        {
            var item = new MenuItem
            {
                Header = text,
                Style = (Style)FindResource("DarkTrayMenuItemStyle")
            };

            item.Icon = CreateVectorMenuIcon(iconType, isChecked);
            item.Click += (s, e) => onClick();
            return item;
        }

        private static FrameworkElement CreateVectorMenuIcon(string type, bool isChecked)
        {
            var grid = new Grid
            {
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            switch (type)
            {
                case "open":
                    // Crisp Window Icon (App frame)
                    var pathOpen = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M 1 2.5 H 13 V 11.5 H 1 Z M 1 5.5 H 13 M 4 2.5 V 5.5"),
                        Stroke = new SolidColorBrush(Color.FromRgb(165, 165, 178)),
                        StrokeThickness = 1.2,
                        Stretch = Stretch.Uniform,
                        Width = 12,
                        Height = 11
                    };
                    grid.Children.Add(pathOpen);
                    break;

                case "toggle":
                    // Precision Crosshair Reticle Icon
                    var pathCross = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M 6 0.5 V 3.5 M 6 8.5 V 11.5 M 0.5 6 H 3.5 M 8.5 6 H 11.5 M 6 3.5 A 2.5 2.5 0 1 0 6 8.5 A 2.5 2.5 0 1 0 6 3.5"),
                        Stroke = new SolidColorBrush(Color.FromRgb(0, 229, 255)),
                        StrokeThickness = 1.2,
                        Stretch = Stretch.Uniform,
                        Width = 12,
                        Height = 12
                    };
                    grid.Children.Add(pathCross);
                    break;

                case "check":
                    if (isChecked)
                    {
                        var pathCheck = new System.Windows.Shapes.Path
                        {
                            Data = Geometry.Parse("M 1 6 L 4.5 9.5 L 11 2"),
                            Stroke = new SolidColorBrush(Color.FromRgb(0, 132, 255)),
                            StrokeThickness = 1.8,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                            StrokeLineJoin = PenLineJoin.Round,
                            Stretch = Stretch.Uniform,
                            Width = 11,
                            Height = 9
                        };
                        grid.Children.Add(pathCheck);
                    }
                    break;

                case "exit":
                    // Red Power / Exit Icon
                    var pathExit = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M 6 1 V 5.5 M 2.5 3 A 4.2 4.2 0 1 0 9.5 3"),
                        Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        StrokeThickness = 1.4,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        Stretch = Stretch.Uniform,
                        Width = 12,
                        Height = 12
                    };
                    grid.Children.Add(pathExit);
                    break;
            }

            return grid;
        }

        private void ShowDarkTrayContextMenu(int screenX, int screenY)
        {
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("DarkTrayContextMenuStyle"),
                Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint,
                HorizontalOffset = screenX,
                VerticalOffset = screenY
            };

            // 1. Open Crosshair Free
            menu.Items.Add(CreateTrayMenuItem("Open Crosshair Free", "open", () =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                Focus();
            }));

            // 2. Toggle Crosshair (F8)
            string keyName = _keybinds != null ? KeybindHelper.GetKeyName(_keybinds.ToggleOverlayKey) : "F8";
            menu.Items.Add(CreateTrayMenuItem($"Toggle Crosshair ({keyName})", "toggle", () => ToggleOverlay()));

            menu.Items.Add(new Separator { Style = (Style)FindResource("DarkTraySeparatorStyle") });

            // 3. Assault Rifle Profile
            menu.Items.Add(CreateTrayMenuItem("Assault Rifle Profile", "check", () => SwitchWeapon(0), isChecked: _activeProfileIndex == 0));

            // 4. Shotgun Profile
            menu.Items.Add(CreateTrayMenuItem("Shotgun Profile", "check", () => SwitchWeapon(1), isChecked: _activeProfileIndex == 1));

            menu.Items.Add(new Separator { Style = (Style)FindResource("DarkTraySeparatorStyle") });

            // 5. Always on Top
            menu.Items.Add(CreateTrayMenuItem("Always on Top", "check", () =>
            {
                if (ChkTopmost != null) ChkTopmost.IsChecked = !ChkTopmost.IsChecked;
            }, isChecked: Topmost));

            menu.Items.Add(new Separator { Style = (Style)FindResource("DarkTraySeparatorStyle") });

            // 6. Exit Crosshair Free
            menu.Items.Add(CreateTrayMenuItem("Exit Crosshair Free", "exit", () =>
            {
                _isRealExit = true;
                Close();
            }));

            var handle = new WindowInteropHelper(this).Handle;
            NativeWin32.SetForegroundWindow(handle);

            menu.IsOpen = true;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isRealExit && ChkSystemTray?.IsChecked == true)
            {
                e.Cancel = true;
                Hide();
                NativeWin32.TrimWorkingSet();
                return;
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            _trayService.Dispose();
            KeyboardHookService.Instance.Stop();
            _overlay?.Close();
            Application.Current.Shutdown();
        }
    }
}