using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CrosshairFree.Core;
using CrosshairFree.Models;

namespace CrosshairFree.Rendering
{
    public partial class CrosshairOverlayWindow : Window
    {
        private CrosshairConfig _config = new CrosshairConfig();
        private IntPtr _hwnd = IntPtr.Zero;
        private DrawingGroup _cachedDrawingGroup = new DrawingGroup();

        public CrosshairOverlayWindow()
        {
            InitializeComponent();
            SourceInitialized += CrosshairOverlayWindow_SourceInitialized;
            Loaded += CrosshairOverlayWindow_Loaded;

            // Enable smooth, crisp anti-aliasing (no jagged/pixelated edges)
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
        }

        private void CrosshairOverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeWin32.MakeClickThrough(_hwnd);
        }

        private void CrosshairOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RepositionAtScreenCenter();
            if (_hwnd != IntPtr.Zero)
            {
                NativeWin32.ForceTopmost(_hwnd);
            }
        }

        public void RepositionAtScreenCenter()
        {
            int screenW = NativeWin32.GetSystemMetrics(NativeWin32.SM_CXSCREEN);
            int screenH = NativeWin32.GetSystemMetrics(NativeWin32.SM_CYSCREEN);
            if (screenW <= 0) screenW = 1920;
            if (screenH <= 0) screenH = 1080;

            double targetLeft = (screenW / 2.0) - (Width / 2.0) + _config.OffsetX;
            double targetTop = (screenH / 2.0) - (Height / 2.0) + _config.OffsetY;

            Left = targetLeft;
            Top = targetTop;

            if (_hwnd != IntPtr.Zero)
            {
                NativeWin32.ForceTopmost(_hwnd);
            }
        }

        public void UpdateConfig(CrosshairConfig config)
        {
            _config = config;
            RepositionAtScreenCenter();

            // Pre-bake and freeze drawing visual: crisp vector anti-aliased geometry with 0 CPU overhead
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                double centerX = Width / 2.0;
                double centerY = Height / 2.0;
                CrosshairDrawer.Draw(dc, _config, centerX, centerY);
            }
            dg.Freeze();
            _cachedDrawingGroup = dg;

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (_cachedDrawingGroup != null)
            {
                drawingContext.DrawDrawing(_cachedDrawingGroup);
            }
        }
    }
}
