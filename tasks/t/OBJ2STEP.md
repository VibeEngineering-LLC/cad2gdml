Write ONE Python 3 file `obj2step.py`. Output ONLY the code in one ```python block. No explanations, no thinking text in the answer.

PURPOSE: convert a Wavefront OBJ (units: mm, objects named by `o NAME` or `g NAME` lines) into a STEP file where every OBJ object is ONE named shape (a sewn faceted shell/compound of planar triangle faces), using OpenCASCADE via the `OCP` module (cadquery-ocp). This is a TEST fixture generator, so geometry must be exactly the OBJ triangles.

CLI: `python obj2step.py <in.obj> <out.step> [--pylib DIR]`.
- `--pylib DIR`: if given, `sys.path.insert(0, DIR)` before importing OCP (default: no change).
- sys.stdout.reconfigure(encoding='utf-8'). Read the OBJ with encoding='utf-8', errors='replace'.
- Exit codes: 2 wrong args or unreadable file; 3 no geometry; 0 success; print one line `OK objects=<N> triangles=<T>`.

OBJ PARSING: `v x y z` lines (global vertex list, 1-based; negative indices are relative to the current end). `o NAME` or `g NAME` start a new object (use the last of consecutive o/g lines; name = the rest of the line stripped; empty -> `part_<n>`). `f` lines: tokens like `a`, `a/b`, `a//c`, `a/b/c`, take the part before the first slash; faces with more than 3 vertices are fan-triangulated (v0,vi,vi+1). Ignore everything else (vn, vt, usemtl, mtllib, s, comments). Faces before any `o`/`g` go to an object named `part_1`. Skip objects with no faces. Make object names unique by appending `_2`, `_3`, ... on duplicates.

OCP BUILD (follow this API exactly; imports from OCP.*):
- For each object: for every triangle, skip it when its area < 1e-9 mm2; build `BRepBuilderAPI_MakePolygon` with 3 `gp_Pnt` points and `Close()`, then `BRepBuilderAPI_MakeFace(polygon.Wire(), True)`; if `IsDone()` false, skip. Collect faces into a `BRepBuilderAPI_Sewing(1e-6)`; `Add(face.Face())` for each; then `Perform()` and `shape = sewing.SewedShape()`.
- STEP with names: `app = XCAFApp_Application.GetApplication_s()`, `doc = TDocStd_Document(TCollection_ExtendedString("MDTV-XCAF"))`, `app.InitDocument(doc)`, `st = XCAFDoc_DocumentTool.ShapeTool_s(doc.Main())`; for each object `lab = st.AddShape(shape, False)` and `TDataStd_Name.Set_s(lab, TCollection_ExtendedString(name))`.
- Write: `Interface_Static.SetCVal_s("write.step.unit", "MM")`; `w = STEPCAFControl_Writer()`; `w.SetNameMode(True)`; `w.Transfer(doc, STEPControl_AsIs)`; `status = w.Write(out_path)`; exit 3 if status != IFSelect_RetDone.
- Imports to use: `from OCP.gp import gp_Pnt`; `from OCP.BRepBuilderAPI import BRepBuilderAPI_MakePolygon, BRepBuilderAPI_MakeFace, BRepBuilderAPI_Sewing`; `from OCP.STEPCAFControl import STEPCAFControl_Writer`; `from OCP.STEPControl import STEPControl_AsIs`; `from OCP.Interface import Interface_Static`; `from OCP.IFSelect import IFSelect_RetDone`; `from OCP.TDocStd import TDocStd_Document`; `from OCP.TCollection import TCollection_ExtendedString`; `from OCP.XCAFApp import XCAFApp_Application`; `from OCP.XCAFDoc import XCAFDoc_DocumentTool`; `from OCP.TDataStd import TDataStd_Name`.
Keep it compact (about 100 lines), plain functions, no classes.
