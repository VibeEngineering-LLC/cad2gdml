using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;
using Cad2Gdml.Sketch;

namespace Cad2Gdml.App
{
    // Программный 3D-просмотрер сцены: вращение, зум, сечение, подсветка
    public sealed class MeshView : Control
    {
        private List<Part> _parts = new List<Part>();
        private List<int[]> _partTris = new List<int[]>();
        private Vec3d _centre;
        private double _radius = 1.0;
        private Vec3d _bboxMin;
        private Vec3d _bboxMax;

        private double _yaw = 30.0;
        private double _pitch = 20.0;
        private double _zoom = 1.0;
        private double _panX;
        private double _panY;

        private bool _sectionEnabled;
        private int _sectionAxis;
        private double _sectionPos = 0.5;

        private string _highlight;

        private bool _leftDown;
        private bool _rightDown;
        private Point _lastMouse;

        public event EventHandler ViewChanged;

        public MeshView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        // Подсветка части по имени
        public string Highlight
        {
            get { return _highlight; }
            set { _highlight = value; Invalidate(); }
        }

        public bool SectionEnabled
        {
            get { return _sectionEnabled; }
            set { _sectionEnabled = value; Invalidate(); }
        }

        // Ось сечения: 0=x, 1=y, 2=z
        public int SectionAxis
        {
            get { return _sectionAxis; }
            set { _sectionAxis = value; Invalidate(); }
        }

        // Позиция сечения 0..1
        public double SectionPos
        {
            get { return _sectionPos; }
            set { _sectionPos = value; Invalidate(); }
        }

