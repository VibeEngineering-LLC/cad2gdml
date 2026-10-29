using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Cad2Gdml.Sketch
{
    // Программный Z-буфер для 3D-предпросмотра
    public sealed class ZBuffer
    {
        public int Width;
        public int Height;
        private int[] Color;
        private float[] Depth;

        public ZBuffer(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Размеры должны быть положительными");
            Width = width;
            Height = height;
            Color = new int[width * height];
            Depth = new float[width * height];
        }

        // Очистка буфера
        public void Clear(int argb)
        {
            for (int i = 0; i < Color.Length; i++) Color[i] = argb;
            for (int i = 0; i < Depth.Length; i++) Depth[i] = float.MaxValue;
        }

        // Треугольник с Z-тестом (оба вращения)
        public void FillTriangle(float x0, float y0, float d0,
                                 float x1, float y1, float d1,
                                 float x2, float y2, float d2, int argb)
        {
            // Коэффициенты рёбер
            float ax = x1 - x0, ay = y1 - y0;
            float bx = x2 - x1, by = y2 - y1;
            float cx = x0 - x2, cy = y0 - y2;

            // Удвоенная ориентированная площадь
            float area2 = ax * (y2 - y0) - ay * (x2 - x0);
            if (Math.Abs(area2) < 1e-6f) return;

            // Ограничивающий прямоугольник, обрезаемый к буферу
            int minPx = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
            int maxPx = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minPy = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
            int maxPy = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
            if (minPx > maxPx || minPy > maxPy) return;

            bool ccw = area2 > 0f;
            float invArea = 1.0f / area2;

            for (int py = minPy; py <= maxPy; py++)
            {
                float fy = py + 0.5f;
                int rowOff = py * Width;
                for (int px = minPx; px <= maxPx; px++)
                {
                    float fx = px + 0.5f;

                    // Функции рёбер (веса барицентрических координат)
                    float w0 = bx * (fy - y1) - by * (fx - x1);
                    float w1 = cx * (fy - y2) - cy * (fx - x2);
                    float w2 = ax * (fy - y0) - ay * (fx - x0);

                    // Внутренность: >= 0 при CCW, <= 0 при CW
                    if (ccw)
                    {
                        if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                    }
                    else
                    {
                        if (w0 > 0f || w1 > 0f || w2 > 0f) continue;
                    }

                    // Барицентрическая интерполяция глубины
                    float depth = (w0 * d0 + w1 * d1 + w2 * d2) * invArea;

                    int idx = rowOff + px;
                    if (depth < Depth[idx])
                    {
                        Depth[idx] = depth;
                        Color[idx] = argb;
                    }
                }
            }
        }

        // Простая линия DDA, без Z-теста (для оверлеев)
        public void DrawLine(float x0, float y0, float x1, float y1, int argb)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)));
            if (steps <= 0)
            {
                int px = (int)Math.Floor(x0), py = (int)Math.Floor(y0);
                if (px >= 0 && px < Width && py >= 0 && py < Height)
                    Color[py * Width + px] = argb;
                return;
            }
            float ix = dx / steps, iy = dy / steps;
            float cx = x0, cy = y0;
            for (int i = 0; i <= steps; i++)
            {
                int px = (int)Math.Floor(cx), py = (int)Math.Floor(cy);
                if (px >= 0 && px < Width && py >= 0 && py < Height)
                    Color[py * Width + px] = argb;
                cx += ix;
                cy += iy;
            }
        }

        // Преобразование в Bitmap 32bppArgb
        public Bitmap ToBitmap()
        {
            Bitmap bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, Width, Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(Color, 0, data.Scan0, Color.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
    }
}
