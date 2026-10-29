import sys, collections
sys.stdout.reconfigure(encoding='utf-8')
V = []; cnt = collections.Counter(); tot = collections.Counter(); cur = None
for l in open(sys.argv[1], encoding='utf-8'):
    t = l.split()
    if not t: continue
    if t[0] == 'v': V.append(tuple(map(float, t[1:4])))
    elif t[0] == 'o': cur = t[1]
    elif t[0] == 'f' and cur:
        i = [int(x.split('/')[0]) - 1 for x in t[1:]]
        for k in range(1, len(i) - 1):
            A, B, C = V[i[0]], V[i[k]], V[i[k + 1]]
            u = [B[d] - A[d] for d in range(3)]; w = [C[d] - A[d] for d in range(3)]
            cr = (u[1]*w[2]-u[2]*w[1], u[2]*w[0]-u[0]*w[2], u[0]*w[1]-u[1]*w[0])
            tot[cur] += 1
            if (cr[0]**2 + cr[1]**2 + cr[2]**2) ** 0.5 / 2 < 1e-6: cnt[cur] += 1
for o in tot: print(o, 'tris', tot[o], 'degenerate(area<1e-6)', cnt[o]) if cnt[o] else None
print('total degenerate', sum(cnt.values()), 'of', sum(tot.values()))
