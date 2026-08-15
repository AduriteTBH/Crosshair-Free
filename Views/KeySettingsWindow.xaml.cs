using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CrosshairFree.Models;

namespace CrosshairFree.Views
{
    public partial class KeySettingsWindow : Window
    {
        public KeybindConfig ResultKeybinds { get; private set; }
        private string? _listeningFor = null; // "AR", "SHOTGUN", "TOGGLE"

        public KeySettingsWindow(KeybindConfig? currentKeybinds)
        {
            InitializeComponent();
            ResultKeybinds = (currentKeybinds ?? new KeybindConfig()).Clone();

            PreviewKeyDown += KeySettingsWindow_PreviewKeyDown;
            RenderKeyChips();
        }

        private void RenderKeyChips()
        {
            // 1. AR Keys
            PanelArKeys.Children.Clear();
            foreach (var vk in ResultKeybinds.ArKeys)
            {
                PanelArKeys.Children.Add(CreateKeyChip(vk, isAr: true));
            }

            // 2. Shotgun Keys
            PanelShotgunKeys.Children.Clear();
            foreach (var vk in ResultKeybinds.ShotgunKeys)
            {
                PanelShotgunKeys.Children.Add(CreateKeyChip(vk, isAr: false));
            }

            // 3. Toggle Hotkey
            if (BtnToggleHotkey != null)
            {
                BtnToggleHotkey.Content = KeybindHelper.GetKeyName(ResultKeybinds.ToggleOverlayKey);
            }
        }

        private Border CreateKeyChip(int vk, bool isAr)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 6, 4),
                Margin = new Thickness(0, 0, 6, 6)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txt = new TextBlock
            {
                Text = KeybindHelper.GetKeyName(vk),
                FontWeight = FontWeights.Bold,
                Foreground = isAr ? new SolidColorBrush(Color.FromRgb(96, 205, 255)) : new SolidColorBrush(Color.FromRgb(255, 94, 134)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(txt, 0);
            grid.Children.Add(txt);

            var btnDel = new Button
            {
                Content = "✕",
                Width = 16,
                Height = 16,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnDel.Click += (s, e) =>
            {
                if (isAr) ResultKeybinds.ArKeys.Remove(vk);
                else ResultKeybinds.ShotgunKeys.Remove(vk);
                RenderKeyChips();
            };
            Grid.SetColumn(btnDel, 1);
            grid.Children.Add(btnDel);

            border.Child = grid;
            return border;
        }

        private void BtnAddArKey_Click(object sender, RoutedEventArgs e)
        {
            _listeningFor = "AR";
            BtnAddArKey.Content = "🔴 Press any key...";
            BtnAddArKey.Background = new SolidColorBrush(Color.FromRgb(0, 132, 255));
            BtnAddArKey.Foreground = Brushes.White;
        }

        private void BtnAddShotgunKey_Click(object sender, RoutedEventArgs e)
        {
            _listeningFor = "SHOTGUN";
            BtnAddShotgunKey.Content = "🔴 Press any key...";
            BtnAddShotgunKey.Background = new SolidColorBrush(Color.FromRgb(255, 38, 0));
            BtnAddShotgunKey.Foreground = Brushes.White;
        }

        private void BtnToggleHotkey_Click(object sender, RoutedEventArgs e)
        {
            _listeningFor = "TOGGLE";
            BtnToggleHotkey.Content = "Press key...";
            BtnToggleHotkey.Background = new SolidColorBrush(Color.FromRgb(0, 132, 255));
        }

        private void KeySettingsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_listeningFor == null) return;

            e.Handled = true;
            int vkCode = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

            if (e.Key == Key.Escape)
            {
                ResetListeningState();
                return;
            }

            if (_listeningFor == "AR")
            {
                if (!ResultKeybinds.ArKeys.Contains(vkCode))
                {
                    ResultKeybinds.ArKeys.Add(vkCode);
                }
            }
            else if (_listeningFor == "SHOTGUN")
            {
                if (!ResultKeybinds.ShotgunKeys.Contains(vkCode))
                {
                    ResultKeybinds.ShotgunKeys.Add(vkCode);
                }
            }
            else if (_listeningFor == "TOGGLE")
            {
                ResultKeybinds.ToggleOverlayKey = vkCode;
            }

            ResetListeningState();
            RenderKeyChips();
        }

        private void ResetListeningState()
        {
            _listeningFor = null;
            if (BtnAddArKey != null)
            {
                BtnAddArKey.Content = "+ Add Key";
                BtnAddArKey.Background = new SolidColorBrush(Color.FromRgb(36, 36, 46));
                BtnAddArKey.Foreground = new SolidColorBrush(Color.FromRgb(96, 205, 255));
            }
            if (BtnAddShotgunKey != null)
            {
                BtnAddShotgunKey.Content = "+ Add Key";
                BtnAddShotgunKey.Background = new SolidColorBrush(Color.FromRgb(36, 36, 46));
                BtnAddShotgunKey.Foreground = new SolidColorBrush(Color.FromRgb(255, 94, 134));
            }
            if (BtnToggleHotkey != null)
            {
                BtnToggleHotkey.Content = KeybindHelper.GetKeyName(ResultKeybinds.ToggleOverlayKey);
                BtnToggleHotkey.Background = new SolidColorBrush(Color.FromRgb(36, 36, 46));
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            ResultKeybinds = new KeybindConfig();
            ResetListeningState();
            RenderKeyChips();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
