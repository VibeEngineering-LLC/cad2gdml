import sys

def convert(text):
    pts = []
    for line in text.splitlines():
        s = line.strip()
        if not s or s.startswith('#') or s.lower().startswith('e_kev'):
            continue
        p = s.split(',')
        if len(p) < 3:
            raise ValueError("bad number")
        try:
            e, eps, sig = float(p[0]), float(p[1]), float(p[2])
        except ValueError:
            raise ValueError("bad number")
        if eps <= 0:
            continue
        unc = sig / eps * 100
        if unc > 100:
            continue
        pts.append((e, eps, unc))
    if len(pts) < 2:
        raise ValueError("fewer than 2 usable points")
    out = "Energy, keV\tEfficiency\tUncertainty, %\n"
    for e, eps, unc in pts:
        out += f"{e:.6g}\t{eps:.6e}\t{unc:.4g}\n"
    return out, len(pts)

def main():
    sys.stdout.reconfigure(encoding='utf-8')
    if len(sys.argv) != 3:
        sys.exit(2)
    try:
        with open(sys.argv[1], encoding='utf-8') as f:
            text = f.read()
    except OSError as ex:
        print(f"ERR {ex}")
        sys.exit(3)
    try:
        out, n = convert(text)
    except ValueError as ex:
        print(f"ERR {ex}")
        sys.exit(3)
    with open(sys.argv[2], 'w', encoding='utf-8', newline='\n') as f:
        f.write(out)
    print(f"OK points={n}")
    sys.exit(0)

if __name__ == '__main__':
    main()
