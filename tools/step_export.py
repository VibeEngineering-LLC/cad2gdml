import sys, os, json, argparse
sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "pylib"))
from OCP.STEPCAFControl import STEPCAFControl_Reader
from OCP.Interface import Interface_Static
from OCP.IFSelect import IFSelect_RetDone
from OCP.TDocStd import TDocStd_Document
from OCP.TCollection import TCollection_ExtendedString
from OCP.XCAFApp import XCAFApp_Application
from OCP.XCAFDoc import XCAFDoc_DocumentTool, XCAFDoc_ShapeTool
from OCP.TDF import TDF_Label
from OCP.collections import Sequence_TDF_Label as TDF_LabelSequence
from OCP.TDataStd import TDataStd_Name
from OCP.TopLoc import TopLoc_Location
from OCP.BRepMesh import BRepMesh_IncrementalMesh
from OCP.StlAPI import StlAPI_Writer

BAD = set('\\/:*?"<>|')

def clean(s, i):
    s = s.strip()
    s = ''.join('_' if c in BAD else c for c in s)
    return s or f"part_{i}"

def gname(l):
    a = TDataStd_Name()
    return a.Get().ToExtString() if l.FindAttribute(TDataStd_Name.GetID_s(), a) else ""

def walk(l, pl, d, out):
    if d > 50:
        return
    if XCAFDoc_ShapeTool.IsAssembly_s(l):
        cs = TDF_LabelSequence()
        XCAFDoc_ShapeTool.GetComponents_s(l, cs)
        for i in range(1, cs.Length() + 1):
            c = cs.Value(i)
            r = TDF_Label()
            XCAFDoc_ShapeTool.GetReferredShape_s(c, r)
            walk(r, pl * XCAFDoc_ShapeTool.GetLocation_s(c), d + 1, out)
    else:
        sh = XCAFDoc_ShapeTool.GetShape_s(l)
        sh = sh.Moved(pl)
        out.append((gname(l) or f"part_{len(out) + 1}", sh))

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("outdir")
    ap.add_argument("--deflection", type=float, default=0.05)
    a = ap.parse_args()
    os.makedirs(a.outdir, exist_ok=True)
    Interface_Static.SetCVal_s("xstep.cascade.unit", "MM")
    Interface_Static.SetIVal_s("read.stepcaf.subshapes.name", 1)
    app = XCAFApp_Application.GetApplication_s()
    doc = TDocStd_Document(TCollection_ExtendedString("MDTV-XCAF"))
    app.InitDocument(doc)
    rd = STEPCAFControl_Reader()
    rd.SetNameMode(True)
    rd.SetColorMode(True)
    if rd.ReadFile(a.input) != IFSelect_RetDone:
        print(json.dumps({"error": "E_STEP_READ", "message": "ReadFile failed"}))
        sys.exit(2)
    if not rd.Transfer(doc):
        print(json.dumps({"error": "E_STEP_READ", "message": "Transfer failed"}))
        sys.exit(2)
    st = XCAFDoc_DocumentTool.ShapeTool_s(doc.Main())
    fs = TDF_LabelSequence()
    st.GetFreeShapes(fs)
    parts = []
    for i in range(1, fs.Length() + 1):
        walk(fs.Value(i), TopLoc_Location(), 0, parts)
    if not parts:
        print(json.dumps({"error": "E_STEP_EMPTY", "message": "no solids"}))
        sys.exit(3)
    mp, tt, used = [], 0, set()
    for idx, (nm, sh) in enumerate(parts, 1):
        nm = clean(nm, idx)
        b, n = nm, 1
        while nm in used:
            n += 1
            nm = f"{b}_{n}"
        used.add(nm)
        BRepMesh_IncrementalMesh(sh, a.deflection, False, 0.3, True)
        fn = f"{idx:03d}_{nm}.stl"
        fp = os.path.join(a.outdir, fn)
        w = StlAPI_Writer()
        w.ASCIIMode = False
        w.Write(sh, fp)
        tr = (os.path.getsize(fp) - 84) // 50
        if tr <= 0:
            os.remove(fp)
            continue
        mp.append({"name": nm, "file": fn, "triangles": tr})
        tt += tr
    if not mp:
        print(json.dumps({"error": "E_STEP_EMPTY", "message": "no solids"}))
        sys.exit(3)
    with open(os.path.join(a.outdir, "_manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"source": a.input, "unit": "mm", "parts": mp}, f, ensure_ascii=False, indent=1)
    print(json.dumps({"ok": True, "parts": len(mp), "triangles": tt}))
    sys.exit(0)

if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        print(json.dumps({"error": "E_STEP_INTERNAL", "message": repr(e)}))
        sys.exit(4)
