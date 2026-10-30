import subprocess, sys, re, pathlib
G = r"D:/OneDrive/Рабочий стол/Тестовые модели/cad2gdml_out/atom_nano5_labr_v8.gdml"
W = pathlib.Path(__file__).parent.parent / "work"
def run(gdml, *extra):
    r = subprocess.run([sys.executable, str(pathlib.Path(__file__).parent / "gdml2in.py"), str(gdml), str(W / "t.in"), *extra], capture_output=True, text=True, encoding="utf-8")
    return r.returncode, (W / "t.in").read_text(encoding="utf-8") if r.returncode == 0 else r.stderr
def val(txt, k): return float(re.search(k + r" = (\S+)", txt).group(1))
s = pathlib.Path(G).read_text(encoding="utf-8")
def mut(name, a, b):
    assert a in s, name
    (W / "m.gdml").write_text(s.replace(a, b, 1), encoding="utf-8"); return run(W / "m.gdml")
rc, t = run(G); ok = [("база", rc == 0 and val(t, "DS_CrystalBoxX") == 1.4 and val(t, "DS_CrystalBoxZ") == 2.54 and "front=-z" in t)]
rc, t = mut("ширина", 'name="Crystal_LaBr3_s" x="14"', 'name="Crystal_LaBr3_s" x="20"'); ok.append(("кристалл 20 мм -> BoxX=2", rc == 0 and val(t, "DS_CrystalBoxX") == 2.0))
A = '<volume name="Module_Al_housing">' + chr(10) + '      <materialref ref="G4_'
rc, t = mut("материал", A + 'Al"', A + 'XXX"'); ok.append(("неизвестный G4_ -> код 3", rc == 3))
rc, t = mut("sipm", 'z="-12.7"/>', 'z="40"/>'); ok.append(("кристалл сдвинут, фронт меняется (+z)", rc == 0 and "front=+z" in t))
for n, v in ok: print("OK " if v else "FAIL", n)
sys.exit(0 if all(v for _, v in ok) else 1)
