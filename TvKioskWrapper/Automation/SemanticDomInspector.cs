namespace TvKioskWrapper.Automation;

/// <summary>
/// Provides compact, token-efficient JavaScript inspectors that summarize a webpage's semantic
/// structure, landmarks, rails/carousels, and active focus state into small JSON payloads.
/// </summary>
public static class SemanticDomInspector
{
    public const string ActiveElementScript = """
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
        """;

    public const string SemanticSummaryScript = """
        (function() {
            const vw = window.innerWidth;
            const vh = window.innerHeight;

            const isVisible = (el) => {
                if (!el) return false;
                const s = getComputedStyle(el);
                if (s.display === 'none' || s.visibility === 'hidden' || parseFloat(s.opacity) < 0.05) return false;
                const r = el.getBoundingClientRect();
                return r.width > 8 && r.height > 8 && r.bottom > 0 && r.right > 0 && r.top < vh * 1.5 && r.left < vw * 1.5;
            };

            const shortText = (el, max = 60) => (el?.innerText || el?.textContent || '').trim().replace(/\s+/g, ' ').slice(0, max);

            const summarizeEl = (el) => {
                const r = el.getBoundingClientRect();
                return {
                    tag: el.tagName.toLowerCase(),
                    id: el.id || undefined,
                    testId: el.getAttribute('data-testid') || undefined,
                    role: el.getAttribute('role') || undefined,
                    ariaLabel: el.getAttribute('aria-label') || undefined,
                    text: shortText(el, 50) || undefined,
                    href: el.getAttribute('href') || undefined,
                    classes: typeof el.className === 'string' && el.className.trim()
                        ? el.className.trim().split(/\s+/).slice(0, 3).join('.')
                        : undefined,
                    rect: [Math.round(r.left), Math.round(r.top), Math.round(r.width), Math.round(r.height)]
                };
            };

            // 1. Collect high-level landmarks & regions
            const landmarkSelectors = 'nav, header, aside, main, footer, [role="navigation"], [role="banner"], [role="main"], [role="dialog"], [role="tablist"], [role="menubar"], [data-testid]';
            const landmarks = [];
            const seenLandmarks = new Set();

            for (const el of document.querySelectorAll(landmarkSelectors)) {
                if ( landmarks.length >= 35 ) break;
                if (!isVisible(el)) continue;
                const tag = el.tagName.toLowerCase();
                const role = el.getAttribute('role');
                const testId = el.getAttribute('data-testid');
                const isMajor = ['nav', 'header', 'aside', 'main', 'footer'].includes(tag) ||
                    role === 'navigation' || role === 'dialog' || role === 'tablist' ||
                    (testId && /nav|menu|header|rail|row|carousel|collection|hero|tile|card|search|profile|tab|player|modal/i.test(testId));
                if (!isMajor) continue;

                const key = `${tag}:${el.id}:${testId}:${role}`;
                if (seenLandmarks.has(key)) continue;
                seenLandmarks.add(key);

                const focusableCount = el.querySelectorAll('a[href], button, input, [tabindex], [role="button"], [role="link"], [role="tab"]').length;
                landmarks.push({
                    ...summarizeEl(el),
                    focusableCount
                });
            }

            // 2. Detect horizontal rails / carousels / grids (containers with >= 2 interactive children aligned horizontally or in a section)
            const railCandidates = document.querySelectorAll('section, [role="region"], [role="list"], [data-testid*="rail" i], [data-testid*="row" i], [data-testid*="collection" i], [data-testid*="carousel" i], [class*="rail" i], [class*="row" i], [class*="carousel" i], [class*="shelf" i], [class*="collection" i]');
            const rails = [];
            for (const container of railCandidates) {
                if (rails.length >= 15) break;
                if (!isVisible(container)) continue;
                const items = Array.from(container.querySelectorAll('a[href], button, [role="button"], [role="link"], [tabindex="0"]'))
                    .filter(isVisible);
                if (items.length < 2) continue;

                const headingEl = container.querySelector('h1, h2, h3, h4, [role="heading"], [class*="title" i], [class*="header" i]');
                const r = container.getBoundingClientRect();
                rails.push({
                    tag: container.tagName.toLowerCase(),
                    testId: container.getAttribute('data-testid') || undefined,
                    ariaLabel: container.getAttribute('aria-label') || undefined,
                    heading: shortText(headingEl, 50) || undefined,
                    classes: typeof container.className === 'string' ? container.className.trim().split(/\s+/).slice(0, 3).join('.') : undefined,
                    rect: [Math.round(r.left), Math.round(r.top), Math.round(r.width), Math.round(r.height)],
                    itemCount: items.length,
                    sampleItems: items.slice(0, 4).map(summarizeEl)
                });
            }

            // 3. Collect visible interactive elements (grouped/capped to stay compact)
            const interactives = Array.from(document.querySelectorAll(
                'a[href], button, input, select, textarea, [role="button"], [role="link"], [role="tab"], [role="menuitem"], [tabindex="0"]'
            )).filter(isVisible);

            return {
                url: location.href,
                title: document.title,
                viewport: [vw, vh],
                totalInteractiveCount: interactives.length,
                activeElement: document.activeElement ? summarizeEl(document.activeElement) : null,
                landmarks,
                rails,
                topInteractiveSample: interactives.slice(0, 30).map(summarizeEl)
            };
        })();
        """;
}

