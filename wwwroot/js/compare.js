// US03: сравнение на размерите в реален мащаб (по идея на carsized.com).
// Рисува силуетите на двете коли като SVG по реалните им дължина, височина, ширина, междуосие и клиренс.
// Силуетите са схематични (по вид купе), но размерите и мащабът са истински.
(function () {
    'use strict';

    const host = document.getElementById('size-canvas');
    if (!host || !window.compareCars) return;

    const cars = window.compareCars.filter(Boolean);
    const COLORS = ['#1d5fe0', '#f08c00'];
    let view = 'side';      // side | front
    let mode = 'apart';     // apart | overlay

    // Стойности по подразбиране, ако липсва някой размер
    const DEFAULT_H = { Sedan: 1450, Hatchback: 1480, Wagon: 1490, Suv: 1690, Coupe: 1350, Convertible: 1380, Van: 1850, Pickup: 1820 };
    const DEFAULT_GC = { Sedan: 140, Hatchback: 145, Wagon: 145, Suv: 200, Coupe: 125, Convertible: 125, Van: 160, Pickup: 220 };

    // Профил на горния контур (от предната броня към задната): [част от дължината, част от височината]
    const PROFILES = {
        Sedan:       [[0, .36], [.015, .50], [.24, .56], [.40, .97], [.64, 1], [.81, .64], [.975, .60], [1, .47], [1, .32]],
        Hatchback:   [[0, .36], [.015, .51], [.23, .58], [.40, .98], [.79, .97], [.95, .78], [1, .54], [1, .32]],
        Wagon:       [[0, .36], [.015, .51], [.21, .57], [.37, .97], [.92, .96], [.985, .82], [1, .52], [1, .32]],
        Suv:         [[0, .38], [.015, .58], [.20, .65], [.34, .98], [.88, .985], [.975, .86], [1, .56], [1, .34]],
        Coupe:       [[0, .35], [.015, .47], [.29, .55], [.46, .97], [.62, .98], [.86, .66], [.98, .62], [1, .48], [1, .33]],
        Convertible: [[0, .35], [.015, .48], [.30, .58], [.42, .90], [.445, .90], [.50, .66], [.86, .66], [.98, .63], [1, .49], [1, .33]],
        Van:         [[0, .32], [.015, .48], [.11, .58], [.24, .985], [.975, .99], [1, .92], [1, .32]],
        Pickup:      [[0, .36], [.015, .55], [.20, .62], [.32, .98], [.58, .98], [.615, .66], [1, .66], [1, .35]]
    };
    // Стъкла (странични прозорци)
    const WINDOWS = {
        Sedan:       [[.27, .60], [.41, .91], [.63, .93], [.77, .64]],
        Hatchback:   [[.26, .62], [.41, .92], [.78, .91], [.91, .70]],
        Wagon:       [[.24, .61], [.38, .91], [.91, .90], [.96, .70]],
        Suv:         [[.23, .68], [.35, .92], [.87, .93], [.94, .72]],
        Coupe:       [[.32, .59], [.47, .91], [.61, .92], [.79, .67]],
        Convertible: [[.33, .62], [.42, .84], [.44, .84], [.46, .66]],
        Van:         [[.14, .62], [.25, .93], [.95, .93], [.97, .64]],
        Pickup:      [[.23, .66], [.33, .92], [.57, .92], [.59, .67]]
    };

    const svgNS = 'http://www.w3.org/2000/svg';
    const el = function (name, attrs, parent) {
        const n = document.createElementNS(svgNS, name);
        Object.keys(attrs).forEach(function (k) { n.setAttribute(k, attrs[k]); });
        if (parent) parent.appendChild(n);
        return n;
    };
    // Заобляне на ъглите (алгоритъм на Чайкин) — първата и последната точка остават
    const smooth = function (pts, iterations) {
        let p = pts;
        for (let k = 0; k < iterations; k++) {
            const out = [p[0]];
            for (let i = 0; i < p.length - 1; i++) {
                const a = p[i], b = p[i + 1];
                out.push([a[0] * .75 + b[0] * .25, a[1] * .75 + b[1] * .25]);
                out.push([a[0] * .25 + b[0] * .75, a[1] * .25 + b[1] * .75]);
            }
            out.push(p[p.length - 1]);
            p = out;
        }
        return p;
    };
    const meters = function (mm) { return (mm / 1000).toFixed(2).replace('.', ',') + ' м'; };

    const normalize = function (c) {
        const body = PROFILES[c.body] ? c.body : 'Hatchback';
        const L = c.length || 4400;
        const H = c.height || DEFAULT_H[body];
        const W = c.width || 1800;
        let WB = c.wheelbase || Math.round(L * 0.6);
        if (WB > L * 0.8) WB = Math.round(L * 0.6);
        const GC = c.clearance || DEFAULT_GC[body];
        return { label: c.label, short: c.short, body: body, L: L, H: H, W: W, WB: WB, GC: GC };
    };

    // ---------- Страничен изглед ----------
    function drawSide(g, c, x0, color, overlay) {
        const r = Math.min(Math.max(c.H * 0.215, 290), 420);            // радиус на гумата
        const fo = (c.L - c.WB) * 0.56;                                   // преден надвес
        const xf = x0 + fo, xr = x0 + fo + c.WB;                          // центрове на колелата
        const yb = Math.max(c.GC, r * 0.55);                              // долен ръб на купето
        const ra = r + 45;                                                // радиус на калника (център = центъра на колелото)
        const dx = Math.sqrt(Math.max(ra * ra - (r - yb) * (r - yb), 1)); // къде калникът пресича долния ръб
        const Y = function (f) { return c.H * f; };
        const prof = smooth(PROFILES[c.body], 2);

        // Контур: долна линия с калници + заоблен горен профил в обратен ред
        let d = 'M ' + x0 + ' ' + Y(prof[0][1]);
        d += ' L ' + x0 + ' ' + yb;
        d += ' L ' + (xf - dx) + ' ' + yb;
        d += ' A ' + ra + ' ' + ra + ' 0 1 0 ' + (xf + dx) + ' ' + yb;   // y-оста е обърната с transform
        d += ' L ' + (xr - dx) + ' ' + yb;
        d += ' A ' + ra + ' ' + ra + ' 0 1 0 ' + (xr + dx) + ' ' + yb;
        d += ' L ' + (x0 + c.L) + ' ' + yb;
        for (let i = prof.length - 1; i >= 0; i--) d += ' L ' + (x0 + c.L * prof[i][0]) + ' ' + Y(prof[i][1]);
        d += ' Z';

        el('path', {
            d: d, fill: color, 'fill-opacity': overlay ? 0.28 : 0.9, stroke: color, 'stroke-width': 18,
            'stroke-linejoin': 'round', 'stroke-dasharray': overlay && color === COLORS[1] ? '60 30' : 'none'
        }, g);

        if (!overlay) {
            const win = smooth(WINDOWS[c.body], 1).map(function (p) { return (x0 + c.L * p[0]) + ',' + Y(p[1]); }).join(' ');
            el('polygon', { points: win, fill: '#dbe8ff', 'fill-opacity': 0.85, stroke: 'none' }, g);
        }
        [xf, xr].forEach(function (x) {
            el('circle', { cx: x, cy: r, r: r, fill: overlay ? 'none' : '#1b1f2a', stroke: overlay ? color : '#1b1f2a', 'stroke-width': 18 }, g);
            if (!overlay) el('circle', { cx: x, cy: r, r: r * 0.58, fill: '#b9c3d6' }, g);
        });
    }

    // ---------- Изглед отпред ----------
    function drawFront(g, c, x0, color, overlay) {
        const W = c.W, H = c.H;
        const yb = Math.max(c.GC, 110);
        const shoulder = H * (c.body === 'Van' ? 0.62 : c.body === 'Suv' || c.body === 'Pickup' ? 0.6 : 0.55);
        const roofW = W * (c.body === 'Van' ? 0.86 : c.body === 'Convertible' ? 0.7 : 0.74);
        const tireW = Math.min(Math.max(W * 0.12, 185), 285);
        const tireH = Math.max(yb + 120, H * 0.36);

        // гуми
        [x0 + W * 0.04, x0 + W * 0.96 - tireW].forEach(function (x) {
            el('rect', { x: x, y: 0, width: tireW, height: tireH, rx: 40, fill: overlay ? 'none' : '#1b1f2a', stroke: overlay ? color : '#1b1f2a', 'stroke-width': 16 }, g);
        });
        // купе
        const top = c.body === 'Convertible' ? H * 0.82 : H;
        let d = 'M ' + (x0 + W * 0.03) + ' ' + yb;
        d += ' L ' + (x0 + W * 0.97) + ' ' + yb;
        d += ' Q ' + (x0 + W) + ' ' + yb + ' ' + (x0 + W) + ' ' + (yb + 80);
        d += ' L ' + (x0 + W) + ' ' + (shoulder - 60);
        d += ' Q ' + (x0 + W) + ' ' + shoulder + ' ' + (x0 + W * 0.95) + ' ' + shoulder;
        d += ' L ' + (x0 + W / 2 + roofW / 2) + ' ' + (top - 40);
        d += ' Q ' + (x0 + W / 2 + roofW / 2 - 20) + ' ' + top + ' ' + (x0 + W / 2 + roofW / 2 - 80) + ' ' + top;
        d += ' L ' + (x0 + W / 2 - roofW / 2 + 80) + ' ' + top;
        d += ' Q ' + (x0 + W / 2 - roofW / 2 + 20) + ' ' + top + ' ' + (x0 + W / 2 - roofW / 2) + ' ' + (top - 40);
        d += ' L ' + (x0 + W * 0.05) + ' ' + shoulder;
        d += ' Q ' + x0 + ' ' + shoulder + ' ' + x0 + ' ' + (shoulder - 60);
        d += ' L ' + x0 + ' ' + (yb + 80);
        d += ' Q ' + x0 + ' ' + yb + ' ' + (x0 + W * 0.03) + ' ' + yb + ' Z';
        el('path', {
            d: d, fill: color, 'fill-opacity': overlay ? 0.28 : 0.9, stroke: color, 'stroke-width': 16,
            'stroke-dasharray': overlay && color === COLORS[1] ? '50 25' : 'none'
        }, g);
        if (!overlay) {
            // предно стъкло, фарове, решетка
            const wsB = shoulder + 30, wsT = top - 90;
            el('polygon', {
                points: [(x0 + W * 0.13) + ',' + wsB, (x0 + W * 0.87) + ',' + wsB, (x0 + W / 2 + roofW / 2 - 70) + ',' + wsT, (x0 + W / 2 - roofW / 2 + 70) + ',' + wsT].join(' '),
                fill: '#dbe8ff', 'fill-opacity': 0.85
            }, g);
            [x0 + W * 0.08, x0 + W * 0.74].forEach(function (x) {
                el('rect', { x: x, y: shoulder - 190, width: W * 0.18, height: 90, rx: 30, fill: '#fff7d6' }, g);
            });
            el('rect', { x: x0 + W * 0.32, y: yb + 120, width: W * 0.36, height: Math.max(shoulder - yb - 340, 80), rx: 30, fill: '#0f1a33', 'fill-opacity': 0.55 }, g);
        }
        // огледала
        [[x0 - 110, x0], [x0 + W, x0 + W + 110]].forEach(function (m) {
            el('rect', { x: m[0], y: shoulder + 40, width: 110, height: 90, rx: 25, fill: color, 'fill-opacity': overlay ? 0.28 : 0.9 }, g);
        });
    }

    // ---------- Мрежа, линии с размери ----------
    function render() {
        host.innerHTML = '';
        const list = cars.map(normalize);
        if (!list.length) return;

        const side = view === 'side';
        const gap = side ? 700 : 600;
        const widthOf = function (c) { return side ? c.L : c.W + 220; };
        const totalW = mode === 'overlay' || list.length === 1
            ? Math.max.apply(null, list.map(widthOf))
            : list.reduce(function (s, c) { return s + widthOf(c); }, 0) + gap * (list.length - 1);
        const maxH = Math.max.apply(null, list.map(function (c) { return c.H; }));
        const overlayMode = mode === 'overlay' && list.length > 1;
        const pad = { l: 520, r: 760, t: overlayMode ? 560 : 380, b: overlayMode ? 760 : 560 };
        const vbW = totalW + pad.l + pad.r;
        const vbH = maxH + pad.t + pad.b;
        const fs = Math.min(Math.max(vbW / 48, 55), 135);   // размер на текста спрямо мащаба на рисунката

        const svg = el('svg', { viewBox: '0 0 ' + vbW + ' ' + vbH, width: '100%', role: 'img', 'aria-label': 'Сравнение на размерите в мащаб', class: 'size-svg' }, host);
        const groundY = pad.t + maxH;
        const world = el('g', { transform: 'translate(' + pad.l + ',' + groundY + ') scale(1,-1)' }, svg);
        const labels = el('g', { transform: 'translate(' + pad.l + ',' + groundY + ')' }, svg);
        const text = function (x, y, s, attrs) {
            const t = el('text', Object.assign({ x: x, y: y, 'font-size': fs, fill: 'currentColor', 'text-anchor': 'middle' }, attrs || {}), labels);
            t.textContent = s;
            return t;
        };

        // хоризонтални линии (височина) на всеки 0,5 м
        for (let h = 0; h <= maxH + 250; h += 500) {
            el('line', { x1: -120, x2: totalW + 120, y1: -h, y2: -h, stroke: 'currentColor', 'stroke-opacity': h === 0 ? 0.6 : 0.12, 'stroke-width': h === 0 ? 14 : 8 }, labels);
            text(-200, -h + 40, (h / 1000).toFixed(1).replace('.', ',') + ' м', { 'font-size': fs * 0.85, 'text-anchor': 'end', 'fill-opacity': 0.6 });
        }

        let x = 0;
        list.forEach(function (c, i) {
            const w = widthOf(c);
            const x0 = mode === 'overlay' || list.length === 1 ? (side ? 0 : (totalW - w) / 2 + 110) : x + (side ? 0 : 110);
            const overlay = mode === 'overlay' && list.length > 1;
            const g = el('g', {}, world);
            if (side) drawSide(g, c, x0, COLORS[i], overlay); else drawFront(g, c, x0, COLORS[i], overlay);

            // линия за дължина/ширина под колата
            const span = side ? c.L : c.W;
            const sx = side ? x0 : x0;
            const ly = 170 + (overlay ? i * fs * 1.8 : 0);
            el('line', { x1: sx, x2: sx + span, y1: ly, y2: ly, stroke: COLORS[i], 'stroke-width': 14 }, labels);
            [sx, sx + span].forEach(function (px) { el('line', { x1: px, x2: px, y1: ly - 50, y2: ly + 50, stroke: COLORS[i], 'stroke-width': 14 }, labels); });
            text(sx + span / 2, ly + fs * 1.25, (side ? 'дължина ' : 'ширина ') + meters(span), { fill: COLORS[i], 'font-weight': 700 });

            // линия за височина
            const rightEdge = Math.max.apply(null, list.map(function (o) { return side ? o.L : (totalW + o.W) / 2 + 110; }));
            const hx = overlay ? rightEdge + 180 + i * 160 : (side ? x0 + c.L + 110 : x0 + c.W + 170);
            el('line', { x1: hx, x2: hx, y1: 0, y2: -c.H, stroke: COLORS[i], 'stroke-width': 14 }, labels);
            [0, -c.H].forEach(function (py) { el('line', { x1: hx - 45, x2: hx + 45, y1: py, y2: py, stroke: COLORS[i], 'stroke-width': 14 }, labels); });
            if (!overlay) text(hx + 40, -c.H - 60, meters(c.H), { fill: COLORS[i], 'font-weight': 700, 'font-size': fs * 0.9, 'text-anchor': 'start' });

            // надпис над колата
            if (!overlay) text(x0 + w / 2 - (side ? 0 : 110), -c.H - 170, c.short, { fill: COLORS[i], 'font-weight': 700, 'font-size': fs * 1.08 });
            x += w + gap;
        });

        if (overlayMode) {
            list.forEach(function (c, i) {
                text(0, -maxH - 360 + i * fs * 1.4, '■ ' + c.short + ' — ' + meters(side ? c.L : c.W) + ' × ' + meters(c.H),
                    { fill: COLORS[i], 'font-weight': 700, 'font-size': fs, 'text-anchor': 'start' });
            });
        }
    }

    // бутони за изглед и режим
    document.querySelectorAll('[data-size-view]').forEach(function (b) {
        b.addEventListener('click', function () {
            view = b.dataset.sizeView;
            document.querySelectorAll('[data-size-view]').forEach(function (x) { x.classList.toggle('active', x === b); });
            render();
        });
    });
    document.querySelectorAll('[data-size-mode]').forEach(function (b) {
        b.addEventListener('click', function () {
            mode = b.dataset.sizeMode;
            document.querySelectorAll('[data-size-mode]').forEach(function (x) { x.classList.toggle('active', x === b); });
            render();
        });
    });

    render();
})();
