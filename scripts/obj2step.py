import sys
import math
import argparse


def parse_obj(lines):
    verts = []
    objects = []
    state = {'current': None, 'pending': None, 'auto': 1}

    def start(name):
        if not name:
            name = "part_%d" % state['auto']
            state['auto'] += 1
        state['current'] = {'name': name, 'faces': []}
        state['pending'] = None

    def flush():
        c = state['current']
        if c is not None and c['faces']:
            objects.append(c)
        state['current'] = None

    def vid(i):
        return verts[i - 1] if i > 0 else verts[i]

    for line in lines:
        s = line.strip()
        if not s:
            continue
        parts = s.split(None, 1)
        tag = parts[0]
        if tag == 'v':
            nums = s.split()
            if len(nums) >= 4:
                try:
                    verts.append((float(nums[1]), float(nums[2]), float(nums[3])))
                except ValueError:
                    pass
        elif tag in ('o', 'g'):
            name = parts[1].strip() if len(parts) > 1 else ''
            cur = state['current']
            if cur is not None and cur['faces']:
                flush()
            state['pending'] = name
        elif tag == 'f':
            idxs = []
            for t in s.split()[1:]:
                try:
                    idxs.append(int(t.split('/')[0]))
                except ValueError:
                    idxs = []
                    break
            if len(idxs) < 3:
                continue
            cur = state['current']
            if cur is None:
                start(state['pending'])
                cur = state['current']
            for i in range(1, len(idxs) - 1):
                try:
                    a = vid(idxs[0])
                    b = vid(idxs[i])
                    c = vid(idxs[i + 1])
                except IndexError:
                    continue
                cur['faces'].append((a, b, c))
    flush()

    seen = {}
    for obj in objects:
        base = obj['name']
        if base not in seen:
            seen[base] = 1
        else:
            seen[base] += 1
            obj['name'] = "%s_%d" % (base, seen[base])
    return objects


def tri_area(a, b, c):
    ab = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    ac = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    cr = (ab[1] * ac[2] - ab[2] * ac[1],
          ab[2] * ac[0] - ab[0] * ac[2],
          ab[0] * ac[1] - ab[1] * ac[0])
    return 0.5 * math.sqrt(cr[0] * cr[0] + cr[1] * cr[1] + cr[2] * cr[2])


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('obj_file')
    ap.add_argument('step_file')
    ap.add_argument('--pylib', default=None)
    args = ap.parse_args()

    if args.pylib:
        sys.path.insert(0, args.pylib)

    try:
        with open(args.obj_file, encoding='utf-8', errors='replace') as f:
            lines = f.readlines()
    except OSError:
        sys.exit(2)

    objects = parse_obj(lines)
    if not objects:
        sys.exit(3)

    from OCP.gp import gp_Pnt
    from OCP.BRepBuilderAPI import (BRepBuilderAPI_MakePolygon,
                                    BRepBuilderAPI_MakeFace,
                                    BRepBuilderAPI_Sewing)
    from OCP.STEPCAFControl import STEPCAFControl_Writer
    from OCP.STEPControl import STEPControl_AsIs
    from OCP.Interface import Interface_Static
    from OCP.IFSelect import IFSelect_RetDone
    from OCP.TDocStd import TDocStd_Document
    from OCP.TCollection import TCollection_ExtendedString
    from OCP.XCAFApp import XCAFApp_Application
    from OCP.XCAFDoc import XCAFDoc_DocumentTool
    from OCP.TDataStd import TDataStd_Name

    shapes = []
    total = 0
    for obj in objects:
        sewing = BRepBuilderAPI_Sewing(1e-6)
        for a, b, c in obj['faces']:
            if tri_area(a, b, c) < 1e-9:
                continue
            poly = BRepBuilderAPI_MakePolygon(gp_Pnt(*a), gp_Pnt(*b), gp_Pnt(*c))
            poly.Close()
            face = BRepBuilderAPI_MakeFace(poly.Wire(), True)
            if not face.IsDone():
                continue
            sewing.Add(face.Face())
            total += 1
        sewing.Perform()
        shapes.append((obj['name'], sewing.SewedShape()))

    if total == 0:
        sys.exit(3)

    app = XCAFApp_Application.GetApplication_s()
    doc = TDocStd_Document(TCollection_ExtendedString("MDTV-XCAF"))
    app.InitDocument(doc)
    st = XCAFDoc_DocumentTool.ShapeTool_s(doc.Main())
    for name, shape in shapes:
        lab = st.AddShape(shape, False)
        TDataStd_Name.Set_s(lab, TCollection_ExtendedString(name))

    Interface_Static.SetCVal_s("write.step.unit", "MM")
    w = STEPCAFControl_Writer()
    w.SetNameMode(True)
    w.Transfer(doc, STEPControl_AsIs)
    status = w.Write(args.step_file)
    if status != IFSelect_RetDone:
        sys.exit(3)

    print("OK objects=%d triangles=%d" % (len(shapes), total))


if __name__ == '__main__':
    main()
