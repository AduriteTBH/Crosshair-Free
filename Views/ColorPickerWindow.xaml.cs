using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrosshairFree.Views
{
    public partial class ColorPickerWindow : Window
    {
        public string SelectedColorHex { get; private set; } = "#00FF88";
        private double _currentHue = 140;
        private double _currentSat = 1.0;
        private double _currentVal = 1.0;
        private bool _isUpdating = false;

        public ColorPickerWindow(string initialHex = "#00FF88")
        {
            InitializeComponent();
            SetInitialColor(initialHex);
        }

        private void SetInitialColor(string hex)
        {
            try
            {
                SelectedColorHex = hex;
                if (TxtHexInput != null) TxtHexInput.Text = hex;
                UpdateSwatch(hex);

                var color = (Color)ColorConverter.ConvertFromString(hex);
                RgbToHsv(color.R, color.G, color.B, out _currentHue, out _currentSat, out _currentVal);

                if (SliderHue != null) SliderHue.Value = _currentHue;
                UpdateHueBaseColor();
                UpdateIndicatorPosition();
            }
            catch
            {
                SelectedColorHex = "#00FF88";
            }
        }

        private void SliderHue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            _currentHue = SliderHue.Value;
            UpdateHueBaseColor();
            CalculateColorFromHsv();
        }

        private void UpdateHueBaseColor()
        {
            Color hueColor = HsvToRgb(_currentHue, 1.0, 1.0);
            if (BrushHueBase != null)
            {
                BrushHueBase.Color = hueColor;
            }
        }

        private void Spectrum_MouseDown(object sender, MouseButtonEventArgs e)
        {
            UpdateSpectrumSelection(e.GetPosition(SpectrumCanvas));
        }

        private void Spectrum_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSpectrumSelection(e.GetPosition(SpectrumCanvas));
            }
        }

        private void UpdateSpectrumSelection(Point pos)
        {
            double w = SpectrumCanvas.ActualWidth;
            double h = SpectrumCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double clampedX = Math.Clamp(pos.X, 0, w);
            double clampedY = Math.Clamp(pos.Y, 0, h);

            _currentSat = clampedX / w;
            _currentVal = 1.0 - (clampedY / h);

            Canvas.SetLeft(IndicatorDot, Math.Clamp(clampedX - 6, 0, w - 12));
            Canvas.SetTop(IndicatorDot, Math.Clamp(clampedY - 6, 0, h - 12));

            CalculateColorFromHsv();
        }

        private void UpdateIndicatorPosition()
        {
            double w = SpectrumCanvas?.ActualWidth ?? 380;
            double h = SpectrumCanvas?.ActualHeight ?? 140;
            if (w <= 0) w = 380;
            if (h <= 0) h = 140;

            double x = _currentSat * w;
            double y = (1.0 - _currentVal) * h;

            if (IndicatorDot != null)
            {
                Canvas.SetLeft(IndicatorDot, Math.Clamp(x - 6, 0, w - 12));
                Canvas.SetTop(IndicatorDot, Math.Clamp(y - 6, 0, h - 12));
            }
        }

        private void CalculateColorFromHsv()
        {
            Color color = HsvToRgb(_currentHue, _currentSat, _currentVal);
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            SelectedColorHex = hex;

            _isUpdating = true;
            if (TxtHexInput != null) TxtHexInput.Text = hex;
            UpdateSwatch(hex);
            _isUpdating = false;
        }

        private void Preset_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Background is SolidColorBrush scb)
            {
                var c = scb.Color;
                string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                SetInitialColor(hex);
            }
        }

        private void TxtHexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || TxtHexInput == null) return;
            string hex = TxtHexInput.Text.Trim();
            if (hex.Length == 7 && hex.StartsWith("#"))
            {
                SelectedColorHex = hex;
                UpdateSwatch(hex);
            }
        }

        private void UpdateSwatch(string hex)
        {
            try
            {
                var col = (Color)ColorConverter.ConvertFromString(hex);
                if (SwatchPreview != null)
                {
                    SwatchPreview.Background = new SolidColorBrush(col);
                }
            }
            catch { }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            int hi = (int)Math.Floor(h / 60.0) % 6;
            double f = (h / 60.0) - Math.Floor(h / 60.0);

            v = Math.Clamp(v, 0, 1) * 255;
            byte vByte = (byte)v;
            byte p = (byte)(v * (1 - s));
            byte q = (byte)(v * (1 - f * s));
            byte t = (byte)(v * (1 - (1 - f) * s));

            switch (hi)
            {
                case 0: return Color.FromRgb(vByte, t, p);
                case 1: return Color.FromRgb(q, vByte, p);
                case 2: return Color.FromRgb(p, vByte, t);
                case 3: return Color.FromRgb(p, q, vByte);
                case 4: return Color.FromRgb(t, p, vByte);
                default: return Color.FromRgb(vByte, p, q);
            }
        }

        private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            v = max;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0)
            {
                h = 0;
            }
            else if (max == rd)
            {
                h = 60.0 * (((gd - bd) / delta) % 6);
                if (h < 0) h += 360.0;
            }
            else if (max == gd)
            {
                h = 60.0 * (((bd - rd) / delta) + 2);
            }
            else
            {
                h = 60.0 * (((rd - gd) / delta) + 4);
            }
        }
    }
}
