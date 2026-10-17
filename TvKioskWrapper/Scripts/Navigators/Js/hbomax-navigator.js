(function() {
    if (!window.__tvKioskMountSemanticNavigator) return;

    window.__tvKioskMountSemanticNavigator({
        name: 'hbomax',
        preferredInitialRowTypes: ['search-input', 'detail-actions', 'hero', 'rail', 'grid-row', 'top-nav'],

        isVideoPlayerActive: () => location.pathname.startsWith('/video/watch'),

        getVisualRect: (el) => {
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
            return null;
        },

        getInitialColIndex: (row) => {
            if (row.type === 'hero') {
                const playIdx = row.items.findIndex(i => i.getAttribute('data-testid') === 'play_button');
                return playIdx !== -1 ? playIdx : 0;
            }
            return 0;
        },

        getSlideOutMenuItems: ({ isRendered }) => {
            const closeBtn = document.querySelector('button[data-testid="closeMenuButton"]');
            if (!closeBtn || !isRendered(closeBtn)) return null;

            const links = Array.from(document.querySelectorAll('a[href]')).filter(a => {
                if (!isRendered(a)) return false;
                const r = a.getBoundingClientRect();
                return r.left >= 0 && r.left < 360 && r.top >= 70 && a.getAttribute('href') !== '#';
            });

            links.sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top);
            return [closeBtn, ...links];
        },

        getOpenListboxItems: ({ isRendered }) => {
            const listbox = document.querySelector('[role="listbox"]');
            if (!listbox || !isRendered(listbox)) return null;
            const options = Array.from(listbox.querySelectorAll('[role="option"], li, button, a')).filter(isRendered);
            return options.length > 0 ? options : null;
        },

        closeListbox: (focusElement) => {
            const seasonBtn = document.querySelector('button[class*="StyledSelectSort"]');
            seasonBtn?.click();
            if (seasonBtn) focusElement(seasonBtn, 'season-selector', 0);
        },

        queryRowItems: (sectionEl, { isRendered }) => {
            return Array.from(sectionEl.querySelectorAll('a[data-testid$="_tile"]')).filter(isRendered);
        },

        onEnterPressed: function(currentEl, currentRow, focusElement, utils) {
            const testId = currentEl.getAttribute('data-testid');
            if (testId === 'slide_out_nav_menuButton') {
                setTimeout(() => {
                    const items = this.getSlideOutMenuItems(utils);
                    if (items && items.length > 1) {
                        focusElement(items[1], 'slide-out-drawer', 0, 'nearest');
                    }
                }, 220);
            } else if (currentRow.type === 'season-select') {
                setTimeout(() => {
                    const opts = this.getOpenListboxItems(utils);
                    if (opts && opts.length > 0) {
                        focusElement(opts[0], 'open-listbox', 0, 'nearest');
                    }
                }, 200);
            }
        },

        buildSemanticRows: ({ isRendered, clusterIntoGridRows }) => {
            const rows = [];

            // 1. Top Navigation Bar
            const topNavItems = [];
            const menuBtn = document.querySelector('button[data-testid="slide_out_nav_menuButton"]');
            if (isRendered(menuBtn)) topNavItems.push(menuBtn);

            const homeLogo = document.querySelector('header a[href="/home"], a[aria-label*="Home" i][href="/home"]');
            if (isRendered(homeLogo) && !topNavItems.includes(homeLogo)) topNavItems.push(homeLogo);

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
                rows.push({ id: 'top-nav', type: 'top-nav', items: topNavItems });
            }

            // 2. Search Bar Input (/search)
            const searchInput = document.querySelector('input[data-testid="searchBar_field"]');
            if (isRendered(searchInput)) {
                rows.push({ id: 'search-input', type: 'search-input', items: [searchInput] });
            }

            // 3. Search Suggestion Chips (/search)
            const chips = Array.from(document.querySelectorAll('button[data-testid$="_Suggestion"]')).filter(isRendered);
            if (chips.length > 0) {
                chips.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                rows.push({ id: 'search-chips', type: 'chips', items: chips });
            }

            // 4. Immersive Hero Banner (Home / Series / Movies)
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
                    rows.push({ id: 'hero', type: 'hero', sectionEl: heroSection, items: heroItems });
                }
            }

            // 5. Show / Movie Detail Page Hero Controls
            if (!heroSection) {
                const detailPlayBtns = Array.from(document.querySelectorAll('button[data-testid="play_button"]')).filter(isRendered);
                const myListBtn = document.querySelector('button[data-testid="myList_button"]');
                const rateBtn = document.querySelector('button[data-testid="user_action_toggle_button"]');

                if (detailPlayBtns.length > 0) {
                    rows.push({ id: 'detail-primary', type: 'detail-actions', items: [detailPlayBtns[0]] });

                    const secondaryItems = [];
                    if (isRendered(myListBtn)) secondaryItems.push(myListBtn);
                    if (isRendered(rateBtn)) secondaryItems.push(rateBtn);
                    for (let i = 1; i < detailPlayBtns.length; i++) {
                        secondaryItems.push(detailPlayBtns[i]);
                    }
                    if (secondaryItems.length > 0) {
                        rows.push({ id: 'detail-secondary', type: 'detail-actions', items: secondaryItems });
                    }
                }
            }

            // 6. Page Tabs (Episodes / Related / Extras, or My List / Continue Watching)
            const tabs = Array.from(document.querySelectorAll('button[role="tab"][data-testid$="_Tab"]')).filter(isRendered);
            if (tabs.length > 0) {
                tabs.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                rows.push({ id: 'page-tabs', type: 'tabs', items: tabs });
            }

            // 7. Season Selector Dropdown Button
            const seasonBtn = Array.from(document.querySelectorAll('button[class*="StyledSelectSort"], button[id^="dropdown-"][id$="-toggle"]'))
                .find(b => isRendered(b) && !b.hasAttribute('data-testid'));
            if (seasonBtn) {
                rows.push({ id: 'season-selector', type: 'season-select', items: [seasonBtn] });
            }

            // 8. Horizontal Content Rails (section[data-testid$="_rail"])
            const railSections = Array.from(document.querySelectorAll('section[data-testid$="_rail"]')).filter(isRendered);
            for (let i = 0; i < railSections.length; i++) {
                const sec = railSections[i];
                const railId = sec.getAttribute('data-testid') || `rail-${i}`;
                const tiles = Array.from(sec.querySelectorAll('a[data-testid$="_tile"]')).filter(isRendered);
                rows.push({ id: railId, type: 'rail', sectionEl: sec, items: tiles });
            }

            // 9. Standalone 2D Tile Grids (Search Results, My Stuff)
            const standaloneTiles = Array.from(document.querySelectorAll('a[data-testid$="_tile"]'))
                .filter(t => isRendered(t) && !t.closest('section[data-testid$="_rail"]'));
            if (standaloneTiles.length > 0) {
                rows.push(...clusterIntoGridRows(standaloneTiles, 55, 'grid-row'));
            }

            return rows;
        }
    });
})();

