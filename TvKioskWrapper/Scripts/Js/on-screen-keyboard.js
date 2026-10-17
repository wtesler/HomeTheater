(function () {
    if (window.__tvKioskOskInstalled) return;
    window.__tvKioskOskInstalled = true;

    const TEXT_TYPES = new Set(['text', 'search', 'email', 'url', 'tel', 'password', 'number']);

    const LAYERS = {
        alpha: [
            ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'],
            ['q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p'],
            ['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', "'"],
            ['z', 'x', 'c', 'v', 'b', 'n', 'm', ',', '.', '-']
        ],
        symbols: [
            ['!', '@', '#', '$', '%', '^', '&', '*', '(', ')'],
            ['~', '`', '_', '+', '=', '/', '\\', ':', ';', '"'],
            ['[', ']', '{', '}', '<', '>', '|', '?', "'", '-'],
            ['.', ',', '@', '_', '-', '/', ':', '?', '!', '&']
        ]
    };

    const CONTROL_ROW = [
        { action: 'shift', label: '\u21E7', w: 1.4 },
        { action: 'layer', label: '?123', w: 1.6 },
        { action: 'space', label: 'space', w: 3.6 },
        { action: 'backspace', label: '\u232B', w: 1.4 },
        { action: 'clear', label: 'Clear', w: 1.6 },
        { action: 'go', label: 'Go \u21B5', w: 1.6 },
        { action: 'close', label: '\u2715', w: 1 }
    ];

    const isTextField = (el) => {
        if (!el || el.disabled || el.readOnly) return false;
        if (el.tagName === 'TEXTAREA') return true;
        return el.tagName === 'INPUT' && TEXT_TYPES.has((el.type || 'text').toLowerCase());
    };

    let host = null;
    let panel = null;
    let visible = false;
    let target = null;
    let layer = 'alpha';
    let shift = false;
    let cursor = { row: 1, col: 0 };
    let rows = []; // rows[r][c] = { el, def }

    const css = `
        :host { all: initial; }
        .panel {
            position: fixed; left: 50%; bottom: 24px; transform: translateX(-50%);
            width: min(1000px, 92vw); box-sizing: border-box; padding: 12px;
            background: rgba(8, 12, 20, 0.97); border: 2px solid rgba(0, 229, 255, 0.55);
            border-radius: 18px; box-shadow: 0 12px 48px rgba(0, 0, 0, 0.75);
            font-family: 'Segoe UI', sans-serif; z-index: 2147483646;
            display: flex; flex-direction: column; gap: 8px;
        }
        .row { display: flex; gap: 8px; }
        .key {
            flex: var(--w, 1) 1 0; min-width: 0; height: 52px;
            display: flex; align-items: center; justify-content: center;
            background: #1d2733; color: #fff; font-size: 26px; font-weight: 600;
            border: 3px solid transparent; border-radius: 10px; box-sizing: border-box;
            user-select: none;
        }
        .key.toggled { background: #0b4f5c; }
        .key.active {
            background: #00E5FF; color: #00131a; border-color: #fff;
            box-shadow: 0 0 18px rgba(0, 229, 255, 0.9);
        }
    `;

    const ensureHost = () => {
        if (host && document.documentElement.contains(host)) return;
        host = document.createElement('div');
        host.id = '__tv_kiosk_osk__';
        const root = host.attachShadow({ mode: 'open' });
        const style = document.createElement('style');
        style.textContent = css;
        panel = document.createElement('div');
        panel.className = 'panel';
        root.append(style, panel);
        document.documentElement.appendChild(host);
    };

    const displayChar = (ch) => (shift && /^[a-z]$/.test(ch) ? ch.toUpperCase() : ch);

    const render = () => {
        ensureHost();
        panel.textContent = '';
        rows = [];
        const addRow = (defs) => {
            const rowEl = document.createElement('div');
            rowEl.className = 'row';
            const row = defs.map((def) => {
                const el = document.createElement('div');
                el.className = 'key';
                el.style.setProperty('--w', def.w || 1);
                if ((def.action === 'shift' && shift) || (def.action === 'layer' && layer === 'symbols')) {
                    el.classList.add('toggled');
                }
                el.textContent = def.action === 'layer'
                    ? (layer === 'alpha' ? '?123' : 'ABC')
                    : (def.char !== undefined ? displayChar(def.char) : def.label);
                rowEl.appendChild(el);
                return { el, def };
            });
            panel.appendChild(rowEl);
            rows.push(row);
        };

        for (const chars of LAYERS[layer]) {
            addRow(chars.map((ch) => ({ action: 'char', char: ch })));
        }
        addRow(CONTROL_ROW);
        updateActive();
    };

    const updateActive = () => {
        cursor.row = Math.min(Math.max(cursor.row, 0), rows.length - 1);
        cursor.col = Math.min(Math.max(cursor.col, 0), rows[cursor.row].length - 1);
        rows.forEach((row, r) => row.forEach((k, c) => k.el.classList.toggle('active', r === cursor.row && c === cursor.col)));
    };

    const show = (el) => {
        target = el;
        if (!visible) {
            visible = true;
            layer = 'alpha';
            shift = false;
            cursor = { row: 1, col: 0 };
            render();
        }
        requestAnimationFrame(() => {
            if (!visible || !target || !panel) return;
            const tr = target.getBoundingClientRect();
            if (tr.bottom > panel.getBoundingClientRect().top - 8) {
                target.scrollIntoView({ block: 'start', behavior: 'instant' });
            }
        });
    };

    const hide = () => {
        visible = false;
        if (target && typeof target.blur === 'function') {
            try { target.blur(); } catch (_) {}
        }
        if (document.activeElement && isTextField(document.activeElement)) {
            try { document.activeElement.blur(); } catch (_) {}
        }
        if (document.body) {
            try {
                if (!document.body.hasAttribute('tabindex')) {
                    document.body.setAttribute('tabindex', '-1');
                }
                document.body.focus({ preventScroll: true });
            } catch (_) {}
        }
        target = null;
        if (host) host.remove();
        host = null;
        panel = null;
        rows = [];
    };

    const setValue = (el, value) => {
        const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
        Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, value);
    };

    const edit = (el, kind, data) => {
        let start = el.value.length;
        let end = start;
        try {
            if (el.selectionStart != null) {
                start = el.selectionStart;
                end = el.selectionEnd;
            }
        } catch (_) { /* e.g. type=email/number */ }

        let value = el.value;
        let caret;
        let inputType = 'insertText';
        if (kind === 'insert') {
            value = value.slice(0, start) + data + value.slice(end);
            caret = start + data.length;
        } else if (kind === 'backspace') {
            if (start === end) {
                if (start === 0) return;
                start -= 1;
            }
            value = value.slice(0, start) + value.slice(end);
            caret = start;
            inputType = 'deleteContentBackward';
        } else {
            value = '';
            caret = 0;
            inputType = 'deleteContent';
        }

        setValue(el, value);
        try { el.setSelectionRange(caret, caret); } catch (_) { /* unsupported type */ }
        el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType, data: kind === 'insert' ? data : null }));
    };

    const submit = (el) => {
        const init = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };
        const notPrevented = el.dispatchEvent(new KeyboardEvent('keydown', init));
        el.dispatchEvent(new KeyboardEvent('keypress', init));
        el.dispatchEvent(new KeyboardEvent('keyup', init));
        if (notPrevented && el.form) {
            try { el.form.requestSubmit(); } catch (_) { /* no-op */ }
        }
    };

    const press = (def) => {
        if (!target) return;
        switch (def.action) {
            case 'char': {
                edit(target, 'insert', displayChar(def.char));
                if (shift) { shift = false; render(); }
                break;
            }
            case 'space': edit(target, 'insert', ' '); break;
            case 'backspace': edit(target, 'backspace'); break;
            case 'clear': edit(target, 'clear'); break;
            case 'shift': shift = !shift; render(); break;
            case 'layer': layer = layer === 'alpha' ? 'symbols' : 'alpha'; render(); break;
            case 'go': submit(target); hide(); break;
            case 'close': hide(); break;
        }
    };

    const move = (dRow, dCol) => {
        if (dCol) {
            cursor.col += dCol;
        } else {
            const cur = rows[cursor.row][cursor.col].el.getBoundingClientRect();
            const centerX = cur.left + cur.width / 2;
            const nextRow = Math.min(Math.max(cursor.row + dRow, 0), rows.length - 1);
            let best = 0;
            let bestDist = Infinity;
            rows[nextRow].forEach((k, c) => {
                const r = k.el.getBoundingClientRect();
                const dist = Math.abs(r.left + r.width / 2 - centerX);
                if (dist < bestDist) { bestDist = dist; best = c; }
            });
            cursor = { row: nextRow, col: best };
        }
        updateActive();
    };

    const consume = (e) => {
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
    };

    // Registered on window/capture and injected BEFORE the semantic navigator core so that,
    // while the keyboard is open, D-Pad keys drive the keyboard instead of page navigation.
    window.addEventListener('keydown', (e) => {
        const active = document.activeElement;

        if (!visible) {
            if (e.key === 'Enter' && !e.repeat && isTextField(active)) {
                show(active);
                consume(e);
            }
            return;
        }

        if (!isTextField(active)) {
            hide();
            return;
        }
        target = active;

        switch (e.key) {
            case 'ArrowLeft': move(0, -1); consume(e); break;
            case 'ArrowRight': move(0, 1); consume(e); break;
            case 'ArrowUp': move(-1, 0); consume(e); break;
            case 'ArrowDown':
                if (cursor.row >= rows.length - 1) {
                    hide(); // let page navigation continue downward (e.g. to search results)
                } else {
                    move(1, 0);
                    consume(e);
                }
                break;
            case 'Enter':
                if (!e.repeat) press(rows[cursor.row][cursor.col].def);
                consume(e);
                break;
            case 'Escape': hide(); consume(e); break;
        }
    }, true);

    document.addEventListener('focusin', (e) => {
        if (isTextField(e.target)) show(e.target);
    }, true);

    document.addEventListener('focusout', () => {
        setTimeout(() => {
            if (visible && !isTextField(document.activeElement)) hide();
        }, 0);
    }, true);

    window.__tvKioskOsk = {
        isVisible: () => visible,
        tryClose: () => {
            if (!visible) {
                const active = document.activeElement;
                if (isTextField(active)) {
                    try { active.blur(); } catch (_) {}
                    return true;
                }
                return false;
            }
            hide();
            return true;
        }
    };
})();
