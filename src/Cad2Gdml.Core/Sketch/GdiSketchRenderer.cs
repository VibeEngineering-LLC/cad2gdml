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

        // Подписи деталей: режим 1 — обычная надпись, режим 2 — нумерованный бейдж
        private static void DrawLabels(Graphics g, SketchModel m, float sc, float ox, float oy)
        {
            using (Font labelFont = new Font("Segoe UI", 9f))
            using (SolidBrush labelBr = new SolidBrush(Color.FromArgb(60, 60, 60)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                List<RectangleF> drawn = new List<RectangleF>();
                List<KeyValuePair<int, string>> badges = new List<KeyValuePair<int, string>>(); // n → Label

                int badgeNum = 0;

                foreach (SketchRegion r in m.Regions)
                {
                    if (r.Points == null || r.Points.Count < 2) continue;
                    if (string.IsNullOrEmpty(r.Label)) continue;

                    // Экранный bbox региона
                    float bnX = float.MaxValue, bxX = float.MinValue, bnZ = float.MaxValue, bxZ = float.MinValue;
                    foreach (Vec3d p in r.Points)
                    {
                        PointF sp = TS((float)p.X, (float)p.Z, sc, ox, oy);
                        if (sp.X < bnX) bnX = sp.X;
                        if (sp.X > bxX) bxX = sp.X;
                        if (sp.Y < bnZ) bnZ = sp.Y;
                        if (sp.Y > bxZ) bxZ = sp.Y;
                    }
                    float boxW = bxX - bnX;
                    float boxH = bxZ - bnZ;

                    // Центр региона
                    float cx = 0, cz = 0;
                    foreach (Vec3d p in r.Points) { cx += (float)p.X; cz += (float)p.Z; }
                    cx /= r.Points.Count; cz /= r.Points.Count;
                    PointF anchor = TS(cx, cz, sc, ox, oy);

                    // Размер надписи
                    SizeF sz = g.MeasureString(r.Label, labelFont);
                    float lw = sz.Width;
                    float lh = sz.Height;

                    // Режим 1: надпись помещается в bbox и не пересекается
                    bool fitsW = lw <= 0.95f * boxW;
                    bool fitsH = lh <= boxH;
                    RectangleF plainRect = new RectangleF(anchor.X - lw / 2f, anchor.Y - lh / 2f, lw, lh);

                    if (fitsW && fitsH && !IntersectsAny(drawn, plainRect))
                    {
                        g.DrawString(r.Label, labelFont, labelBr, anchor, sf);
                        drawn.Add(plainRect);
                        continue;
                    }

                    // Режим 2: нумерованный бейдж
                    badgeNum++;
                    int n = badgeNum;
                    float br = 8f; // радиус круга
                    float badgeSize = 16f; // квадрат 16x16

                    PointF badgePos = anchor;
                    bool shifted = false;

                    RectangleF badgeRect = new RectangleF(anchor.X - br, anchor.Y - br, badgeSize, badgeSize);
                    if (IntersectsAny(drawn, badgeRect))
                    {
                        // Попытки со сдвигом
                        float[][] offsets = new float[][]
                        {
                            new float[] { -18f, 0f }, new float[] { 18f, 0f },
                            new float[] { 0f, -18f }, new float[] { 0f, 18f },
                            new float[] { -18f, -18f }, new float[] { 18f, -18f },
                            new float[] { -18f, 18f }, new float[] { 18f, 18f },
                            new float[] { -36f, 0f }, new float[] { 36f, 0f },
                            new float[] { 0f, -36f }, new float[] { 0f, 36f }
                        };

                        for (int i = 0; i < offsets.Length; i++)
                        {
                            PointF tryPos = new PointF(anchor.X + offsets[i][0], anchor.Y + offsets[i][1]);
                            RectangleF tryRect = new RectangleF(tryPos.X - br, tryPos.Y - br, badgeSize, badgeSize);
                            if (!IntersectsAny(drawn, tryRect))
                            {
                                badgePos = tryPos;
                                shifted = true;
                                break;
                            }
                        }
                        // Если ни одно не подошло — оставляем anchor (никогда не теряем)
                    }

                    // Линия-указатель
                    if (shifted)
                    {
                        using (Pen leaderPen = new Pen(Color.FromArgb(60, 60, 60), 1f))
                            g.DrawLine(leaderPen, anchor.X, anchor.Y, badgePos.X, badgePos.Y);
                    }

                    // Круг бейджа
                    using (SolidBrush bg = new SolidBrush(Color.White))
                    using (Pen outline = new Pen(Color.FromArgb(40, 40, 40), 1f))
                    {
                        g.FillEllipse(bg, badgePos.X - br, badgePos.Y - br, badgeSize, badgeSize);
                        g.DrawEllipse(outline, badgePos.X - br, badgePos.Y - br, badgeSize, badgeSize);
                    }

                    // Номер в круге
                    using (Font numFont = new Font("Segoe UI", 8f, FontStyle.Bold))
                    using (SolidBrush numBr = new SolidBrush(Color.FromArgb(32, 32, 32)))
                    {
                        string numStr = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        g.DrawString(numStr, numFont, numBr, badgePos, sf);
                    }

                    drawn.Add(new RectangleF(badgePos.X - br, badgePos.Y - br, badgeSize, badgeSize));
                    badges.Add(new KeyValuePair<int, string>(n, r.Label));
                }

                // Легенда бейджей — верхний правый угол
                if (badges.Count > 0)
                {
                    RectangleF clip = g.VisibleClipBounds;
                    float bmpW = clip.Width;
                    float bmpH = clip.Height;
                    float maxLineW = bmpW * 0.45f;
                    float pad = 4f;
                    float lineH = 14f;

                    using (Font legFont = new Font("Segoe UI", 8.5f))
                    {
                        // Измеряем максимальную ширину
                        float maxTextW = 0;
                        foreach (KeyValuePair<int, string> b in badges)
                        {
                            string line = b.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + " \u2014 " + b.Value;
                            float tw = g.MeasureString(line, legFont).Width;
                            if (tw > maxTextW) maxTextW = tw;
                        }
                        if (maxTextW > maxLineW) maxTextW = maxLineW;

                        float blockW = maxTextW + 2 * pad;
                        float blockH = badges.Count * lineH + 2 * pad;

                        // Позиция: правый верхний угол, 8 px от края
                        float bx = bmpW - 8f - blockW;
                        float by = 8f;

                        // Не выходить за пределы
                        if (bx < 0) bx = 0;
                        if (by + blockH > bmpH) by = Math.Max(0, bmpH - blockH);

                        // Фон
                        using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(217, 255, 255, 255)))
                        using (Pen bgPen = new Pen(Color.FromArgb(180, 120, 120, 120), 1f))
                        {
                            g.FillRectangle(bgBrush, bx, by, blockW, blockH);
                            g.DrawRectangle(bgPen, bx, by, blockW, blockH);
                        }

                        // Строки легенды
                        using (SolidBrush fgBrush = new SolidBrush(Color.FromArgb(32, 32, 32)))
                        using (StringFormat legSf = new StringFormat
                        {
                            Alignment = StringAlignment.Near,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        })
                        {
                            for (int i = 0; i < badges.Count; i++)
                            {
                                string line = badges[i].Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + " \u2014 " + badges[i].Value;
                                float ly = by + pad + i * lineH;
                                // Прямоугольник для обрезки
                                float textW = Math.Min(maxTextW, maxLineW);
                                RectangleF textRect = new RectangleF(bx + pad, ly, textW, lineH);
                                g.DrawString(line, legFont, fgBrush, textRect, legSf);
                            }
                        }
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
