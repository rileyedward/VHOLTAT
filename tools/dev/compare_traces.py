"""Compare VHOLTAT movement recordings with VHOLUME replays (dev tool, no third-party libraries).

    python tools/dev/compare_traces.py --vholtat <recordings dir or csv>... --vholume <ghost dir>... --out report.html

VHOLTAT side: CSVs written by the in-game recorder (F11). VHOLUME side: replay files (*.json.gz) read from wherever
they live on this machine; nothing from them is stored in the repo. Both are reduced to the same states (ground,
crouch, slide, air, wallrun, climb, mantle) and the report overlays median speed curves aligned on each event.
"""
import argparse, csv, glob, gzip, html, io, json, math, os, statistics, zlib

DT = 0.05  # resample everything to the replays' 20 Hz grid
WINDOW = (-0.3, 1.5)

GHOST_MODES = {1: 'wallrun', 2: 'wallrun', 3: 'climb', 4: 'ledge', 5: 'mantle', 6: 'slide', 7: 'ground', 8: 'crouch'}
VHOLTAT_STATES = {'Ground': 'ground', 'Crouch': 'crouch', 'Slide': 'slide', 'Air': 'air', 'WallRunLeft': 'wallrun',
                  'WallRunRight': 'wallrun', 'WallClimb': 'climb', 'Mantle': 'mantle', 'Vanilla': 'vanilla'}


def load_ghost(path):
    raw = open(path, 'rb').read()
    g = json.loads(zlib.decompressobj(31).decompress(raw[4:]))
    loc = vel = None
    mode, mm = 0, 1
    out = []
    for f in g['frames']:
        if 'loc' in f:
            loc = [f['loc'][k] for k in 'xyz']
            vel = [f['vel'][k] for k in 'xyz']
        else:
            loc = [a + b for a, b in zip(loc, f.get('dloc', [0, 0, 0]))]
            vel = [a + b for a, b in zip(vel, f.get('dvel', [0, 0, 0]))]
        anim = f.get('anim', {})
        mode = anim.get('mode', mode)
        mm = anim.get('movementMode', mm)
        state = GHOST_MODES.get(mode) or ('ground' if mm == 1 else 'air')
        out.append((f['t'], math.hypot(vel[0], vel[1]) / 100.0, vel[2] / 100.0, state))
    return out


def load_recording(path):
    rows = []
    with open(path, encoding='utf-8') as fh:
        lines = [l for l in fh if not l.startswith('#')]
    for r in csv.DictReader(io.StringIO(''.join(lines))):
        rows.append((float(r['t']), float(r['hspeed']), float(r['vy']), VHOLTAT_STATES.get(r['state'], r['state'])))
    # resample to the 20 Hz grid (nearest earlier frame)
    out, i, t = [], 0, 0.0
    end = rows[-1][0] if rows else 0
    while t <= end:
        while i + 1 < len(rows) and rows[i + 1][0] <= t:
            i += 1
        out.append((t,) + rows[i][1:])
        t += DT
    return out


def files(paths, pattern):
    for p in paths:
        if os.path.isdir(p):
            yield from glob.glob(os.path.join(p, '**', pattern), recursive=True)
        elif os.path.exists(p):
            yield p


def events(trace):
    """Yields (kind, index) for every event in a 20 Hz trace."""
    for i in range(1, len(trace)):
        prev, cur = trace[i - 1][3], trace[i][3]
        if cur != prev:
            if cur in ('slide', 'wallrun', 'climb', 'mantle'):
                yield cur + ' start', i
            if prev in ('ground', 'slide', 'crouch') and cur == 'air' and trace[i][2] > 2.0:
                yield 'jump', i
            if prev in ('air', 'wallrun') and cur in ('ground', 'slide', 'crouch'):
                yield 'landing', i
        if trace[i - 1][1] < 0.5 and trace[i][1] >= 0.5 and cur == 'ground':
            yield 'run start', i


def windows(traces):
    lo, hi = int(round(WINDOW[0] / DT)), int(round(WINDOW[1] / DT))
    out = {}
    for tr in traces:
        for kind, i in events(tr):
            if i + lo < 0 or i + hi >= len(tr):
                continue
            seg = tr[i + lo:i + hi + 1]
            out.setdefault(kind, []).append(([s[1] for s in seg], [s[2] for s in seg]))
    return out


def median_curve(curves):
    return [statistics.median(c[k] for c in curves) for k in range(len(curves[0]))] if curves else []


