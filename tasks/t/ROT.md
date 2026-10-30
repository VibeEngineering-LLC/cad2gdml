Write C# (C# 7.3, no namespace, no class, no usings) as a fragment to be pasted into the class `MeshView : Control` (WinForms). Output ONLY the code in one ```csharp block. Members are indented by 8 spaces. The fragment is the contiguous block of FOUR members, in this order, nothing else:

1. A field with a Russian comment: `private double[] _rot = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };` — a 3x3 row-major accumulated view-rotation matrix, applied in VIEW space AFTER the existing yaw/pitch turntable rotation. Comment: rotation is accumulated relative to the SCREEN (#CG-35): horizontal mouse motion rotates about the screen's vertical axis, vertical motion about the screen's horizontal axis.

2. `private void RotateView(double dxDeg, double dyDeg)` — converts both angles to radians a (horizontal) and b (vertical); view space is x right, y depth, z up. Build `double[] rz = { ca, -sa, 0, sa, ca, 0, 0, 0, 1 };` (rotation about the screen's vertical axis, z) and `double[] rx = { 1, 0, 0, 0, cb, -sb, 0, sb, cb };` (rotation about the screen's horizontal axis, x), then `_rot = MulMat(rx, MulMat(rz, _rot));`.

3. `private static double[] MulMat(double[] p, double[] q)` — plain 3x3 row-major product p*q with three nested loops (result[i*3+j] = sum_k p[i*3+k]*q[k*3+j]).

4. The existing method, now an INSTANCE method (remove `static`), with the same signature and the same first part, keep the comment line `// Вращение: yaw вокруг Z, pitch вокруг X (стандартный turntable, Z — вверх)` above it:
```
        private void RotatePoint(double x, double y, double z,
            double yawR, double pitchR, out double rx, out double ry, out double rz)
        {
            double cy = Math.Cos(yawR), sy = Math.Sin(yawR);
            double cp = Math.Cos(pitchR), sp = Math.Sin(pitchR);

            // Yaw вокруг Z
            double x1 = x * cy - y * sy;
            double y1 = x * sy + y * cy;
            double z1 = z;

            // Pitch вокруг X
            double vx = x1;
            double vy = y1 * cp - z1 * sp;
            double vz = y1 * sp + z1 * cp;

            // Поворот вида относительно экрана (#CG-35)
            rx = _rot[0] * vx + _rot[1] * vy + _rot[2] * vz;
            ry = _rot[3] * vx + _rot[4] * vy + _rot[5] * vz;
            rz = _rot[6] * vx + _rot[7] * vy + _rot[8] * vz;
        }
```
Put the comment line `// Вращение: ...` first, then members 1-3 BEFORE the RotatePoint method is wrong: order is: member 1 (with its comment), member 2, member 3, then the `// Вращение:` comment line and member 4. Nothing before member 1's comment, a blank line between members, the fragment ends right after the closing brace of RotatePoint plus one blank line.
