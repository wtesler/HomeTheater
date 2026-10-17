(function () {
    if (window.__tvKioskSpatialNavInstalled) {
        if (window.__tvKioskSpatialNavRefresh) {
            window.__tvKioskSpatialNavRefresh();
        }
        return;
    }
    window.__tvKioskSpatialNavInstalled = true;

    const isNativeTvInterface = () => {
        return window.__TV_KIOSK_DISABLE_SPATIAL_NAV__ === true;
    };

    let currentEl = null;
    let ringEl = null;
    let rafId = null;

    const ensureRing = () => {
        if (ringEl && document.body && document.body.contains(ringEl)) return ringEl;
        if (!document.body) return null;
        ringEl = document.createElement('div');
        ringEl.id = '__tv_kiosk_focus_ring__';
        ringEl.style.cssText = [
            'position: fixed',
            'top: 0px',
            'left: 0px',
            'width: 0px',
            'height: 0px',
            'pointer-events: none',
            'z-index: 2147483647',
            'border: 4px solid #00E5FF',
            'border-radius: 10px',
            'box-shadow: 0 0 0 2px rgba(0, 0, 0, 0.9), 0 0 22px rgba(0, 229, 255, 0.85)',
            'transition: top 0.11s ease-out, left 0.11s ease-out, width 0.11s ease-out, height 0.11s ease-out, opacity 0.15s ease',
            'opacity: 0',
            'box-sizing: border-box'
        ].join(';');
        document.body.appendChild(ringEl);
        return ringEl;
    };

    const isFullscreenVideoActive = () => {
        if (document.fullscreenElement) return true;
        const videos = document.querySelectorAll('video');
        for (const v of videos) {
            if (!v.paused && v.readyState >= 2) {
                const r = v.getBoundingClientRect();
                if (r.width >= window.innerWidth * 0.75 && r.height >= window.innerHeight * 0.65) {
                    return true;
                }
            }
        }
        return false;
    };

    const isVisible = (el) => {
        if (!el || !el.isConnected) return false;
        const style = window.getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden' || parseFloat(style.opacity) < 0.05) {
            return false;
        }
        const r = el.getBoundingClientRect();
        if (r.width < 22 || r.height < 22) return false;
        if (r.bottom < -120 || r.top > window.innerHeight + 350) return false;
        if (r.right < -60 || r.left > window.innerWidth + 60) return false;
        return true;
    };

    const getCandidates = () => {
        if (isNativeTvInterface()) return [];

        const cardContainersSelector = [
            'ytd-rich-item-renderer',
            'ytd-video-renderer',
            'ytd-grid-video-renderer',
            'ytd-compact-video-renderer',
            'ytd-reel-item-renderer',
            '.slider-item',
            '.title-card-container',
            'article[data-testid]',
            '[data-automation-id="HeroCarousel"] li'
        ].join(',');

        const chosen = new Set();
        const results = [];

        const containers = document.querySelectorAll(cardContainersSelector);
        for (const container of containers) {
            if (!isVisible(container)) continue;
            const primary = container.querySelector('a#thumbnail, a#video-title-link, a[href*="/watch"], a[href*="/detail/"], a[href], button') || container;
            if (isVisible(primary)) {
                chosen.add(primary);
                results.push(primary);
            }
        }

        const controlSelector = [
            'input#search',
            'input[type="search"]',
            'input[type="text"]',
            'input[name="search_query"]',
            '.ytSearchboxComponentInput',
            'button#search-icon-legacy',
            'yt-chip-cloud-chip-renderer',
            'ytd-guide-entry-renderer a',
            'ytd-mini-guide-entry-renderer a',
            'header a[href]',
            'header button',
            'nav a[href]',
            'nav button',
            '.profile-link',
            '.billboard-links a',
            '.billboard-links button',
            'a[href]',
            'button',
            '[role="button"]',
            '[role="tab"]',
            '[role="link"]'
        ].join(',');

        const controls = document.querySelectorAll(controlSelector);
        for (const el of controls) {
            if (chosen.has(el)) continue;
            const parentCard = el.closest(cardContainersSelector);
            if (parentCard && results.some(r => parentCard.contains(r))) {
                continue;
            }
            if (!isVisible(el)) continue;
            chosen.add(el);
            results.push(el);
        }

        const deduped = [];
        for (const el of results) {
            const r = el.getBoundingClientRect();
            const duplicate = deduped.some(other => {
                const or = other.getBoundingClientRect();
                return Math.abs(r.left - or.left) < 12 &&
                       Math.abs(r.top - or.top) < 12 &&
                       Math.abs(r.width - or.width) < 20 &&
                       Math.abs(r.height - or.height) < 20;
            });
            if (!duplicate) {
                deduped.push(el);
            }
        }

        return deduped;
    };

    const updateRingPosition = () => {
        const ring = ensureRing();
        if (!ring) return;

        if (isNativeTvInterface() || !currentEl || !isVisible(currentEl) || isFullscreenVideoActive()) {
            ring.style.opacity = '0';
            return;
        }

        const r = currentEl.getBoundingClientRect();
        const pad = 4;
        ring.style.left = `${Math.round(r.left - pad)}px`;
        ring.style.top = `${Math.round(r.top - pad)}px`;
        ring.style.width = `${Math.round(r.width + pad * 2)}px`;
        ring.style.height = `${Math.round(r.height + pad * 2)}px`;
        ring.style.opacity = '1';
    };

    const selectElement = (el, scroll = true) => {
        if (!el) return;
        currentEl = el;
        if (scroll) {
            try {
                el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
            } catch (_) {}
        }
        try {
            if (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA') {
                el.focus({ preventScroll: true });
            }
            el.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
            el.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
        } catch (_) {}

        updateRingPosition();
        setTimeout(updateRingPosition, 140);
        setTimeout(updateRingPosition, 320);
    };

    const findNextCandidate = (dir) => {
        const candidates = getCandidates();
        if (candidates.length === 0) return null;

        if (!currentEl || !isVisible(currentEl)) {
            return candidates[0];
        }

        const cur = currentEl.getBoundingClientRect();
        const cx = cur.left + cur.width / 2;
        const cy = cur.top + cur.height / 2;

        let best = null;
        let bestScore = Infinity;

        for (const cand of candidates) {
            if (cand === currentEl) continue;
            const r = cand.getBoundingClientRect();
            const tx = r.left + r.width / 2;
            const ty = r.top + r.height / 2;
            const dx = tx - cx;
            const dy = ty - cy;

            let primaryDist = 0;
            let crossDist = 0;
            let overlap = 0;

            if (dir === 'Right') {
                if (dx <= 10) continue;
                primaryDist = Math.max(0, r.left - cur.right);
                crossDist = Math.abs(dy);
                overlap = Math.max(0, Math.min(cur.bottom, r.bottom) - Math.max(cur.top, r.top));
            } else if (dir === 'Left') {
                if (dx >= -10) continue;
                primaryDist = Math.max(0, cur.left - r.right);
                crossDist = Math.abs(dy);
                overlap = Math.max(0, Math.min(cur.bottom, r.bottom) - Math.max(cur.top, r.top));
            } else if (dir === 'Down') {
                if (dy <= 10) continue;
                primaryDist = Math.max(0, r.top - cur.bottom);
                crossDist = Math.abs(dx);
                overlap = Math.max(0, Math.min(cur.right, r.right) - Math.max(cur.left, r.left));
            } else if (dir === 'Up') {
                if (dy >= -10) continue;
                primaryDist = Math.max(0, cur.top - r.bottom);
                crossDist = Math.abs(dx);
                overlap = Math.max(0, Math.min(cur.right, r.right) - Math.max(cur.left, r.left));
            }

            if ((dir === 'Left' || dir === 'Right') && overlap <= 0 && crossDist > cur.height * 1.1) {
                continue;
            }

            const alignmentBonus = overlap > 0 ? 0 : 250;
            const crossWeight = (dir === 'Left' || dir === 'Right') ? 4.0 : 2.2;
            const score = primaryDist + crossDist * crossWeight + alignmentBonus;

            if (score < bestScore) {
                bestScore = score;
                best = cand;
            }
        }

        return best;
    };

    window.addEventListener('keydown', (e) => {
        if (isNativeTvInterface()) return;

        const active = document.activeElement;
        const isTextInput = active && (
            active.tagName === 'INPUT' ||
            active.tagName === 'TEXTAREA' ||
            active.isContentEditable
        );

        if (isTextInput) {
            if (e.key === 'Escape' || e.key === 'ArrowDown') {
                e.preventDefault();
                e.stopPropagation();
                active.blur();
                const next = findNextCandidate('Down') || getCandidates()[0];
                if (next) selectElement(next);
                return;
            }
            if (e.key === 'Enter') {
                const form = active.closest('form');
                const searchBtn = document.querySelector('button#search-icon-legacy, button.ytSearchboxComponentSearchButton, button[type="submit"]');
                if (searchBtn) {
                    searchBtn.click();
                } else if (form) {
                    form.requestSubmit ? form.requestSubmit() : form.submit();
                }
                active.blur();
                return;
            }
            return;
        }

        if (isFullscreenVideoActive()) {
            return;
        }

        if (e.key === 'ArrowRight' || e.key === 'ArrowLeft' || e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            const dir = e.key.replace('Arrow', '');
            const next = findNextCandidate(dir);

            if (next) {
                e.preventDefault();
                e.stopPropagation();
                selectElement(next);
            } else if (dir === 'Down') {
                e.preventDefault();
                e.stopPropagation();
                window.scrollBy({ top: Math.round(window.innerHeight * 0.6), behavior: 'smooth' });
                setTimeout(() => {
                    const afterScroll = findNextCandidate('Down');
                    if (afterScroll) selectElement(afterScroll);
                }, 280);
            } else if (dir === 'Up' && window.scrollY > 50) {
                e.preventDefault();
                e.stopPropagation();
                window.scrollBy({ top: -Math.round(window.innerHeight * 0.6), behavior: 'smooth' });
                setTimeout(() => {
                    const afterScroll = findNextCandidate('Up');
                    if (afterScroll) selectElement(afterScroll);
                }, 280);
            }
            return;
        }

        if (e.key === 'Enter' && currentEl && isVisible(currentEl)) {
            e.preventDefault();
            e.stopPropagation();
            if (currentEl.tagName === 'INPUT' || currentEl.tagName === 'TEXTAREA') {
                currentEl.focus();
                currentEl.click();
            } else {
                const clickable = currentEl.querySelector('a[href], button, input') || currentEl;
                clickable.click();
            }
        }
    }, true);

    window.addEventListener('scroll', () => {
        if (rafId) cancelAnimationFrame(rafId);
        rafId = requestAnimationFrame(updateRingPosition);
    }, { passive: true });

    window.addEventListener('resize', updateRingPosition, { passive: true });

    window.__tvKioskSpatialNavRefresh = () => {
        if (isNativeTvInterface()) {
            if (ringEl) ringEl.style.opacity = '0';
            return;
        }
        if (!currentEl || !isVisible(currentEl)) {
            const initial = getCandidates()[0];
            if (initial) selectElement(initial, false);
        } else {
            updateRingPosition();
        }
    };

    setTimeout(window.__tvKioskSpatialNavRefresh, 600);
})();

