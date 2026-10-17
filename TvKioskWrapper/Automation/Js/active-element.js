(function() {
    const describeEl = (el) => {
        if (!el || el === document.body || el === document.documentElement) {
            return { tag: el ? el.tagName : null, isBody: true };
        }
        const r = el.getBoundingClientRect();
        const testId = el.getAttribute('data-testid') || el.closest('[data-testid]')?.getAttribute('data-testid') || null;
        const role = el.getAttribute('role') || null;
        const ariaLabel = el.getAttribute('aria-label') || el.getAttribute('title') || null;
        const text = (el.innerText || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 80);
        const href = el.getAttribute('href') || el.closest('a')?.getAttribute('href') || null;
        return {
            tag: el.tagName,
            id: el.id || null,
            className: typeof el.className === 'string' ? el.className.split(/\s+/).slice(0, 4).join(' ') : null,
            testId,
            role,
            ariaLabel,
            text,
            href,
            rect: {
                x: Math.round(r.left),
                y: Math.round(r.top),
                w: Math.round(r.width),
                h: Math.round(r.height)
            }
        };
    };

    const customFocused = window.__tvKioskCurrentFocusedEl || document.querySelector('.__tv_kiosk_focused__');
    return {
        url: location.href,
        title: document.title,
        navigatorMode: window.__TV_KIOSK_NAVIGATOR_MODE__ || null,
        domActiveElement: describeEl(document.activeElement),
        kioskFocusedElement: customFocused ? describeEl(customFocused) : null
    };
})();