        // Скрытые части по имени
        public HashSet<string> Hidden { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Перерисовать после изменения Hidden
        public void RefreshHidden() { Invalidate(); }

        // Установить сцену и сбросить вид
        public void SetScene(SceneModel scene)
        {
            _parts.Clear();
            _partTris.Clear();

            if (scene == null) return;

            foreach (Part p in scene.Parts)
            {
                if (p.Mesh != null && p.Mesh.TriangleCount > 0)
                {
                    _parts.Add(p);
                }
            }

            if (_parts.Count == 0)
            {
                _bboxMin = new Vec3d(-1, -1, -1);
                _bboxMax = new Vec3d(1, 1, 1);
                _centre = Vec3d.Zero;
                _radius = 1.0;
                ResetViewInternal();
                return;
            }

            // Габариты сцены
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (Part p in _parts)
            {
                Vec3d lo, hi;
                p.Mesh.GetBounds(out lo, out hi);
                if (lo.X < minX) minX = lo.X;
                if (lo.Y < minY) minY = lo.Y;
                if (lo.Z < minZ) minZ = lo.Z;
                if (hi.X > maxX) maxX = hi.X;
                if (hi.Y > maxY) maxY = hi.Y;
                if (hi.Z > maxZ) maxZ = hi.Z;
            }

            _bboxMin = new Vec3d(minX, minY, minZ);
            _bboxMax = new Vec3d(maxX, maxY, maxZ);
            _centre = new Vec3d((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            _radius = Math.Sqrt(dx * dx + dy * dy + dz * dz) * 0.5;
            if (_radius < 1e-9) _radius = 1.0;

            ResetViewInternal();
            ComputeTriangleBudgets();
            Invalidate();
        }

        // Сброс вида
        public void ResetView()
        {
            ResetViewInternal();
            Invalidate();
            RaiseViewChanged();
        }

        private void ResetViewInternal()
        {
            _yaw = 30.0;
            _pitch = 20.0;
            _zoom = 1.0;
            _panX = 0;
            _panY = 0;
            _sectionEnabled = false;
        }

        private void RaiseViewChanged()
        {
            EventHandler h = ViewChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // Бюджет треугольников: не более 40000 суммарно, минимум 12 на часть
        private void ComputeTriangleBudgets()
        {
            const int maxTotal = 40000;
            const int minPer = 12;

            int totalTris = 0;
            foreach (Part p in _parts) totalTris += p.Mesh.TriangleCount;

            if (totalTris <= maxTotal)
            {
                foreach (Part p in _parts)
                {
                    int cnt = p.Mesh.TriangleCount;
                    int[] idx = new int[cnt];
                    for (int i = 0; i < cnt; i++) idx[i] = i;
                    _partTris.Add(idx);
                }
                return;
            }

            // Пропорциональное распределение с минимумом
            int totalAboveMin = 0;
            int[] extra = new int[_parts.Count];
            for (int i = 0; i < _parts.Count; i++)
            {
                int tc = _parts[i].Mesh.TriangleCount;
                extra[i] = tc - minPer;
                if (extra[i] < 0) extra[i] = 0;
                totalAboveMin += extra[i];
            }

            int remaining = maxTotal - _parts.Count * minPer;
            if (remaining < 0) remaining = 0;

            int[] budgets = new int[_parts.Count];
            for (int i = 0; i < _parts.Count; i++)
            {
                if (totalAboveMin > 0)
                {
                    int share = (int)((double)extra[i] / totalAboveMin * remaining);
                    budgets[i] = minPer + share;
                }
                else
                {
                    budgets[i] = minPer;
                }
                if (budgets[i] > _parts[i].Mesh.TriangleCount) budgets[i] = _parts[i].Mesh.TriangleCount;
                if (budgets[i] < 1) budgets[i] = 1;
            }

            for (int pi = 0; pi < _parts.Count; pi++)
            {
                int tc = _parts[pi].Mesh.TriangleCount;
                int k = (tc + budgets[pi] - 1) / budgets[pi];
                if (k < 1) k = 1;

                int kept = 0;
                for (int i = 0; i < tc; i += k) kept++;

                int[] idx = new int[kept];
                int j = 0;
                for (int i = 0; i < tc; i += k) idx[j++] = i;
                _partTris.Add(idx);
            }
        }

        // Вращение: yaw вокруг Z, pitch вокруг X (стандартный turntable, Z — вверх)
        private static void RotatePoint(double x, double y, double z,
            double yawR, double pitchR, out double rx, out double ry, out double rz)
        {
            double cy = Math.Cos(yawR), sy = Math.Sin(yawR);
            double cp = Math.Cos(pitchR), sp = Math.Sin(pitchR);

            // Yaw вокруг Z
            double x1 = x * cy - y * sy;
            double y1 = x * sy + y * cy;
            double z1 = z;

            // Pitch вокруг X
            rx = x1;
            ry = y1 * cp - z1 * sp;
            rz = y1 * sp + z1 * cp;
        }

        // Проекция точки сцены на экран
        private void Project(double x, double y, double z,
            double yawR, double pitchR, double scale, double cx, double cyy,
            out float sx, out float sy, out double depth)
        {
            double rx, ry, rz;
            RotatePoint(x, y, z, yawR, pitchR, out rx, out ry, out rz);
            sx = (float)(rx * scale + cx);
            sy = (float)(-rz * scale + cyy);
            depth = ry;
        }

        // Цвет части из палитры
        private static Color GetPartColor(Part part)
        {
            try
            {
                string hex = Palette.ColorHex(part.Role, part.Name);
                if (hex != null && hex.Length == 6)
                {
                    int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Color.FromArgb(r, g, b);
                }
            }
            catch
            {
                // Игнорируем
            }
            return Color.FromArgb(128, 128, 128);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _lastMouse = e.Location;
            if (e.Button == MouseButtons.Left) _leftDown = true;
            if (e.Button == MouseButtons.Right) _rightDown = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) _leftDown = false;
            if (e.Button == MouseButtons.Right) _rightDown = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int dx = e.X - _lastMouse.X;
            int dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;

            if (_leftDown)
            {
                _yaw += dx * 0.5;
                _pitch += dy * 0.5;
                if (_pitch > 89.0) _pitch = 89.0;
                if (_pitch < -89.0) _pitch = -89.0;
                Invalidate();
                RaiseViewChanged();
            }
            else if (_rightDown)
            {
                _panX += dx;
                _panY += dy;
                Invalidate();
                RaiseViewChanged();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0) _zoom *= 1.1;
            else _zoom /= 1.1;
            if (_zoom > 20.0) _zoom = 20.0;
            if (_zoom < 0.1) _zoom = 0.1;
            Invalidate();
            RaiseViewChanged();
        }

        // Клип треугольника по полупространству coord <= planePos (Sutherland-Hodgman)
        // Возвращает 0, 1 или 2 треугольника в outTris (до 6 Vec3d)
        private static int ClipTriangle(Vec3d a, Vec3d b, Vec3d c, int axis, double planePos, Vec3d[] outTris)
        {
            double ca = axis == 0 ? a.X : (axis == 1 ? a.Y : a.Z);
            double cb = axis == 0 ? b.X : (axis == 1 ? b.Y : b.Z);
            double cc = axis == 0 ? c.X : (axis == 1 ? c.Y : c.Z);

            bool aIn = ca <= planePos;
            bool bIn = cb <= planePos;
            bool cIn = cc <= planePos;

            Vec3d[] cv = new Vec3d[4];
            int cn = 0;

            // Ребро a->b
            if (aIn && bIn) { cv[cn++] = b; }
            else if (aIn && !bIn)
            {
                double t = (planePos - ca) / (cb - ca);
                cv[cn++] = new Vec3d(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
            }
            else if (!aIn && bIn)
            {
                double t = (planePos - ca) / (cb - ca);
                cv[cn++] = new Vec3d(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
                cv[cn++] = b;
            }

            // Ребро b->c
            if (bIn && cIn) { cv[cn++] = c; }
            else if (bIn && !cIn)
            {
                double t = (planePos - cb) / (cc - cb);
                cv[cn++] = new Vec3d(b.X + t * (c.X - b.X), b.Y + t * (c.Y - b.Y), b.Z + t * (c.Z - b.Z));
            }
            else if (!bIn && cIn)
            {
                double t = (planePos - cb) / (cc - cb);
                cv[cn++] = new Vec3d(b.X + t * (c.X - b.X), b.Y + t * (c.Y - b.Y), b.Z + t * (c.Z - b.Z));
                cv[cn++] = c;
            }

            // Ребро c->a
            if (cIn && aIn) { cv[cn++] = a; }
            else if (cIn && !aIn)
            {
                double t = (planePos - cc) / (ca - cc);
                cv[cn++] = new Vec3d(c.X + t * (a.X - c.X), c.Y + t * (a.Y - c.Y), c.Z + t * (a.Z - c.Z));
            }
            else if (!cIn && aIn)
            {
                double t = (planePos - cc) / (ca - cc);
                cv[cn++] = new Vec3d(c.X + t * (a.X - c.X), c.Y + t * (a.Y - c.Y), c.Z + t * (a.Z - c.Z));
                cv[cn++] = a;
            }

            if (cn < 3) return 0;

            if (cn == 3)
            {
                outTris[0] = cv[0]; outTris[1] = cv[1]; outTris[2] = cv[2];
                return 1;
            }

            // 4 вершины: fan от cv[0]
            outTris[0] = cv[0]; outTris[1] = cv[1]; outTris[2] = cv[2];
            outTris[3] = cv[0]; outTris[4] = cv[2]; outTris[5] = cv[3];
            return 2;
        }

        // Основной отрисовщик
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            try
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.None;

                int w = Width, h = Height;
                if (w < 10 || h < 10 || _parts.Count == 0) return;

                double scale = _zoom * 0.45 * Math.Min(w, h) / _radius;
                double cx = w * 0.5 + _panX;
                double cyy = h * 0.5 + _panY;
                double yawR = _yaw * Math.PI / 180.0;
                double pitchR = _pitch * Math.PI / 180.0;

                // Свет в пространстве вида
                double lx = 0.3, ly = 0.5, lz = 0.8;
                double ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
                lx /= ll; ly /= ll; lz /= ll;

                // Позиция плоскости сечения
                double planePos = 0;
                if (_sectionEnabled)
                {
                    if (_sectionAxis == 0) planePos = _bboxMin.X + _sectionPos * (_bboxMax.X - _bboxMin.X);
                    else if (_sectionAxis == 1) planePos = _bboxMin.Y + _sectionPos * (_bboxMax.Y - _bboxMin.Y);
                    else planePos = _bboxMin.Z + _sectionPos * (_bboxMax.Z - _bboxMin.Z);
                }

                // Подсчёт всего
                int totalTris = 0;
                for (int pi = 0; pi < _parts.Count; pi++) totalTris += _partTris[pi].Length;
                if (totalTris == 0) { DrawGizmo(g, w, h); return; }

                // Массивы данных: каждый исходный треугольник может дать до 2 после клипа
                int maxOut = totalTris * 2;
                float[] depths = new float[maxOut];
                float[] triPts = new float[maxOut * 6];
                float[] triNrm = new float[maxOut * 3];
                int[] triPart = new int[maxOut];
                bool[] triHi = new bool[maxOut];

                int total = 0;

                for (int pi = 0; pi < _parts.Count; pi++)
                {
                    Part part = _parts[pi];
                    // Пропустить скрытую часть
                    if (Hidden.Contains(part.Name)) continue;

                    int[] keptIdx = _partTris[pi];
                    Mesh mesh = part.Mesh;
                    bool hi = (_highlight != null && part.Name == _highlight);

                    for (int ki = 0; ki < keptIdx.Length; ki++)
                    {
                        int ti = keptIdx[ki];
                        Vec3d a, b, c;
                        mesh.GetTriangle(ti, out a, out b, out c);

                        // Клип или пропуск без клипа
                        Vec3d[] triVerts = new Vec3d[6];
                        int nTris = 1;

                        if (_sectionEnabled)
                        {
                            nTris = ClipTriangle(a, b, c, _sectionAxis, planePos, triVerts);
                            if (nTris == 0) continue;
                        }
                        else
                        {
                            triVerts[0] = a; triVerts[1] = b; triVerts[2] = c;
                        }

                        // Обработка 1 или 2 треугольников
                        for (int tIdx = 0; tIdx < nTris; tIdx++)
                        {
                            Vec3d va = triVerts[tIdx * 3];
                            Vec3d vb = triVerts[tIdx * 3 + 1];
                            Vec3d vc = triVerts[tIdx * 3 + 2];

                            // Вращение относительно центра
                            double ax0 = va.X - _centre.X, ay0 = va.Y - _centre.Y, az0 = va.Z - _centre.Z;
                            double bx0 = vb.X - _centre.X, by0 = vb.Y - _centre.Y, bz0 = vb.Z - _centre.Z;
                            double cx0 = vc.X - _centre.X, cy0 = vc.Y - _centre.Y, cz0 = vc.Z - _centre.Z;

                            double ax, ay, az, bx, by, bz, cxr, cyyr, czr;
                            RotatePoint(ax0, ay0, az0, yawR, pitchR, out ax, out ay, out az);
                            RotatePoint(bx0, by0, bz0, yawR, pitchR, out bx, out by, out bz);
                            RotatePoint(cx0, cy0, cz0, yawR, pitchR, out cxr, out cyyr, out czr);

                            // Экран
                            int idx = total;
                            triPts[idx * 6 + 0] = (float)(ax * scale + cx);
                            triPts[idx * 6 + 1] = (float)(-az * scale + cyy);
                            triPts[idx * 6 + 2] = (float)(bx * scale + cx);
                            triPts[idx * 6 + 3] = (float)(-bz * scale + cyy);
                            triPts[idx * 6 + 4] = (float)(cxr * scale + cx);
                            triPts[idx * 6 + 5] = (float)(-czr * scale + cyy);

                            depths[idx] = (float)((ay + by + cyyr) / 3.0);
                            triPart[idx] = pi;
                            triHi[idx] = hi;

                            // Нормаль в пространстве вида
                            double e1x = bx - ax, e1y = by - ay, e1z = bz - az;
                            double e2x = cxr - ax, e2y = cyyr - ay, e2z = czr - az;
                            double nx = e1y * e2z - e1z * e2y;
                            double ny = e1z * e2x - e1x * e2z;
                            double nz = e1x * e2y - e1y * e2x;
                            double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                            if (nl > 1e-12)
                            {
                                triNrm[idx * 3 + 0] = (float)(nx / nl);
                                triNrm[idx * 3 + 1] = (float)(ny / nl);
                                triNrm[idx * 3 + 2] = (float)(nz / nl);
                            }
                            else
                            {
                                triNrm[idx * 3 + 0] = 0;
                                triNrm[idx * 3 + 1] = 1;
                                triNrm[idx * 3 + 2] = 0;
                            }

                            total++;
                        }
                    }
                }

                if (total == 0) { DrawGizmo(g, w, h); return; }

                // Разделение: не-подсветка / подсветка
                List<int> nonHi = new List<int>(total);
                List<int> hiList = new List<int>();
                for (int i = 0; i < total; i++)
                {
                    if (triHi[i]) hiList.Add(i);
                    else nonHi.Add(i);
                }

                // Сортировка по глубине (далекие первыми)
                nonHi.Sort(delegate(int a, int b) { return depths[b].CompareTo(depths[a]); });
                hiList.Sort(delegate(int a, int b) { return depths[b].CompareTo(depths[a]); });

                bool drawEdges = total < 12000;
                Pen edgePen = drawEdges ? new Pen(Color.FromArgb(60, 40, 40, 40), 1f) : null;
                bool hasHi = hiList.Count > 0;

                // Отрисовка не-подсвеченных
                for (int i = 0; i < nonHi.Count; i++)
                {
                    int idx = nonHi[i];
                    Part part = _parts[triPart[idx]];
                    Color baseC = GetPartColor(part);

                    float nx = triNrm[idx * 3 + 0], ny = triNrm[idx * 3 + 1], nz = triNrm[idx * 3 + 2];
                    float dot = nx * (float)lx + ny * (float)ly + nz * (float)lz;
                    if (dot < 0) dot = -dot;
                    float lam = 0.55f + 0.45f * dot;

                    int cr = Clamp255((int)(baseC.R * lam));
                    int cg = Clamp255((int)(baseC.G * lam));
                    int cb = Clamp255((int)(baseC.B * lam));

                    int alpha = hasHi ? 90 : 255;
                    Color fill = Color.FromArgb(alpha, cr, cg, cb);

                    Point[] pts = MakePts(triPts, idx);
                    using (SolidBrush br = new SolidBrush(fill))
                    {
                        g.FillPolygon(br, pts);
                    }
                    if (drawEdges) g.DrawPolygon(edgePen, pts);
                }

                // Отрисовка подсвеченной (оранжевая)
                for (int i = 0; i < hiList.Count; i++)
                {
                    int idx = hiList[i];
                    float nx = triNrm[idx * 3 + 0], ny = triNrm[idx * 3 + 1], nz = triNrm[idx * 3 + 2];
                    float dot = nx * (float)lx + ny * (float)ly + nz * (float)lz;
                    if (dot < 0) dot = -dot;
                    float lam = 0.55f + 0.45f * dot;

                    int cr = Clamp255((int)(255.0 * lam));
                    int cg = Clamp255((int)(140.0 * lam));
                    Color fill = Color.FromArgb(255, cr, cg, 0);

                    Point[] pts = MakePts(triPts, idx);
                    using (SolidBrush br = new SolidBrush(fill))
                    {
                        g.FillPolygon(br, pts);
                    }
                    if (drawEdges) g.DrawPolygon(edgePen, pts);
                }

                if (edgePen != null) edgePen.Dispose();

                // Плоскость сечения
                if (_sectionEnabled) DrawSectionPlane(g, scale, cx, cyy, yawR, pitchR, planePos);

                // Гизмо осей
                DrawGizmo(g, w, h);
            }
            catch (Exception ex)
            {
                // Небольшой текст об ошибке
                using (Font f = new Font(FontFamily.GenericSansSerif, 9f))
                using (Brush b = new SolidBrush(Color.Red))
                {
                    e.Graphics.DrawString("Ошибка: " + ex.Message, f, b, 10, 10);
                }
            }
        }

        private static Point[] MakePts(float[] arr, int idx)
        {
            return new Point[]
            {
                new Point((int)arr[idx * 6 + 0], (int)arr[idx * 6 + 1]),
                new Point((int)arr[idx * 6 + 2], (int)arr[idx * 6 + 3]),
                new Point((int)arr[idx * 6 + 4], (int)arr[idx * 6 + 5])
            };
        }

        private static int Clamp255(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }

        // Проекция плоскости сечения (4 угла)
        private void DrawSectionPlane(Graphics g, double scale, double cx, double cyy,
            double yawR, double pitchR, double planePos)
        {
            double mx = _bboxMin.X, my = _bboxMin.Y, mz = _bboxMin.Z;
            double Mx = _bboxMax.X, My = _bboxMax.Y, Mz = _bboxMax.Z;

            double[] px = new double[4], py = new double[4], pz = new double[4];

            if (_sectionAxis == 0)
            {
                px[0] = planePos; py[0] = my; pz[0] = mz;
                px[1] = planePos; py[1] = My; pz[1] = mz;
                px[2] = planePos; py[2] = My; pz[2] = Mz;
                px[3] = planePos; py[3] = my; pz[3] = Mz;
            }
            else if (_sectionAxis == 1)
            {
                px[0] = mx; py[0] = planePos; pz[0] = mz;
                px[1] = Mx; py[1] = planePos; pz[1] = mz;
                px[2] = Mx; py[2] = planePos; pz[2] = Mz;
                px[3] = mx; py[3] = planePos; pz[3] = Mz;
            }
            else
            {
                px[0] = mx; py[0] = my; pz[0] = planePos;
                px[1] = Mx; py[1] = my; pz[1] = planePos;
                px[2] = Mx; py[2] = My; pz[2] = planePos;
                px[3] = mx; py[3] = My; pz[3] = planePos;
            }

            Point[] screenPts = new Point[4];
            for (int i = 0; i < 4; i++)
            {
                float sx, sy;
                double d;
                Project(px[i] - _centre.X, py[i] - _centre.Y, pz[i] - _centre.Z,
                    yawR, pitchR, scale, cx, cyy, out sx, out sy, out d);
                screenPts[i] = new Point((int)sx, (int)sy);
            }

            using (Pen pen = new Pen(Color.FromArgb(200, 0, 0), 1f) { DashStyle = DashStyle.Dash })
            {
                g.DrawPolygon(pen, screenPts);
            }
        }

        // Гизмо осей в левом нижнем углу
        private void DrawGizmo(Graphics g, int w, int h)
        {
            int ox = 50, oy = h - 50;
            int len = 40;

            using (Pen pX = new Pen(Color.Red, 2f))
            using (Pen pY = new Pen(Color.LimeGreen, 2f))
            using (Pen pZ = new Pen(Color.Blue, 2f))
            using (Font fnt = new Font(FontFamily.GenericSansSerif, 8f))
            using (Brush b = new SolidBrush(Color.Black))
            {
                g.DrawLine(pX, ox, oy, ox + len, oy);
                g.DrawString("X", fnt, b, ox + len + 2, oy - 6);

                g.DrawLine(pY, ox, oy, ox - len / 2, oy - len / 2);
                g.DrawString("Y", fnt, b, ox - len / 2 - 12, oy - len / 2 - 6);

                g.DrawLine(pZ, ox, oy, ox, oy - len);
                g.DrawString("Z", fnt, b, ox - 4, oy - len - 12);
            }
        }
    }
}
