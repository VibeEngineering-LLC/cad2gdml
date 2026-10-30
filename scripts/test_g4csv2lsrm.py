import subprocess, sys, pathlib
S = str(pathlib.Path(__file__).parent / "g4csv2lsrm.py")
W = pathlib.Path(__file__).parent.parent / "work"
def run(csv):
    (W / "tc.csv").write_text(csv, encoding="utf-8")
    r = subprocess.run([sys.executable, S, str(W / "tc.csv"), str(W / "tc.txt")], capture_output=True, text=True, encoding="utf-8")
    return r.returncode, (W / "tc.txt").read_text(encoding="utf-8").splitlines() if r.returncode == 0 else []
H = "# c\nE_keV,eps_fep,sig_fep\n"
rc, L = run(H + "100,0.01,0.0005\n200,0.02,0.002\n")
ok = [("база: unc=5 и 10 %", rc == 0 and L[1].split("\t")[2] == "5" and L[2].split("\t")[2] == "10" and L[0].startswith("Energy"))]
rc, L = run(H + "100,0.01,0.0005\n150,0.01,0.02\n200,0.02,0.002\n")
ok.append(("unc>100 % отброшена", rc == 0 and len(L) == 3))
rc, L = run(H + "100,0,0\n200,0.02,0.002\n300,0.03,0.003\n")
ok.append(("eps=0 отброшена", rc == 0 and len(L) == 3))
rc, L = run(H + "100,0.01,0.0005\n")
ok.append(("1 точка -> код 3", rc == 3))
rc, L = run(H + "100,abc,0.1\n200,0.02,0.002\n")
ok.append(("нечисло -> код 3", rc == 3))
for n, v in ok: print("OK " if v else "FAIL", n)
sys.exit(0 if all(v for _, v in ok) else 1)
