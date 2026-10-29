using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Cad2Gdml.Geometry;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Sketch
{
    // Модель сечения
    public sealed class SketchModel
    {
        public List<SketchRegion> Regions;
        public List<string> Captions;
    }

    // Регион сечения
    public sealed class SketchRegion
    {
        public List<Vec3d> Points;
        public bool IsClosed;
        public PartRole Role;
        public string Label;
    }

    public static class GdiSketchRenderer
    {
        public static Bitmap Render(SketchModel model, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.White);
                bool empty = model == null || model.Regions == null || model.Regions.Count == 0;
                if (empty)
                {
                    DrawEmpty(g, width, height);
                }
                else
                {
                    float sc, ox, oy;
                    ComputeTransform(model, width, height, out sc, out ox, out oy);
                    DrawPolys(g, model, sc, ox, oy);
                    DrawLabels(g, model, sc, ox, oy);
                    DrawCaption(g, model);
                    DrawScaleBar(g, sc, height);
                    DrawAxisArrow(g, sc, ox, oy, height);
                }
            }
            return bmp;
        }

        public static void SavePng(SketchModel model, string path, int width, int height)
        {
            Bitmap bmp = Render(model, width, height);
            using (Bitmap img = bmp)
            {
                img.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        // Масштаб и смещение: x → горизонталь, z → вертикаль (вверх)
        private static void ComputeTransform(SketchModel m, int w, int h, out float scale, out float ox, out float oy)
        {
            float mnX = float.MaxValue, mxX = float.MinValue, mnZ = float.MaxValue, mxZ = float.MinValue;
            foreach (SketchRegion r in m.Regions)
            {
                if (r.Points == null) continue;
                foreach (Vec3d p in r.Points)
                {
                    float x = (float)p.X, z = (float)p.Z;
                    if (x < mnX) mnX = x;
                    if (x > mxX) mxX = x;
                    if (z < mnZ) mnZ = z;
                    if (z > mxZ) mxZ = z;
                }
            }
            float spanX = Math.Max(mxX - mnX, 1f);
            float spanZ = Math.Max(mxZ - mnZ, 1f);
            const float mg = 40f;
            scale = Math.Min((w - 2 * mg) / spanX, (h - 2 * mg) / spanZ);
            float cx = (mnX + mxX) / 2f, cz = (mnZ + mxZ) / 2f;
            ox = w / 2f - cx * scale;
            oy = h / 2f + cz * scale;
        }

        // Мировой (x,z) → экранный (x, y), +z вверх
        private static PointF TS(float wx, float wz, float sc, float ox, float oy)
        {
            return new PointF(ox + wx * sc, oy - wz * sc);
        }

        // Заполнение и обводка полигонов
        private static void DrawPolys(Graphics g, SketchModel m, float sc, float ox, float oy)
        {
            using (Pen pen = new Pen(Color.FromArgb(60, 60, 60), 1.5f))
            {
                foreach (SketchRegion r in m.Regions)
                {
                    if (r.Points == null || r.Points.Count < 2) continue;
                    PointF[] pts = new PointF[r.Points.Count];
                    for (int i = 0; i < r.Points.Count; i++)
                        pts[i] = TS((float)r.Points[i].X, (float)r.Points[i].Z, sc, ox, oy);
                    Color fc = HexColor(Palette.ColorHex(r.Role, r.Label));
                    if (r.IsClosed)
                    {
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(120, fc.R, fc.G, fc.B)))
                            g.FillPolygon(br, pts);
                        g.DrawPolygon(pen, pts);
                    }
                    else
                    {
                        g.DrawLines(pen, pts);
                    }
                }
            }
        }

        // Подписи деталей (9 pt, по центру) с анти-наложением
        private static void DrawLabels(Graphics g, SketchModel m, float sc, float ox, float oy)
        {
            using (Font f = new Font("Segoe UI", 9f))
            using (SolidBrush br = new SolidBrush(Color.FromArgb(60, 60, 60)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center })
            using (Pen leaderPen = new Pen(Color.FromArgb(60, 60, 60), 1f))
            {
                List<RectangleF> drawn = new List<RectangleF>();

                foreach (SketchRegion r in m.Regions)
                {
                    if (r.Points == null || r.Points.Count < 2) continue;
                    if (string.IsNullOrEmpty(r.Label)) continue;

                    float cx = 0, cz = 0;
                    foreach (Vec3d p in r.Points) { cx += (float)p.X; cz += (float)p.Z; }
                    cx /= r.Points.Count; cz /= r.Points.Count;

                    PointF anchor = TS(cx, cz, sc, ox, oy);

                    // Измеряем текст
                    SizeF sz = g.MeasureString(r.Label, f);
                    float w = sz.Width;
                    float h = sz.Height;

                    // Прямоугольник по центру (StringFormat центрирует)
                    RectangleF rect = new RectangleF(anchor.X - w / 2f, anchor.Y - h / 2f, w, h);

                    // Проверяем пересечение с уже нарисованными (с увеличением 1 px)
                    if (!IntersectsAny(drawn, rect))
                    {
                        g.DrawString(r.Label, f, br, anchor, sf);
                        drawn.Add(rect);
                    }
                    else
                    {
                        // Пробы вертикального сдвига
                        float[] offsets = new float[] { h, -h, 2f * h, -2f * h, 3f * h, -3f * h };
                        for (int i = 0; i < offsets.Length; i++)
                        {
                            PointF shifted = new PointF(anchor.X, anchor.Y + offsets[i]);
                            RectangleF tryRect = new RectangleF(shifted.X - w / 2f, shifted.Y - h / 2f, w, h);
                            if (!IntersectsAny(drawn, tryRect))
                            {
                                // Линия-указатель от якоря к смещённому центру
                                g.DrawLine(leaderPen, anchor.X, anchor.Y, shifted.X, shifted.Y);
                                g.DrawString(r.Label, f, br, shifted, sf);
                                drawn.Add(tryRect);
                                break;
                            }
                        }
                        // Если ни одно место не подошло — пропускаем
                    }
                }
            }
        }

        // Пересечение прямоугольника (увеличенного на 1 px) с набором
        private static bool IntersectsAny(List<RectangleF> rects, RectangleF r)
        {
            RectangleF inflated = RectangleF.Inflate(r, 1f, 1f);
            for (int i = 0; i < rects.Count; i++)
            {
                if (inflated.IntersectsWith(rects[i]))
                    return true;
            }
            return false;
        }

        // Кепшион слева-сверху (10 pt bold)
        private static void DrawCaption(Graphics g, SketchModel m)
        {
            if (m.Captions == null || m.Captions.Count == 0) return;
            using (Font f = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            using (SolidBrush fg = new SolidBrush(Color.FromArgb(32, 32, 32)))
            {
                float maxW = 0;
                foreach (string line in m.Captions)
                    maxW = Math.Max(maxW, g.MeasureString(line, f).Width);
                float lh = 15f, pad = 5f;
                float bh = m.Captions.Count * lh + 2 * pad;
                g.FillRectangle(bg, 6f, 6f, maxW + 2 * pad, bh);
                float y = 6f + pad;
                foreach (string line in m.Captions) { g.DrawString(line, f, fg, 6f + pad, y); y += lh; }
            }
        }

        // Масштабная линейка (10/20/50/100 мм), внизу-слева
        private static void DrawScaleBar(Graphics g, float sc, int height)
        {
            int mm = 10;
            foreach (int c in new[] { 100, 50, 20, 10 })
            {
                if (c * sc >= 30f && c * sc <= 160f) { mm = c; break; }
            }
            float len = mm * sc, x0 = 40f, y0 = height - 28f;
            using (Pen p = new Pen(Color.FromArgb(32, 32, 32), 2f))
            {
                g.DrawLine(p, x0, y0, x0 + len, y0);
                g.DrawLine(p, x0, y0 - 4f, x0, y0 + 4f);
                g.DrawLine(p, x0 + len, y0 - 4f, x0 + len, y0 + 4f);
            }
            using (Font f = new Font("Segoe UI", 8f))
            using (SolidBrush br = new SolidBrush(Color.FromArgb(32, 32, 32)))
                g.DrawString(mm.ToString(System.Globalization.CultureInfo.InvariantCulture) + " мм", f, br, x0 + len / 2f - 12f, y0 + 5f);
        }

        // Стрелка оси +z
        private static void DrawAxisArrow(Graphics g, float sc, float ox, float oy, int height)
        {
            float x = ox; // x_world = 0
            float baseY = oy;
            float tipY = Math.Max(30f, oy - 40f * sc);
            if (tipY >= baseY - 5f) return;
            using (Pen p = new Pen(Color.FromArgb(100, 100, 100), 1.2f))
            {
                g.DrawLine(p, x, baseY, x, tipY);
                g.DrawLine(p, x, tipY, x - 4f, tipY + 8f);
                g.DrawLine(p, x, tipY, x + 4f, tipY + 8f);
            }
        }

        // Пустая сцена
        private static void DrawEmpty(Graphics g, int w, int h)
        {
            using (Font f = new Font("Segoe UI", 12f, FontStyle.Italic))
            using (SolidBrush br = new SolidBrush(Color.Gray))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString("\u043F\u0443\u0441\u0442\u043E", f, br, new RectangleF(0, 0, w, h), sf);
        }

        // Hex → Color
        private static Color HexColor(string hex)
        {
            if (hex == null || hex.Length != 6) return Color.Gray;
            return Color.FromArgb(
                int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
