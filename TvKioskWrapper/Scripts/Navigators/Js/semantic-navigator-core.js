(function () {
    if (window.__tvKioskSemanticCoreInstalled) return;
    window.__tvKioskSemanticCoreInstalled = true;

    const isRendered = (el) => {
        if (!el || !document.body || !document.body.contains(el)) return false;
        const s = getComputedStyle(el);
        if (s.display === 'none' || s.visibility === 'hidden' || parseFloat(s.opacity) < 0.05) return false;
        const r = el.getBoundingClientRect();
        return r.width > 8 && r.height > 8;
    };

    const clusterIntoGridRows = (elements, rowTolerance = 55, idPrefix = 'grid-row') => {
        const buckets = [];
        for (const el of elements) {
            if (!isRendered(el)) continue;
            const r = el.getBoundingClientRect();
            let bucket = buckets.find(b => Math.abs(b.top - r.top) < rowTolerance);
            if (!bucket) {
                bucket = { top: r.top, items: [] };
                buckets.push(bucket);
            }
            bucket.items.push(el);
        }
        buckets.sort((a, b) => a.top - b.top);
        return buckets.map((bucket, idx) => {
            bucket.items.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
            return {
                id: `${idPrefix}-${idx}`,
                type: 'grid-row',
                items: bucket.items
            };
        });
    };

    const activateElement = (el) => {
        if (!el) return;
        try {
            el.focus({ preventScroll: true });
            el.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 }));
            el.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, button: 0 }));
            el.click();
        } catch (_) {}
    };

    window.__tvKioskNavUtils = { isRendered, clusterIntoGridRows, activateElement };

    let activeAdapter = null;
    let currentEl = null;
    let currentRowId = null;
    let preferredColIndex = 0;
    let inSidebar = false;
    let lastMainEl = null;
    let lastMainRowId = null;
    let lastMainColIndex = 0;
    const railColMemory = new Map();

    let ringEl = null;
    let badgeEl = null;
    let rafId = null;
    let lastUrl = location.href;
    let lastFocusCheck = 0;
    let emptyRowChecks = 0;

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

    const isPlayerActive = () => {
        if (document.fullscreenElement) return true;
        return activeAdapter?.isVideoPlayerActive ? activeAdapter.isVideoPlayerActive() : false;
    };

    const getVisualRect = (el) => {
        if (!el) return null;
        if (activeAdapter?.getVisualRect) {
            const custom = activeAdapter.getVisualRect(el);
            if (custom) return custom;
        }
        const r = el.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height, badge: null };
    };

    const updateRingPosition = () => {
        const ring = ensureOverlay();
        if (!ring) return;

        if (isPlayerActive() || !currentEl || !isRendered(currentEl)) {
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
            if (rowId && rowId !== 'sidebar' && rowId !== 'slide-out-drawer') {
                railColMemory.set(rowId, colIdx);
            }
        }

        if (rowId !== 'sidebar' && rowId !== 'slide-out-drawer' && rowId !== 'open-listbox') {
            inSidebar = false;
            lastMainEl = el;
            lastMainRowId = rowId;
            lastMainColIndex = preferredColIndex;
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
            if (activeAdapter?.scrollElementIntoView) {
                activeAdapter.scrollElementIntoView(el, rowId, scrollBlock);
            } else if (rowId === 'top-nav' || rowId === 'hero' || rowId === 'detail-primary') {
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
        return true;
    };

    const ensureInitialFocus = () => {
        if (!activeAdapter || isPlayerActive()) return null;

        const openListboxItems = activeAdapter.getOpenListboxItems
            ? activeAdapter.getOpenListboxItems(window.__tvKioskNavUtils)
            : null;
        if (openListboxItems && openListboxItems.length > 0) {
            if (!openListboxItems.includes(currentEl)) {
                focusElement(openListboxItems[0], 'open-listbox', 0, 'nearest');
            }
            return currentEl;
        }

        if (currentEl && isRendered(currentEl)) return currentEl;

        const rows = activeAdapter.buildSemanticRows(window.__tvKioskNavUtils);
        const preferredTypes = activeAdapter.preferredInitialRowTypes || [
            'search-input', 'detail-actions', 'hero', 'rail', 'grid-row', 'tabs', 'top-nav'
        ];

        for (const t of preferredTypes) {
            const row = rows.find(r => r.type === t && r.items.length > 0);
            if (row) {
                emptyRowChecks = 0;
                const startIdx = activeAdapter.getInitialColIndex
                    ? activeAdapter.getInitialColIndex(row)
                    : 0;
                focusElement(row.items[startIdx], row.id, startIdx);
                return currentEl;
            }
        }

        emptyRowChecks++;
        if (emptyRowChecks >= 3) {
            const sidebarItems = activeAdapter.getSidebarItems ? activeAdapter.getSidebarItems(window.__tvKioskNavUtils) : null;
            if (sidebarItems && sidebarItems.length > 0) {
                inSidebar = true;
                focusElement(sidebarItems[0], 'sidebar', 0, 'nearest');
                return currentEl;
            }
        }

        return null;
    };

    const startTracking = () => {
        if (rafId) return;
        const tick = (now) => {
            if (location.href !== lastUrl) {
                lastUrl = location.href;
                currentEl = null;
                currentRowId = null;
                preferredColIndex = 0;
                inSidebar = false;
                emptyRowChecks = 0;
            }
            if (now - lastFocusCheck > 180) {
                lastFocusCheck = now;
                const openListboxItems = activeAdapter?.getOpenListboxItems
                    ? activeAdapter.getOpenListboxItems(window.__tvKioskNavUtils)
                    : null;
                if (openListboxItems && openListboxItems.length > 0) {
                    if (!openListboxItems.includes(currentEl)) {
                        focusElement(openListboxItems[0], 'open-listbox', 0, 'nearest');
                    }
                } else if (currentRowId === 'open-listbox') {
                    currentEl = null;
                    currentRowId = null;
                    ensureInitialFocus();
                } else if (!currentEl || !isRendered(currentEl)) {
                    ensureInitialFocus();
                }
            }
            updateRingPosition();
            rafId = requestAnimationFrame(tick);
        };
        rafId = requestAnimationFrame(tick);
    };

    const pickBestColInRow = (items, referenceX, fallbackCol) => {
        if (!items || items.length === 0) return 0;
        if (typeof referenceX === 'number') {
            let bestIdx = 0;
            let bestDist = Infinity;
            for (let i = 0; i < items.length; i++) {
                const r = items[i].getBoundingClientRect();
                if (r.right < 0 || r.left > window.innerWidth) continue;
                const dist = Math.abs(r.left - referenceX);
                if (dist < bestDist) {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            if (bestDist < Infinity) {
                return bestIdx;
            }
        }
        return Math.min(Math.max(0, fallbackCol), items.length - 1);
    };

    const hydrateOrMoveToRow = (rows, startRowIndex, directionDelta) => {
        const step = directionDelta >= 0 ? 1 : -1;
        const refX = currentEl ? currentEl.getBoundingClientRect().left : null;

        const tryIndex = (i) => {
            if (i < 0 || i >= rows.length) return;
            const targetRow = rows[i];

            if (targetRow.items.length > 0) {
                const col = pickBestColInRow(targetRow.items, refX, preferredColIndex);
                focusElement(targetRow.items[col], targetRow.id, col);
                return;
            }

            if (targetRow.sectionEl && activeAdapter?.queryRowItems) {
                targetRow.sectionEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
                let attempts = 0;
                const poll = () => {
                    attempts++;
                    const tiles = activeAdapter.queryRowItems(targetRow.sectionEl, window.__tvKioskNavUtils);
                    if (tiles.length > 0) {
                        const col = pickBestColInRow(tiles, refX, preferredColIndex);
                        focusElement(tiles[col], targetRow.id, col);
                    } else if (attempts < 10) {
                        setTimeout(poll, 55);
                    } else {
                        tryIndex(i + step);
                    }
                };
                setTimeout(poll, 60);
            } else {
                tryIndex(i + step);
            }
        };

        tryIndex(startRowIndex);
    };

    const handleDrawerNavigation = (drawerItems, key) => {
        let idx = drawerItems.indexOf(currentEl);
        if (idx === -1) {
            focusElement(drawerItems[1] || drawerItems[0], 'slide-out-drawer', 0, 'nearest');
            return;
        }

        if (key === 'ArrowDown') {
            focusElement(drawerItems[Math.min(drawerItems.length - 1, idx + 1)], 'slide-out-drawer', 0, 'nearest');
        } else if (key === 'ArrowUp') {
            focusElement(drawerItems[Math.max(0, idx - 1)], 'slide-out-drawer', 0, 'nearest');
        } else if (key === 'ArrowRight' || key === 'Escape') {
            activateElement(drawerItems[0]);
            setTimeout(() => {
                currentEl = null;
                ensureInitialFocus();
            }, 200);
        } else if (key === 'Enter') {
            activateElement(currentEl);
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
            activateElement(currentEl);
            setTimeout(() => {
                if (isPlayerActive()) return;
                const stillOpen = activeAdapter.getOpenListboxItems
                    ? activeAdapter.getOpenListboxItems(window.__tvKioskNavUtils)
                    : null;
                if (stillOpen && stillOpen.length > 0) return;

                if (activeAdapter.onListboxItemSelected) {
                    activeAdapter.onListboxItemSelected(focusElement, window.__tvKioskNavUtils);
                } else {
                    const rows = activeAdapter.buildSemanticRows(window.__tvKioskNavUtils);
                    const epRail = rows.find(r => r.type === 'rail' && r.items.length > 0);
                    if (epRail) {
                        focusElement(epRail.items[0], epRail.id, 0);
                    } else {
                        currentEl = null;
                        ensureInitialFocus();
                    }
                }
            }, 300);
        } else if (key === 'Escape') {
            if (activeAdapter?.closeListbox) {
                activeAdapter.closeListbox(focusElement, activateElement);
            }
        }
    };

    const handleSidebarNavigation = (sidebarItems, key) => {
        let idx = sidebarItems.indexOf(currentEl);
        if (idx === -1) {
            idx = 0;
        }

        if (key === 'ArrowDown') {
            const nextIdx = Math.min(sidebarItems.length - 1, idx + 1);
            focusElement(sidebarItems[nextIdx], 'sidebar', 0, 'nearest');
        } else if (key === 'ArrowUp') {
            const prevIdx = Math.max(0, idx - 1);
            focusElement(sidebarItems[prevIdx], 'sidebar', 0, 'nearest');
        } else if (key === 'ArrowRight') {
            inSidebar = false;
            if (lastMainEl && isRendered(lastMainEl)) {
                focusElement(lastMainEl, lastMainRowId || 'row-0', lastMainColIndex);
            } else {
                currentEl = null;
                ensureInitialFocus();
            }
        } else if (key === 'Enter') {
            activateElement(currentEl);
            inSidebar = false;
            setTimeout(() => {
                currentEl = null;
                lastMainEl = null;
                ensureInitialFocus();
            }, 350);
        }
    };

    window.__tvKioskTryHandleBack = () => {
        if (!activeAdapter) return false;

        const drawerItems = activeAdapter.getSlideOutMenuItems
            ? activeAdapter.getSlideOutMenuItems(window.__tvKioskNavUtils)
            : null;
        if (drawerItems && drawerItems.length > 0) {
            activateElement(drawerItems[0]);
            setTimeout(() => {
                currentEl = null;
                ensureInitialFocus();
            }, 200);
            return true;
        }

        const listboxItems = activeAdapter.getOpenListboxItems
            ? activeAdapter.getOpenListboxItems(window.__tvKioskNavUtils)
            : null;
        if (listboxItems && listboxItems.length > 0 && activeAdapter.closeListbox) {
            activeAdapter.closeListbox(focusElement, activateElement);
            return true;
        }

        if (isPlayerActive() && activeAdapter.closeVideoPlayer) {
            return activeAdapter.closeVideoPlayer(focusElement, activateElement) === true;
        }

        return false;
    };

    window.addEventListener('keydown', (e) => {
        if (!activeAdapter) return;

        if (isPlayerActive()) {
            if (e.key === 'Escape' && activeAdapter.closeVideoPlayer) {
                if (activeAdapter.closeVideoPlayer(focusElement, activateElement)) {
                    e.preventDefault();
                    e.stopPropagation();
                }
            }
            return;
        }

        const key = e.key;
        if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Enter', 'Escape'].includes(key)) {
            return;
        }

        const drawerItems = activeAdapter.getSlideOutMenuItems
            ? activeAdapter.getSlideOutMenuItems(window.__tvKioskNavUtils)
            : null;
        if (drawerItems) {
            e.preventDefault();
            e.stopPropagation();
            handleDrawerNavigation(drawerItems, key);
            return;
        }

        const listboxItems = activeAdapter.getOpenListboxItems
            ? activeAdapter.getOpenListboxItems(window.__tvKioskNavUtils)
            : null;
        if (listboxItems) {
            e.preventDefault();
            e.stopPropagation();
            handleListboxNavigation(listboxItems, key);
            return;
        }

        const sidebarItems = activeAdapter.getSidebarItems
            ? activeAdapter.getSidebarItems(window.__tvKioskNavUtils)
            : null;
        if (inSidebar && sidebarItems && sidebarItems.length > 0) {
            e.preventDefault();
            e.stopPropagation();
            handleSidebarNavigation(sidebarItems, key);
            return;
        }

        if (document.activeElement && document.activeElement.tagName === 'INPUT') {
            if (key === 'ArrowLeft' || key === 'ArrowRight' || key === 'Enter') {
                return;
            }
        }

        if (key === 'Escape') {
            return;
        }

        const rows = activeAdapter.buildSemanticRows(window.__tvKioskNavUtils);
        if (rows.length === 0) {
            if (sidebarItems && sidebarItems.length > 0) {
                e.preventDefault();
                e.stopPropagation();
                inSidebar = true;
                focusElement(sidebarItems[0], 'sidebar', 0, 'nearest');
            }
            return;
        }

        if (!currentEl || !isRendered(currentEl)) {
            e.preventDefault();
            e.stopPropagation();
            ensureInitialFocus();
            return;
        }

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
            activateElement(currentEl);

            if (activeAdapter.onEnterPressed) {
                activeAdapter.onEnterPressed(currentEl, currentRow, focusElement, window.__tvKioskNavUtils);
            }
            setTimeout(() => {
                const openedListbox = activeAdapter.getOpenListboxItems
                    ? activeAdapter.getOpenListboxItems(window.__tvKioskNavUtils)
                    : null;
                if (openedListbox && openedListbox.length > 0) {
                    if (!openedListbox.includes(currentEl)) {
                        focusElement(openedListbox[0], 'open-listbox', 0, 'nearest');
                    }
                    return;
                }
                if (!currentEl || !isRendered(currentEl)) {
                    currentEl = null;
                    ensureInitialFocus();
                }
            }, 320);
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
            } else if (sidebarItems && sidebarItems.length > 0) {
                inSidebar = true;
                const activeSidebarItem = activeAdapter.getActiveSidebarItem
                    ? activeAdapter.getActiveSidebarItem(sidebarItems)
                    : sidebarItems[0];
                focusElement(activeSidebarItem || sidebarItems[0], 'sidebar', 0, 'nearest');
            }
            return;
        }

        if (key === 'ArrowDown') {
            if (rowIdx + 1 < rows.length) {
                hydrateOrMoveToRow(rows, rowIdx + 1, 1);
            } else if (currentRow.type === 'grid-row' || currentRow.type === 'rail') {
                const refX = currentEl ? currentEl.getBoundingClientRect().left : null;
                const scrolled = activeAdapter.scrollVirtualGrid
                    ? activeAdapter.scrollVirtualGrid(1)
                    : (window.scrollBy({ top: 450, behavior: 'smooth' }), true);
                if (scrolled) {
                    setTimeout(() => {
                        const updatedRows = activeAdapter.buildSemanticRows(window.__tvKioskNavUtils);
                        const newIdx = updatedRows.findIndex(r => r.items.includes(currentEl));
                        const targetIdx = newIdx !== -1 && newIdx + 1 < updatedRows.length
                            ? newIdx + 1
                            : updatedRows.length - 1;
                        if (targetIdx >= 0 && updatedRows[targetIdx]) {
                            const nextRow = updatedRows[targetIdx];
                            const nextCol = pickBestColInRow(nextRow.items, refX, colIdx);
                            focusElement(nextRow.items[nextCol], nextRow.id, nextCol);
                        }
                    }, 220);
                }
            }
            return;
        }

        if (key === 'ArrowUp') {
            const isTopGridRow = currentRow.type === 'grid-row' &&
                (rowIdx === 0 || rows[rowIdx - 1].type !== 'grid-row');

            if (isTopGridRow && activeAdapter.scrollVirtualGrid && activeAdapter.scrollVirtualGrid(-1)) {
                const refX = currentEl ? currentEl.getBoundingClientRect().left : null;
                setTimeout(() => {
                    const updatedRows = activeAdapter.buildSemanticRows(window.__tvKioskNavUtils);
                    const gridIdx = updatedRows.findIndex(r => r.type === 'grid-row');
                    if (gridIdx !== -1) {
                        const topGridRow = updatedRows[gridIdx];
                        const nextCol = pickBestColInRow(topGridRow.items, refX, colIdx);
                        focusElement(topGridRow.items[nextCol], topGridRow.id, nextCol);
                    }
                }, 220);
                return;
            }

            if (rowIdx - 1 >= 0) {
                hydrateOrMoveToRow(rows, rowIdx - 1, -1);
            }
            return;
        }
    }, true);

    window.__tvKioskMountSemanticNavigator = (adapter) => {
        activeAdapter = adapter;
        window.__TV_KIOSK_NAVIGATOR_MODE__ = adapter.name;
        startTracking();
        ensureInitialFocus();
    };
})();