def svg(title, series, ylabel):
    w, h, pad = 560, 240, 40
    allv = [v for _, ys, _ in series for v in ys] or [0, 1]
    lo, hi = min(0.0, min(allv)), max(allv) * 1.1 or 1
    n = max(len(ys) for _, ys, _ in series) if series else 1
    def x(k): return pad + (w - 2 * pad) * k / max(1, n - 1)
    def y(v): return h - pad - (h - 2 * pad) * (v - lo) / (hi - lo)
    parts = [f'<svg width="{w}" height="{h}" xmlns="http://www.w3.org/2000/svg" style="background:#11151c">',
             f'<text x="{pad}" y="18" fill="#ddd" font-size="13">{html.escape(title)}</text>',
             f'<text x="4" y="{h/2}" fill="#888" font-size="10" transform="rotate(-90 10 {h/2})">{ylabel}</text>']
    zero = int(-WINDOW[0] / DT)
    parts.append(f'<line x1="{x(zero)}" y1="{pad}" x2="{x(zero)}" y2="{h-pad}" stroke="#555" stroke-dasharray="3"/>')
    for tick in (0, hi / 2, hi):
        parts.append(f'<text x="2" y="{y(tick)+4}" fill="#888" font-size="10">{tick:.1f}</text>')
    for name, ys, color in series:
        pts = ' '.join(f'{x(k):.1f},{y(v):.1f}' for k, v in enumerate(ys))
        parts.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="2"/>')
    for k, (name, _, color) in enumerate(series):
        parts.append(f'<text x="{w-pad-150}" y="{36+14*k}" fill="{color}" font-size="12">{html.escape(name)}</text>')
    parts.append(f'<text x="{pad}" y="{h-8}" fill="#888" font-size="10">{WINDOW[0]} s ... {WINDOW[1]} s around the event</text>')
    parts.append('</svg>')
    return ''.join(parts)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--vholtat', nargs='*', default=[])
    ap.add_argument('--vholume', nargs='*', default=[])
    ap.add_argument('--out', default='movement_compare.html')
    a = ap.parse_args()
    vt = [load_recording(f) for f in files(a.vholtat, '*.csv')]
    vh = [load_ghost(f) for f in files(a.vholume, '*.gz')]
    wv, wh = windows(vt), windows(vh)
    kinds = sorted(set(wv) | set(wh))
    body = [f'<h2>VHOLTAT {len(vt)} recordings vs VHOLUME {len(vh)} replays</h2>']
    for kind in kinds:
        cv, ch = wv.get(kind, []), wh.get(kind, [])
        sp = [(f'VHOLUME (n={len(ch)})', median_curve([c[0] for c in ch]), '#6ec6ff'),
              (f'VHOLTAT (n={len(cv)})', median_curve([c[0] for c in cv]), '#ffb84d')]
        vy = [(f'VHOLUME (n={len(ch)})', median_curve([c[1] for c in ch]), '#6ec6ff'),
              (f'VHOLTAT (n={len(cv)})', median_curve([c[1] for c in cv]), '#ffb84d')]
        sp = [s for s in sp if s[1]]
        vy = [s for s in vy if s[1]]
        body.append(f'<h3>{html.escape(kind)}</h3><div>{svg(kind + " - horizontal speed (m/s)", sp, "m/s")} '
                    f'{svg(kind + " - vertical speed (m/s)", vy, "m/s")}</div>')
    # Text summary: medians at fixed offsets around each event.
    marks = [-0.2, 0.0, 0.1, 0.2, 0.4, 0.7, 1.0, 1.5]
    idx = [int(round((m - WINDOW[0]) / DT)) for m in marks]
    print('event'.ljust(16) + 'source'.ljust(9) + ' n  ' + ''.join(f'{m:>+7.1f}s' for m in marks))
    for kind in kinds:
        for name, curves in (('VHOLUME', wh.get(kind, [])), ('VHOLTAT', wv.get(kind, []))):
            if not curves:
                continue
            hs, vs = median_curve([c[0] for c in curves]), median_curve([c[1] for c in curves])
            print(kind.ljust(16) + name.ljust(9) + f'{len(curves):3d} h ' + ''.join(f'{hs[i]:8.2f}' for i in idx))
            print(''.ljust(16) + ''.ljust(9) + '    vy' + ''.join(f'{vs[i]:8.2f}' for i in idx))
    open(a.out, 'w', encoding='utf-8').write(
        '<html><body style="background:#0b0e13;color:#ddd;font-family:sans-serif">' + ''.join(body) + '</body></html>')
    print(f'wrote {a.out}: {len(kinds)} event types, {len(vt)} VHOLTAT recordings, {len(vh)} VHOLUME replays')


if __name__ == '__main__':
    main()
