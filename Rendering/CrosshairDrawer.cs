using System;
using System.Windows;
using System.Windows.Media;
using CrosshairFree.Models;

namespace CrosshairFree.Rendering
{
    public static class CrosshairDrawer
    {
        public static void Draw(DrawingContext dc, CrosshairConfig config, double centerX, double centerY)
        {
            if (config == null || config.Opacity <= 0.01) return;

            Color mainColor = ParseColor(config.ColorHex, config.Opacity);
            Color outlineColor = ParseColor(config.OutlineColorHex, config.Opacity);

            Brush mainBrush = new SolidColorBrush(mainColor);
            Brush outlineBrush = new SolidColorBrush(outlineColor);
            mainBrush.Freeze();
            outlineBrush.Freeze();

            Pen mainPen = new Pen(mainBrush, config.Thickness) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
            Pen outlinePen = new Pen(outlineBrush, config.Thickness + (config.OutlineThickness * 2)) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
            mainPen.Freeze();
            outlinePen.Freeze();

            double gap = Math.Max(0, config.Gap);
            double size = Math.Max(1, config.Size);

            // 1. Center Dot (if enabled or if DotOnly / Bullseye)
            bool shouldDrawCenterDot = config.HasCenterDot || config.Style == CrosshairStyle.DotOnly;
            if (shouldDrawCenterDot && config.CenterDotSize > 0)
            {
                double dotRadius = (config.Style == CrosshairStyle.DotOnly ? Math.Max(config.CenterDotSize, size * 0.5) : config.CenterDotSize) / 2.0;
                if (config.HasOutline)
                {
                    dc.DrawEllipse(outlineBrush, null, new Point(centerX, centerY), dotRadius + config.OutlineThickness, dotRadius + config.OutlineThickness);
                }
                dc.DrawEllipse(mainBrush, null, new Point(centerX, centerY), dotRadius, dotRadius);
            }

            // 2. Style-Specific Reticle Drawing
            switch (config.Style)
            {
                case CrosshairStyle.CrossAndDot:
                case CrosshairStyle.ClassicCross:
                case CrosshairStyle.ValorantClassic:
                    DrawCrossLines(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.Cs2Precision:
                    // CS2 Pro Box-Cross: Tight inner precision box with 4 directional extended cross ticks
                    DrawBoxSquare(dc, centerX, centerY, gap + (size * 0.35), config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCrossLines(dc, centerX, centerY, gap + (size * 0.35) + 2, size * 0.65, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.ShotgunCircle:
                case CrosshairStyle.DotWithCircle:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.ShotgunQuadrant:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCrossLines(dc, centerX, centerY, gap + size, size * 0.45, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.ShotgunOctagon:
                    DrawPolygon(dc, centerX, centerY, gap + size, 8, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.ShotgunHexagon:
                    DrawPolygon(dc, centerX, centerY, gap + size, 6, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.ShotgunDoubleRing:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCircle(dc, centerX, centerY, (gap + size) * 0.55, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.ShotgunCrossRing:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCrossLines(dc, centerX, centerY, gap * 0.4, (gap + size) * 0.6, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.ShotgunDiamondBloom:
                    DrawDiamond(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCornerDots(dc, centerX, centerY, (gap + size) * 0.7, config.CenterDotSize, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.ShotgunCrossDots:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCardinalDots(dc, centerX, centerY, (gap + size) * 0.7, config.CenterDotSize, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.ShotgunTriBloom:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawTriPoint(dc, centerX, centerY, gap * 0.5, size * 0.6, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.CrossAndCircle:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCrossLines(dc, centerX, centerY, gap * 0.3, size * 0.65, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.TStyle:
                    DrawCrossLines(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline, top: false, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.TacticalChevron:
                    DrawChevron(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.DoubleChevron:
                    DrawDoubleChevron(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.Diamond:
                    DrawDiamond(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.BoxSquare:
                    DrawBoxSquare(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.TriPoint:
                    DrawTriPoint(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.XCross:
                    DrawXCross(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.HollowSquare:
                    DrawHollowSquare(dc, centerX, centerY, gap, size, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.ApexTriDot:
                    DrawApexTriDot(dc, centerX, centerY, gap + size, config.CenterDotSize, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.OverwatchTriTick:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawOverwatchTicks(dc, centerX, centerY, gap + size, size * 0.45, mainPen, outlinePen, config.HasOutline);
                    break;

                case CrosshairStyle.CyberDot:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCrossLines(dc, centerX, centerY, gap + size + 2, size * 0.4, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    break;

                case CrosshairStyle.SniperCrosshair:
                    DrawCrossLines(dc, centerX, centerY, gap, size * 1.5, mainPen, outlinePen, config.HasOutline, top: true, bottom: true, left: true, right: true);
                    DrawMilDots(dc, centerX, centerY, gap, size * 1.5, Math.Max(2.5, config.CenterDotSize), mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.Bullseye:
                    DrawCircle(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    DrawCircle(dc, centerX, centerY, (gap + size) * 0.5, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.Heart:
                    DrawHeart(dc, centerX, centerY, gap + size, config.Thickness, mainBrush, outlineBrush, config.HasOutline, config.OutlineThickness);
                    break;

                case CrosshairStyle.DotOnly:
                    // Handled by center dot above
                    break;
            }
        }

        private static void DrawCrossLines(DrawingContext dc, double cx, double cy, double gap, double len, Pen mainPen, Pen outlinePen, bool outline, bool top, bool bottom, bool left, bool right)
        {
            if (outline)
            {
                if (top) dc.DrawLine(outlinePen, new Point(cx, cy - gap), new Point(cx, cy - gap - len));
                if (bottom) dc.DrawLine(outlinePen, new Point(cx, cy + gap), new Point(cx, cy + gap + len));
                if (left) dc.DrawLine(outlinePen, new Point(cx - gap, cy), new Point(cx - gap - len, cy));
                if (right) dc.DrawLine(outlinePen, new Point(cx + gap, cy), new Point(cx + gap + len, cy));
            }

            if (top) dc.DrawLine(mainPen, new Point(cx, cy - gap), new Point(cx, cy - gap - len));
            if (bottom) dc.DrawLine(mainPen, new Point(cx, cy + gap), new Point(cx, cy + gap + len));
            if (left) dc.DrawLine(mainPen, new Point(cx - gap, cy), new Point(cx - gap - len, cy));
            if (right) dc.DrawLine(mainPen, new Point(cx + gap, cy), new Point(cx + gap + len, cy));
        }

        private static void DrawXCross(DrawingContext dc, double cx, double cy, double gap, double len, Pen mainPen, Pen outlinePen, bool outline)
        {
            double dGap = gap * 0.7071;
            double dLen = len * 0.7071;

            if (outline)
            {
                dc.DrawLine(outlinePen, new Point(cx - dGap, cy - dGap), new Point(cx - dGap - dLen, cy - dGap - dLen));
                dc.DrawLine(outlinePen, new Point(cx + dGap, cy - dGap), new Point(cx + dGap + dLen, cy - dGap - dLen));
                dc.DrawLine(outlinePen, new Point(cx - dGap, cy + dGap), new Point(cx - dGap - dLen, cy + dGap + dLen));
                dc.DrawLine(outlinePen, new Point(cx + dGap, cy + dGap), new Point(cx + dGap + dLen, cy + dGap + dLen));
            }

            dc.DrawLine(mainPen, new Point(cx - dGap, cy - dGap), new Point(cx - dGap - dLen, cy - dGap - dLen));
            dc.DrawLine(mainPen, new Point(cx + dGap, cy - dGap), new Point(cx + dGap + dLen, cy - dGap - dLen));
            dc.DrawLine(mainPen, new Point(cx - dGap, cy + dGap), new Point(cx - dGap - dLen, cy + dGap + dLen));
            dc.DrawLine(mainPen, new Point(cx + dGap, cy + dGap), new Point(cx + dGap + dLen, cy + dGap + dLen));
        }

        private static void DrawHollowSquare(DrawingContext dc, double cx, double cy, double gap, double len, Pen mainPen, Pen outlinePen, bool outline)
        {
            double half = gap + len;
            double corner = Math.Max(3, len * 0.4);

            if (outline)
            {
                // Top-Left corner
                dc.DrawLine(outlinePen, new Point(cx - half, cy - half), new Point(cx - half + corner, cy - half));
                dc.DrawLine(outlinePen, new Point(cx - half, cy - half), new Point(cx - half, cy - half + corner));

                // Top-Right corner
                dc.DrawLine(outlinePen, new Point(cx + half, cy - half), new Point(cx + half - corner, cy - half));
                dc.DrawLine(outlinePen, new Point(cx + half, cy - half), new Point(cx + half, cy - half + corner));

                // Bottom-Left corner
                dc.DrawLine(outlinePen, new Point(cx - half, cy + half), new Point(cx - half + corner, cy + half));
                dc.DrawLine(outlinePen, new Point(cx - half, cy + half), new Point(cx - half, cy + half - corner));

                // Bottom-Right corner
                dc.DrawLine(outlinePen, new Point(cx + half, cy + half), new Point(cx + half - corner, cy + half));
                dc.DrawLine(outlinePen, new Point(cx + half, cy + half), new Point(cx + half, cy + half - corner));
            }

            // Top-Left corner
            dc.DrawLine(mainPen, new Point(cx - half, cy - half), new Point(cx - half + corner, cy - half));
            dc.DrawLine(mainPen, new Point(cx - half, cy - half), new Point(cx - half, cy - half + corner));

            // Top-Right corner
            dc.DrawLine(mainPen, new Point(cx + half, cy - half), new Point(cx + half - corner, cy - half));
            dc.DrawLine(mainPen, new Point(cx + half, cy - half), new Point(cx + half, cy - half + corner));

            // Bottom-Left corner
            dc.DrawLine(mainPen, new Point(cx - half, cy + half), new Point(cx - half + corner, cy + half));
            dc.DrawLine(mainPen, new Point(cx - half, cy + half), new Point(cx - half, cy + half - corner));

            // Bottom-Right corner
            dc.DrawLine(mainPen, new Point(cx + half, cy + half), new Point(cx + half - corner, cy + half));
            dc.DrawLine(mainPen, new Point(cx + half, cy + half), new Point(cx + half, cy + half - corner));
        }

        private static void DrawChevron(DrawingContext dc, double cx, double cy, double gap, double size, Pen mainPen, Pen outlinePen, bool outline)
        {
            double halfW = size * 0.9;
            double height = size * 0.8;
            Point pApex = new Point(cx, cy - gap);
            Point pLeft = new Point(cx - halfW, cy - gap + height);
            Point pRight = new Point(cx + halfW, cy - gap + height);

            if (outline)
            {
                dc.DrawLine(outlinePen, pLeft, pApex);
                dc.DrawLine(outlinePen, pApex, pRight);
            }

            dc.DrawLine(mainPen, pLeft, pApex);
            dc.DrawLine(mainPen, pApex, pRight);
        }

        private static void DrawDoubleChevron(DrawingContext dc, double cx, double cy, double gap, double size, Pen mainPen, Pen outlinePen, bool outline)
        {
            double wingW = Math.Max(3.5, size * 0.85);
            double wingH = Math.Max(3.5, size * 0.6);
            double separation = Math.Max(5.0, size * 0.7);

            // Upper Chevron
            Point p1Apex = new Point(cx, cy - gap - separation);
            Point p1Left = new Point(cx - wingW, cy - gap - separation + wingH);
            Point p1Right = new Point(cx + wingW, cy - gap - separation + wingH);

            // Lower Chevron
            Point p2Apex = new Point(cx, cy - gap);
            Point p2Left = new Point(cx - wingW, cy - gap + wingH);
            Point p2Right = new Point(cx + wingW, cy - gap + wingH);

            if (outline)
            {
                dc.DrawLine(outlinePen, p1Left, p1Apex);
                dc.DrawLine(outlinePen, p1Apex, p1Right);
                dc.DrawLine(outlinePen, p2Left, p2Apex);
                dc.DrawLine(outlinePen, p2Apex, p2Right);
            }

            dc.DrawLine(mainPen, p1Left, p1Apex);
            dc.DrawLine(mainPen, p1Apex, p1Right);
            dc.DrawLine(mainPen, p2Left, p2Apex);
            dc.DrawLine(mainPen, p2Apex, p2Right);
        }

        private static void DrawTriPoint(DrawingContext dc, double cx, double cy, double gap, double size, Pen mainPen, Pen outlinePen, bool outline)
        {
            Point pTopStart = new Point(cx, cy - gap);
            Point pTopEnd = new Point(cx, cy - gap - size);

            double cos30 = 0.866025;
            double sin30 = 0.5;
            Point pLStart = new Point(cx - gap * cos30, cy + gap * sin30);
            Point pLEnd = new Point(cx - (gap + size) * cos30, cy + (gap + size) * sin30);

            Point pRStart = new Point(cx + gap * cos30, cy + gap * sin30);
            Point pREnd = new Point(cx + (gap + size) * cos30, cy + (gap + size) * sin30);

            if (outline)
            {
                dc.DrawLine(outlinePen, pTopStart, pTopEnd);
                dc.DrawLine(outlinePen, pLStart, pLEnd);
                dc.DrawLine(outlinePen, pRStart, pREnd);
            }

            dc.DrawLine(mainPen, pTopStart, pTopEnd);
            dc.DrawLine(mainPen, pLStart, pLEnd);
            dc.DrawLine(mainPen, pRStart, pREnd);
        }

        private static void DrawOverwatchTicks(DrawingContext dc, double cx, double cy, double radius, double tickLen, Pen mainPen, Pen outlinePen, bool outline)
        {
            Point pL1 = new Point(cx - radius, cy);
            Point pL2 = new Point(cx - radius - tickLen, cy);

            Point pR1 = new Point(cx + radius, cy);
            Point pR2 = new Point(cx + radius + tickLen, cy);

            Point pB1 = new Point(cx, cy + radius);
            Point pB2 = new Point(cx, cy + radius + tickLen);

            if (outline)
            {
                dc.DrawLine(outlinePen, pL1, pL2);
                dc.DrawLine(outlinePen, pR1, pR2);
                dc.DrawLine(outlinePen, pB1, pB2);
            }

            dc.DrawLine(mainPen, pL1, pL2);
            dc.DrawLine(mainPen, pR1, pR2);
            dc.DrawLine(mainPen, pB1, pB2);
        }

        private static void DrawDiamond(DrawingContext dc, double cx, double cy, double radius, double thickness, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            PathGeometry geom = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = new Point(cx, cy - radius), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(cx + radius, cy), true));
            figure.Segments.Add(new LineSegment(new Point(cx, cy + radius), true));
            figure.Segments.Add(new LineSegment(new Point(cx - radius, cy), true));
            geom.Figures.Add(figure);
            geom.Freeze();

            if (outline)
            {
                Pen outPen = new Pen(outlineBrush, thickness + (outlineThick * 2));
                outPen.Freeze();
                dc.DrawGeometry(null, outPen, geom);
            }

            Pen inPen = new Pen(mainBrush, thickness);
            inPen.Freeze();
            dc.DrawGeometry(null, inPen, geom);
        }

        private static void DrawBoxSquare(DrawingContext dc, double cx, double cy, double radius, double thickness, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            Rect rect = new Rect(cx - radius, cy - radius, radius * 2, radius * 2);

            if (outline)
            {
                Pen outPen = new Pen(outlineBrush, thickness + (outlineThick * 2));
                outPen.Freeze();
                dc.DrawRectangle(null, outPen, rect);
            }

            Pen inPen = new Pen(mainBrush, thickness);
            inPen.Freeze();
            dc.DrawRectangle(null, inPen, rect);
        }

        private static void DrawPolygon(DrawingContext dc, double cx, double cy, double radius, int sides, double thickness, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            if (radius <= 0 || sides < 3) return;
            PathGeometry geom = new PathGeometry();
            double step = (Math.PI * 2) / sides;
            double offsetAngle = Math.PI / sides;
            Point start = new Point(cx + radius * Math.Cos(offsetAngle), cy + radius * Math.Sin(offsetAngle));
            PathFigure figure = new PathFigure { StartPoint = start, IsClosed = true };

            for (int i = 1; i < sides; i++)
            {
                double angle = offsetAngle + (i * step);
                figure.Segments.Add(new LineSegment(new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)), true));
            }
            geom.Figures.Add(figure);
            geom.Freeze();

            if (outline)
            {
                Pen outPen = new Pen(outlineBrush, thickness + (outlineThick * 2));
                outPen.Freeze();
                dc.DrawGeometry(null, outPen, geom);
            }

            Pen inPen = new Pen(mainBrush, thickness);
            inPen.Freeze();
            dc.DrawGeometry(null, inPen, geom);
        }

        private static void DrawCircle(DrawingContext dc, double cx, double cy, double radius, double thickness, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            if (radius <= 0) return;

            if (outline)
            {
                Pen outPen = new Pen(outlineBrush, thickness + (outlineThick * 2));
                outPen.Freeze();
                dc.DrawEllipse(null, outPen, new Point(cx, cy), radius, radius);
            }

            Pen inPen = new Pen(mainBrush, thickness);
            inPen.Freeze();
            dc.DrawEllipse(null, inPen, new Point(cx, cy), radius, radius);
        }

        private static void DrawCornerDots(DrawingContext dc, double cx, double cy, double dist, double dotSize, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            double r = Math.Max(1.5, dotSize * 0.5);
            Point[] pts = {
                new Point(cx - dist, cy - dist),
                new Point(cx + dist, cy - dist),
                new Point(cx - dist, cy + dist),
                new Point(cx + dist, cy + dist)
            };

            foreach (var pt in pts)
            {
                if (outline)
                {
                    dc.DrawEllipse(outlineBrush, null, pt, r + outlineThick, r + outlineThick);
                }
                dc.DrawEllipse(mainBrush, null, pt, r, r);
            }
        }

        private static void DrawCardinalDots(DrawingContext dc, double cx, double cy, double dist, double dotSize, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            double r = Math.Max(1.5, dotSize * 0.5);
            Point[] pts = {
                new Point(cx - dist, cy - dist),
                new Point(cx, cy + dist),
                new Point(cx - dist, cy),
                new Point(cx + dist, cy)
            };

            foreach (var pt in pts)
            {
                if (outline)
                {
                    dc.DrawEllipse(outlineBrush, null, pt, r + outlineThick, r + outlineThick);
                }
                dc.DrawEllipse(mainBrush, null, pt, r, r);
            }
        }

        private static void DrawApexTriDot(DrawingContext dc, double cx, double cy, double dist, double dotSize, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            double r = Math.Max(1.5, (dotSize > 0 ? dotSize : 3.0) * 0.5);
            Point[] pts = {
                new Point(cx, cy - dist),
                new Point(cx - dist * 0.866, cy + dist * 0.5),
                new Point(cx + dist * 0.866, cy + dist * 0.5)
            };

            foreach (var pt in pts)
            {
                if (outline)
                {
                    dc.DrawEllipse(outlineBrush, null, pt, r + outlineThick, r + outlineThick);
                }
                dc.DrawEllipse(mainBrush, null, pt, r, r);
            }
        }

        private static void DrawMilDots(DrawingContext dc, double cx, double cy, double gap, double len, double dotSize, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            double r = Math.Max(1.2, dotSize * 0.35);
            double step = (len - gap) / 3.0;
            if (step <= 1) step = 4.0;

            for (int i = 1; i <= 2; i++)
            {
                double d = gap + (step * i);
                Point[] pts = {
                    new Point(cx, cy - d),
                    new Point(cx, cy + d),
                    new Point(cx - d, cy),
                    new Point(cx + d, cy)
                };
                foreach (var pt in pts)
                {
                    if (outline) dc.DrawEllipse(outlineBrush, null, pt, r + outlineThick, r + outlineThick);
                    dc.DrawEllipse(mainBrush, null, pt, r, r);
                }
            }
        }

        private static void DrawHeart(DrawingContext dc, double cx, double cy, double radius, double thickness, Brush mainBrush, Brush outlineBrush, bool outline, double outlineThick)
        {
            double r = Math.Max(5, radius * 0.65);
            PathGeometry geom = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = new Point(cx, cy + r * 1.2), IsClosed = true };
            
            figure.Segments.Add(new BezierSegment(
                new Point(cx - r * 1.6, cy + r * 0.2),
                new Point(cx - r * 1.6, cy - r * 0.9),
                new Point(cx, cy - r * 0.4),
                true));

            figure.Segments.Add(new BezierSegment(
                new Point(cx + r * 1.6, cy - r * 0.9),
                new Point(cx + r * 1.6, cy + r * 0.2),
                new Point(cx, cy + r * 1.2),
                true));

            geom.Figures.Add(figure);
            geom.Freeze();

            if (outline)
            {
                Pen outPen = new Pen(outlineBrush, thickness + (outlineThick * 2));
                outPen.Freeze();
                dc.DrawGeometry(null, outPen, geom);
            }

            Pen inPen = new Pen(mainBrush, thickness);
            inPen.Freeze();
            dc.DrawGeometry(null, inPen, geom);
        }

        public static Color ParseColor(string hex, double opacity = 1.0)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return Colors.White;
                hex = hex.Trim().TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte a = (byte)(Math.Clamp(opacity, 0.0, 1.0) * 255);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch { }
            return Colors.White;
        }
    }
}
