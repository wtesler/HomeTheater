namespace TvKioskWrapper.Scripts.Navigators;

/// <summary>
/// Semantic D-Pad navigator tailored specifically to HBO Max (play.hbomax.com / play.max.com).
/// Understands HBO Max's DOM landmarks: Top Header, Slide-Out Browse Drawer, Immersive Hero,
/// Show/Movie Detail Actions, Tabs, Season Selector Listbox, Lazy-Loaded Horizontal Content Rails,
/// and 4-Column Search/My-Stuff Grids—while ignoring noisy 3-dot tile menu overlays.
/// </summary>
public static class HboMaxNavigatorScript
{
    public const string Source = """
        (function() {
            window.__TV_KIOSK_NAVIGATOR_MODE__ = 'hbomax';

            if (window.__tvKioskHboMaxNavInstalled) {
                if (window.__tvKioskHboMaxNavRefresh) {
                    window.__tvKioskHboMaxNavRefresh();
                }
                return;
            }
            window.__tvKioskHboMaxNavInstalled = true;

            let currentEl = null;
            let currentRowId = null;
            let preferredColIndex = 0;
            const railColMemory = new Map();
            let ringEl = null;
            let badgeEl = null;
            let rafId = null;
            let lastUrl = location.href;

            const ensureOverlay = () => {
                if (!document.body) return null;
                if (!ringEl || !document.body.contains(ringEl)) {
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
                        'border: 3.5px solid #00E5FF',
                        'border-radius: 10px',
                        'box-shadow: 0 0 0 2px rgba(0, 0, 0, 0.92), 0 0 20px rgba(0, 229, 255, 0.85)',
                        'transition: top 0.12s ease-out, left 0.12s ease-out, width 0.12s ease-out, height 0.12s ease-out, opacity 0.12s ease',
                        'opacity: 0',
                        'box-sizing: border-box'
                    ].join(';');

                    badgeEl = document.createElement('div');
                    badgeEl.id = '__tv_kiosk_focus_badge__';
                    badgeEl.style.cssText = [
                        'position: absolute',
                        'bottom: -26px',
                        'left: 0px',
                        'background: rgba(0, 12, 24, 0.92)',
                        'color: #00E5FF',
                        'font-family: Segoe UI, sans-serif',
                        'font-size: 11px',
                        'font-weight: 600',
                        'padding: 2px 8px',
                        'border-radius: 4px',
                        'border: 1px solid rgba(0, 229, 255, 0.5)',
                        'white-space: nowrap',
                        'display: none'
                    ].join(';');
                    ringEl.appendChild(badgeEl);
                    document.body.appendChild(ringEl);
                }
                return ringEl;
            };

            const isRendered = (el) => {
                if (!el || !document.body || !document.body.contains(el)) return false;
                const s = getComputedStyle(el);
                if (s.display === 'none' || s.visibility === 'hidden' || parseFloat(s.opacity) < 0.05) return false;
                const r = el.getBoundingClientRect();
                return r.width > 8 && r.height > 8;
            };

            const isVideoPlayerActive = () => {
                if (location.pathname.startsWith('/video/watch')) return true;
                if (document.fullscreenElement) return true;
                return false;
            };

            const getVisualRect = (el) => {
                if (!el) return null;
                // Special case: HBO Max's immersive-hero-surface-button spans the entire 1551x679 hero background.
                // Frame the hero title/metadata area neatly instead of the whole viewport.
                if (el.getAttribute('data-testid') === 'immersive-hero-surface-button') {
                    const playBtn = document.querySelector('section[data-testid$="_immersiveHero"] button[data-testid="play_button"]');
                    if (playBtn) {
                        const pr = playBtn.getBoundingClientRect();
                        return {
                            left: pr.left,
                            top: Math.max(90, pr.top - 130),
                            width: 340,
                            height: 110,
                            badge: 'More Info (Enter)'
                        };
                    }
                }
                const r = el.getBoundingClientRect();
                return { left: r.left, top: r.top, width: r.width, height: r.height, badge: null };
            };

            const updateRingPosition = () => {
                const ring = ensureOverlay();
                if (!ring) return;

                if (isVideoPlayerActive() || !currentEl || !isRendered(currentEl)) {
                    ring.style.opacity = '0';
                    return;
                }

                const vr = getVisualRect(currentEl);
                if (!vr || vr.width < 4 || vr.height < 4) {
                    ring.style.opacity = '0';
                    return;
                }

                const pad = 4;
                ring.style.top = `${Math.round(vr.top - pad)}px`;
                ring.style.left = `${Math.round(vr.left - pad)}px`;
                ring.style.width = `${Math.round(vr.width + pad * 2)}px`;
                ring.style.height = `${Math.round(vr.height + pad * 2)}px`;
                ring.style.opacity = '1';

                if (badgeEl) {
                    if (vr.badge) {
                        badgeEl.textContent = vr.badge;
                        badgeEl.style.display = 'block';
                    } else {
                        badgeEl.style.display = 'none';
                    }
                }
            };

            let lastFocusCheck = 0;

            const startTracking = () => {
                if (rafId) return;
                const tick = (now) => {
                    if (location.href !== lastUrl) {
                        lastUrl = location.href;
                        currentEl = null;
                        currentRowId = null;
                        preferredColIndex = 0;
                    }
                    if ((!currentEl || !isRendered(currentEl)) && now - lastFocusCheck > 250) {
                        lastFocusCheck = now;
                        ensureInitialFocus();
                    }
                    updateRingPosition();
                    rafId = requestAnimationFrame(tick);
                };
                rafId = requestAnimationFrame(tick);
            };

            // Detect if Slide-out Browse Menu is open
            const getSlideOutMenuItems = () => {
                const closeBtn = document.querySelector('button[data-testid="closeMenuButton"]');
                if (!closeBtn || !isRendered(closeBtn)) return null;

                const links = Array.from(document.querySelectorAll('a[href]')).filter(a => {
                    if (!isRendered(a)) return false;
                    const r = a.getBoundingClientRect();
                    return r.left >= 0 && r.left < 360 && r.top >= 70 && a.getAttribute('href') !== '#';
                });

                links.sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top);
                return [closeBtn, ...links];
            };

            // Detect if a Dropdown Listbox (e.g. Season Picker) is open
            const getOpenListboxItems = () => {
                const listbox = document.querySelector('[role="listbox"]');
                if (!listbox || !isRendered(listbox)) return null;
                const options = Array.from(listbox.querySelectorAll('[role="option"], li, button, a')).filter(isRendered);
                return options.length > 0 ? options : null;
            };

            // Build the ordered Semantic Rows of the current HBO Max page
            const buildSemanticRows = () => {
                const rows = [];

                // 1. Top Navigation Bar
                const topNavItems = [];
                const menuBtn = document.querySelector('button[data-testid="slide_out_nav_menuButton"]');
                if (isRendered(menuBtn)) topNavItems.push(menuBtn);

                const homeLogo = document.querySelector('header a[href="/home"], a[aria-label*="Home" i][href="/home"]');
                if (isRendered(homeLogo) && !topNavItems.includes(homeLogo)) topNavItems.push(homeLogo);

                // Any top-bar category tabs (if rendered in wide layout)
                for (const el of document.querySelectorAll('header a[href], nav a[href]')) {
                    if (!isRendered(el) || topNavItems.includes(el)) continue;
                    const r = el.getBoundingClientRect();
                    if (r.top >= 0 && r.top < 80 && r.left > 140 && r.left < 1300) {
                        topNavItems.push(el);
                    }
                }

                for (const sel of ['[data-testid="search_button"]', '[data-testid="my-stuff_button"]', '[data-testid="profile_button"]']) {
                    const btn = document.querySelector(sel);
                    if (isRendered(btn) && !topNavItems.includes(btn)) topNavItems.push(btn);
                }

                if (topNavItems.length > 0) {
                    topNavItems.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                    rows.push({
                        id: 'top-nav',
                        type: 'top-nav',
                        items: topNavItems
                    });
                }

                // 2. Search Bar Input (on /search)
                const searchInput = document.querySelector('input[data-testid="searchBar_field"]');
                if (isRendered(searchInput)) {
                    rows.push({
                        id: 'search-input',
                        type: 'search-input',
                        items: [searchInput]
                    });
                }

                // 3. Search Suggestion Chips (on /search)
                const chips = Array.from(document.querySelectorAll('button[data-testid$="_Suggestion"]')).filter(isRendered);
                if (chips.length > 0) {
                    chips.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                    rows.push({
                        id: 'search-chips',
                        type: 'chips',
                        items: chips
                    });
                }

                // 4. Immersive Hero Banner (on Home / Series / Movies)
                const heroSection = document.querySelector('section[data-testid$="_immersiveHero"]');
                if (heroSection && isRendered(heroSection)) {
                    const heroItems = [];
                    const prevBtn = heroSection.querySelector('button[aria-label="Previous title"]');
                    const playBtn = heroSection.querySelector('button[data-testid="play_button"]');
                    const myListBtn = heroSection.querySelector('button[data-testid="myList_button"]');
                    const surfaceLink = heroSection.querySelector('a[data-testid="immersive-hero-surface-button"]');
                    const audioBtn = heroSection.querySelector('button[data-testid="immersive-hero-audio-button"]');
                    const nextBtn = heroSection.querySelector('button[aria-label="Next title"]');

                    if (isRendered(playBtn)) heroItems.push(playBtn);
                    if (isRendered(myListBtn)) heroItems.push(myListBtn);
                    if (isRendered(surfaceLink)) heroItems.push(surfaceLink);
                    if (isRendered(audioBtn)) heroItems.push(audioBtn);
                    if (isRendered(prevBtn)) heroItems.unshift(prevBtn);
                    if (isRendered(nextBtn)) heroItems.push(nextBtn);

                    if (heroItems.length > 0) {
                        rows.push({
                            id: 'hero',
                            type: 'hero',
                            sectionEl: heroSection,
                            items: heroItems
                        });
                    }
                }

                // 5. Show / Movie Detail Page Hero Controls (when not an _immersiveHero section)
                if (!heroSection) {
                    const detailPlayBtns = Array.from(document.querySelectorAll('button[data-testid="play_button"]')).filter(isRendered);
                    const myListBtn = document.querySelector('button[data-testid="myList_button"]');
                    const rateBtn = document.querySelector('button[data-testid="user_action_toggle_button"]');

                    if (detailPlayBtns.length > 0) {
                        const primaryPlay = detailPlayBtns[0];
                        rows.push({
                            id: 'detail-primary',
                            type: 'detail-actions',
                            items: [primaryPlay]
                        });

                        const secondaryItems = [];
                        if (isRendered(myListBtn)) secondaryItems.push(myListBtn);
                        if (isRendered(rateBtn)) secondaryItems.push(rateBtn);
                        for (let i = 1; i < detailPlayBtns.length; i++) {
                            secondaryItems.push(detailPlayBtns[i]);
                        }
                        if (secondaryItems.length > 0) {
                            rows.push({
                                id: 'detail-secondary',
                                type: 'detail-actions',
                                items: secondaryItems
                            });
                        }
                    }
                }

                // 6. Page Tabs (e.g. Episodes / You May Also Like / Details, or My List / Continue Watching)
                const tabs = Array.from(document.querySelectorAll('button[role="tab"][data-testid$="_Tab"]')).filter(isRendered);
                if (tabs.length > 0) {
                    tabs.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                    rows.push({
                        id: 'page-tabs',
                        type: 'tabs',
                        items: tabs
                    });
                }

                // 7. Season Selector Dropdown Button (on Show pages)
                const seasonBtn = Array.from(document.querySelectorAll('button[class*="StyledSelectSort"], button[id^="dropdown-"][id$="-toggle"]'))
                    .find(b => isRendered(b) && !b.hasAttribute('data-testid'));
                if (seasonBtn) {
                    rows.push({
                        id: 'season-selector',
                        type: 'season-select',
                        items: [seasonBtn]
                    });
                }

                // 8. Horizontal Content Rails (section[data-testid$="_rail"])
                const railSections = Array.from(document.querySelectorAll('section[data-testid$="_rail"]')).filter(isRendered);
                for (let i = 0; i < railSections.length; i++) {
                    const sec = railSections[i];
                    const railId = sec.getAttribute('data-testid') || `rail-${i}`;
                    const tiles = Array.from(sec.querySelectorAll('a[data-testid$="_tile"]')).filter(isRendered);
                    rows.push({
                        id: railId,
                        type: 'rail',
                        sectionEl: sec,
                        items: tiles
                    });
                }

                // 9. Standalone 2D Tile Grids (Search Results, My Stuff, Genre A-Z pages where tiles are not inside section[data-testid$="_rail"])
                const standaloneTiles = Array.from(document.querySelectorAll('a[data-testid$="_tile"]'))
                    .filter(t => isRendered(t) && !t.closest('section[data-testid$="_rail"]'));

                if (standaloneTiles.length > 0) {
                    const rowBuckets = [];
                    for (const tile of standaloneTiles) {
                        const r = tile.getBoundingClientRect();
                        let bucket = rowBuckets.find(b => Math.abs(b.top - r.top) < 55);
                        if (!bucket) {
                            bucket = { top: r.top, items: [] };
                            rowBuckets.push(bucket);
                        }
                        bucket.items.push(tile);
                    }
                    rowBuckets.sort((a, b) => a.top - b.top);
                    rowBuckets.forEach((bucket, idx) => {
                        bucket.items.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                        rows.push({
                            id: `grid-row-${idx}`,
                            type: 'grid-row',
                            items: bucket.items
                        });
                    });
                }

                return rows;
            };

            const focusElement = (el, rowId, colIdx, scrollBlock = 'center') => {
                if (!el) return false;
                if (currentEl && currentEl !== el) {
                    currentEl.classList.remove('__tv_kiosk_focused__');
                }

                currentEl = el;
                window.__tvKioskCurrentFocusedEl = el;
                currentEl.classList.add('__tv_kiosk_focused__');

                if (rowId) {
                    currentRowId = rowId;
                }
                if (typeof colIdx === 'number') {
                    preferredColIndex = colIdx;
                    if (rowId) {
                        railColMemory.set(rowId, colIdx);
                    }
                }

                try {
                    if (el.tagName === 'INPUT') {
                        el.focus({ preventScroll: true });
                    } else {
                        if (document.activeElement && document.activeElement.tagName === 'INPUT') {
                            document.activeElement.blur();
                        }
                        el.focus({ preventScroll: true });
                    }
                } catch (_) {}

                try {
                    if (rowId === 'top-nav' || rowId === 'hero' || rowId === 'detail-primary') {
                        window.scrollTo({ top: 0, behavior: 'smooth' });
                    } else {
                        el.scrollIntoView({
                            behavior: 'smooth',
                            block: scrollBlock,
                            inline: 'center'
                        });
                    }
                } catch (_) {}

                updateRingPosition();
                startTracking();
                return true;
            };

            const ensureInitialFocus = () => {
                if (isVideoPlayerActive()) return null;
                if (currentEl && isRendered(currentEl)) return currentEl;

                const rows = buildSemanticRows();
                // Prefer focusing the primary Play button on Hero/Detail, Search Input on /search, or first hydrated Rail tile
                const preferredTypes = ['search-input', 'detail-actions', 'hero', 'rail', 'grid-row', 'top-nav'];
                for (const t of preferredTypes) {
                    const row = rows.find(r => r.type === t && r.items.length > 0);
                    if (row) {
                        // On hero row, start on play_button rather than Previous title arrow
                        let startIdx = 0;
                        if (row.type === 'hero') {
                            const playIdx = row.items.findIndex(i => i.getAttribute('data-testid') === 'play_button');
                            if (playIdx !== -1) startIdx = playIdx;
                        }
                        focusElement(row.items[startIdx], row.id, startIdx);
                        return currentEl;
                    }
                }
                return null;
            };

            // Hydrate a lazy-loaded HBO Max rail by scrolling its <section> into view and waiting for tiles
            const hydrateAndFocusRail = (rows, startRowIndex, directionDelta) => {
                let idx = startRowIndex;
                const step = directionDelta >= 0 ? 1 : -1;

                const tryIndex = (i) => {
                    if (i < 0 || i >= rows.length) return;
                    const targetRow = rows[i];

                    if (targetRow.items.length > 0) {
                        const savedCol = railColMemory.get(targetRow.id) ?? preferredColIndex;
                        const col = Math.min(Math.max(0, savedCol), targetRow.items.length - 1);
                        focusElement(targetRow.items[col], targetRow.id, col);
                        return;
                    }

                    if (targetRow.sectionEl) {
                        targetRow.sectionEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        let attempts = 0;
                        const poll = () => {
                            attempts++;
                            const tiles = Array.from(targetRow.sectionEl.querySelectorAll('a[data-testid$="_tile"]')).filter(isRendered);
                            if (tiles.length > 0) {
                                const savedCol = railColMemory.get(targetRow.id) ?? 0;
                                const col = Math.min(Math.max(0, savedCol), tiles.length - 1);
                                focusElement(tiles[col], targetRow.id, col);
                            } else if (attempts < 10) {
                                setTimeout(poll, 55);
                            } else {
                                // Empty placeholder rail; skip to next row in that direction
                                tryIndex(i + step);
                            }
                        };
                        setTimeout(poll, 60);
                    }
                };

                tryIndex(idx);
            };

            const handleDrawerNavigation = (drawerItems, key) => {
                let idx = drawerItems.indexOf(currentEl);
                if (idx === -1) {
                    focusElement(drawerItems[1] || drawerItems[0], 'slide-out-drawer', 0, 'nearest');
                    return;
                }

                if (key === 'ArrowDown') {
                    const nextIdx = Math.min(drawerItems.length - 1, idx + 1);
                    focusElement(drawerItems[nextIdx], 'slide-out-drawer', 0, 'nearest');
                } else if (key === 'ArrowUp') {
                    const prevIdx = Math.max(0, idx - 1);
                    focusElement(drawerItems[prevIdx], 'slide-out-drawer', 0, 'nearest');
                } else if (key === 'ArrowRight' || key === 'Escape') {
                    drawerItems[0].click(); // closeMenuButton
                    setTimeout(() => {
                        currentEl = null;
                        ensureInitialFocus();
                    }, 200);
                } else if (key === 'Enter') {
                    currentEl.click();
                    setTimeout(() => {
                        currentEl = null;
                        ensureInitialFocus();
                    }, 350);
                }
            };

            const handleListboxNavigation = (options, key) => {
                let idx = options.indexOf(currentEl);
                if (idx === -1) {
                    focusElement(options[0], 'open-listbox', 0, 'nearest');
                    return;
                }

                if (key === 'ArrowDown') {
                    focusElement(options[Math.min(options.length - 1, idx + 1)], 'open-listbox', 0, 'nearest');
                } else if (key === 'ArrowUp') {
                    focusElement(options[Math.max(0, idx - 1)], 'open-listbox', 0, 'nearest');
                } else if (key === 'Enter') {
                    currentEl.click();
                    setTimeout(() => {
                        const rows = buildSemanticRows();
                        const epRail = rows.find(r => r.type === 'rail' && r.items.length > 0);
                        if (epRail) {
                            focusElement(epRail.items[0], epRail.id, 0);
                        } else {
                            currentEl = null;
                            ensureInitialFocus();
                        }
                    }, 300);
                } else if (key === 'Escape') {
                    const seasonBtn = document.querySelector('button[class*="StyledSelectSort"]');
                    seasonBtn?.click();
                    if (seasonBtn) focusElement(seasonBtn, 'season-selector', 0);
                }
            };

            window.addEventListener('keydown', (e) => {
                if (isVideoPlayerActive()) return;

                const key = e.key;
                if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Enter', 'Escape'].includes(key)) {
                    return;
                }

                // 1. Check if Slide-Out Browse Menu is open
                const drawerItems = getSlideOutMenuItems();
                if (drawerItems) {
                    e.preventDefault();
                    e.stopPropagation();
                    handleDrawerNavigation(drawerItems, key);
                    return;
                }

                // 2. Check if Season / Dropdown Listbox is open
                const listboxItems = getOpenListboxItems();
                if (listboxItems) {
                    e.preventDefault();
                    e.stopPropagation();
                    handleListboxNavigation(listboxItems, key);
                    return;
                }

                // 3. Special handling when typing inside the Search Input box
                if (document.activeElement && document.activeElement.tagName === 'INPUT') {
                    if (key === 'ArrowLeft' || key === 'ArrowRight') {
                        // Allow normal caret movement inside search input
                        return;
                    }
                    if (key === 'Enter') {
                        return;
                    }
                    // ArrowDown or ArrowUp moves out of the search input into the semantic rows!
                }

                if (key === 'Escape') {
                    return;
                }

                const rows = buildSemanticRows();
                if (rows.length === 0) return;

                if (!currentEl || !isRendered(currentEl)) {
                    e.preventDefault();
                    e.stopPropagation();
                    ensureInitialFocus();
                    return;
                }

                // Locate current row & item index
                let rowIdx = rows.findIndex(r => r.items.includes(currentEl));
                if (rowIdx === -1 && currentRowId) {
                    rowIdx = rows.findIndex(r => r.id === currentRowId);
                }
                if (rowIdx === -1) {
                    rowIdx = 0;
                }

                const currentRow = rows[rowIdx];
                let colIdx = currentRow.items.indexOf(currentEl);
                if (colIdx === -1) {
                    colIdx = Math.min(preferredColIndex, Math.max(0, currentRow.items.length - 1));
                }

                if (key === 'Enter') {
                    e.preventDefault();
                    e.stopPropagation();
                    currentEl.click();

                    // If opening Slide-Out menu or Season selector, focus its first option automatically
                    const testId = currentEl.getAttribute('data-testid');
                    if (testId === 'slide_out_nav_menuButton') {
                        setTimeout(() => {
                            const items = getSlideOutMenuItems();
                            if (items && items.length > 1) {
                                focusElement(items[1], 'slide-out-drawer', 0, 'nearest');
                            }
                        }, 220);
                    } else if (currentRow.type === 'season-select') {
                        setTimeout(() => {
                            const opts = getOpenListboxItems();
                            if (opts && opts.length > 0) {
                                focusElement(opts[0], 'open-listbox', 0, 'nearest');
                            }
                        }, 200);
                    }
                    return;
                }

                e.preventDefault();
                e.stopPropagation();

                if (key === 'ArrowRight') {
                    if (colIdx + 1 < currentRow.items.length) {
                        focusElement(currentRow.items[colIdx + 1], currentRow.id, colIdx + 1, 'nearest');
                    }
                    return;
                }

                if (key === 'ArrowLeft') {
                    if (colIdx - 1 >= 0) {
                        focusElement(currentRow.items[colIdx - 1], currentRow.id, colIdx - 1, 'nearest');
                    } else if (currentRow.type !== 'top-nav') {
                        // Pressing Left at the start of a row can open the Slide-Out Browse Menu if desired, or stay at col 0
                    }
                    return;
                }

                if (key === 'ArrowDown') {
                    if (rowIdx + 1 < rows.length) {
                        hydrateAndFocusRail(rows, rowIdx + 1, 1);
                    } else if (currentRow.type === 'grid-row') {
                        // Scroll down to trigger infinite scroll in 2D grids (Search / My Stuff)
                        window.scrollBy({ top: 450, behavior: 'smooth' });
                        setTimeout(() => {
                            const updatedRows = buildSemanticRows();
                            if (rowIdx + 1 < updatedRows.length) {
                                const nextRow = updatedRows[rowIdx + 1];
                                const nextCol = Math.min(colIdx, nextRow.items.length - 1);
                                focusElement(nextRow.items[nextCol], nextRow.id, nextCol);
                            }
                        }, 250);
                    }
                    return;
                }

                if (key === 'ArrowUp') {
                    if (rowIdx - 1 >= 0) {
                        hydrateAndFocusRail(rows, rowIdx - 1, -1);
                    }
                    return;
                }
            }, true);

            window.__tvKioskHboMaxNavRefresh = () => {
                updateRingPosition();
                if (!currentEl || !isRendered(currentEl)) {
                    ensureInitialFocus();
                }
            };

            setTimeout(() => {
                ensureInitialFocus();
                startTracking();
            }, 300);
        })();
        """;
}
