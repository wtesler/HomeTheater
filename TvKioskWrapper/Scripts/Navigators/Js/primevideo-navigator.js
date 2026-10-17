(function () {
    if (!window.__tvKioskMountSemanticNavigator) return;

    if (!document.getElementById('__tv_kiosk_primevideo_styles__')) {
        const style = document.createElement('style');
        style.id = '__tv_kiosk_primevideo_styles__';
        style.textContent = '[data-testid="episodes-playbutton"] { opacity: 1 !important; }';
        (document.head || document.documentElement).appendChild(style);
    }

    const CAROUSEL_SELECTOR = [
        'section[data-testid="standard-carousel"]',
        'section[data-testid="super-carousel"]',
        'section[data-testid="beard-supported-carousel"]'
    ].join(', ');

    const CARD_SELECTOR = 'article[data-testid="card"], article[data-testid="super-carousel-card"]';

    const extractCardTarget = (card, isRendered) => {
        if (!card || !isRendered(card)) return null;
        const posterLink = card.querySelector('a[data-testid="poster-link"]');
        if (posterLink && isRendered(posterLink)) return posterLink;
        const detailLink = Array.from(card.querySelectorAll('a[href*="/detail/"], a[href]')).find(isRendered);
        if (detailLink) return detailLink;
        const btn = Array.from(card.querySelectorAll('button[data-testid="poster-focus-button"], button')).find(isRendered);
        return btn || null;
    };

    const queryCarouselItems = (sectionEl, isRendered) => {
        if (!sectionEl) return [];
        const cards = Array.from(sectionEl.querySelectorAll(CARD_SELECTOR));
        const items = [];
        for (const card of cards) {
            const target = extractCardTarget(card, isRendered);
            if (target && !items.includes(target)) {
                items.push(target);
            }
        }
        items.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
        return items;
    };

    window.__tvKioskMountSemanticNavigator({
        name: 'primevideo',
        preferredInitialRowTypes: [
            'search-input',
            'detail-actions',
            'hero',
            'grid-row',
            'rail',
            'tabs',
            'top-nav'
        ],

        isVideoPlayerActive: () => {
            const player = document.querySelector('#dv-web-player.dv-player-fullscreen, #dv-web-player .atvwebplayersdk-overlays-container');
            if (!player) return false;
            const r = player.getBoundingClientRect();
            return r.width > 400 && r.height > 300 && getComputedStyle(player).visibility !== 'hidden';
        },

        closeVideoPlayer: (_focusElement, activateElement) => {
            const closeBtn = document.querySelector(
                '#dv-web-player button[aria-label*="Close" i], ' +
                '#dv-web-player .atvwebplayersdk-closebutton-wrapper button, ' +
                '#dv-web-player button[class*="close" i]'
            );
            if (closeBtn) {
                activateElement(closeBtn);
                return true;
            }
            return false;
        },

        getVisualRect: (el) => {
            if (!el) return null;
            const episodeCard = el.closest('li[data-testid="episode-list-item"]');
            if (episodeCard) {
                const er = episodeCard.getBoundingClientRect();
                if (er.width > 20 && er.height > 20) {
                    return { left: er.left, top: er.top, width: er.width, height: er.height, badge: null };
                }
            }

            const card = el.closest(CARD_SELECTOR);
            if (card) {
                const cr = card.getBoundingClientRect();
                if (cr.width > 20 && cr.height > 20) {
                    return { left: cr.left, top: cr.top, width: cr.width, height: cr.height, badge: null };
                }
            }
            return null;
        },

        getInitialColIndex: (row) => {
            if (row.type === 'hero') {
                const playIdx = row.items.findIndex(i =>
                    i.getAttribute('data-testid') === 'play' ||
                    (i.getAttribute('aria-label') || '').toLowerCase().startsWith('more details')
                );
                return playIdx !== -1 ? playIdx : 0;
            }
            return 0;
        },

        getOpenListboxItems: ({ isRendered }) => {
            const seasonToggle = document.querySelector('input#av-droplist-av-atf-season-selector');
            if (seasonToggle && !seasonToggle.checked) return null;

            const seasonOptions = Array.from(
                document.querySelectorAll('[data-testid="dp-season-selector"] ul a[href], [data-testid="dp-season-selector"] ul button')
            ).filter(isRendered);

            return seasonOptions.length > 0 ? seasonOptions : null;
        },

        closeListbox: (focusElement, activateElement) => {
            const seasonLabel = document.querySelector('[data-testid="dp-season-selector"] label');
            if (seasonLabel) {
                activateElement(seasonLabel);
                focusElement(seasonLabel, 'season-selector', 0);
            }
        },

        onListboxItemSelected: (focusElement, utils) => {
            const rows = window.__tvKioskPrimeBuildRows ? window.__tvKioskPrimeBuildRows(utils) : [];
            const epRow = rows.find(r => r.id.startsWith('episode-row-') && r.items.length > 0);
            if (epRow) {
                focusElement(epRow.items[0], epRow.id, 0);
            } else {
                const seasonLabel = document.querySelector('[data-testid="dp-season-selector"] label');
                if (seasonLabel && utils.isRendered(seasonLabel)) {
                    focusElement(seasonLabel, 'season-selector', 0);
                }
            }
        },

        queryRowItems: (sectionEl, { isRendered }) => {
            return queryCarouselItems(sectionEl, isRendered);
        },

        onEnterPressed: function (currentEl, currentRow, focusElement, utils) {
            const testId = currentEl.getAttribute('data-testid') || '';

            if (testId === 'pv-nav-search-dropdown-trigger') {
                setTimeout(() => {
                    const searchInput = document.querySelector(
                        'input#pv-search-nav, [data-testid="pv-nav-search-dropdown-content"] input[type="search"]'
                    );
                    if (searchInput && utils.isRendered(searchInput)) {
                        focusElement(searchInput, 'search-input', 0, 'nearest');
                    }
                }, 180);
            } else if (currentRow.type === 'season-select') {
                setTimeout(() => {
                    const opts = this.getOpenListboxItems(utils);
                    if (opts && opts.length > 0) {
                        focusElement(opts[0], 'open-listbox', 0, 'nearest');
                    }
                }, 180);
            } else if (currentRow.type === 'hero' && (testId === 'left-arrow' || testId === 'right-arrow')) {
                setTimeout(() => {
                    if (utils.isRendered(currentEl)) {
                        focusElement(currentEl, 'hero', currentRow.items.indexOf(currentEl), 'nearest');
                    }
                }, 220);
            }
        },

        buildSemanticRows: function (utils) {
            const { isRendered, clusterIntoGridRows } = utils;
            const rows = [];

            // 1. Top Navigation Bar
            const topNavItems = [];
            const mainNav = document.querySelector('[data-testid="main-nav"]');
            if (mainNav && isRendered(mainNav)) {
                for (const el of mainNav.querySelectorAll('a[data-testid^="pv-nav-"], button[data-testid^="pv-nav-"]')) {
                    if (!isRendered(el)) continue;
                    if (el.closest('[data-testid*="dropdown-content"]')) continue;
                    if (!topNavItems.includes(el)) {
                        topNavItems.push(el);
                    }
                }
            }

            const rightNavSelectors = [
                'button[data-testid="pv-nav-search-dropdown-trigger"]',
                'button[data-testid="pv-nav-search-Clear"]',
                'a[data-testid="pv-nav-categories"]',
                'a[data-testid="pv-nav-my-stuff-dropdown-trigger"]',
                'button[data-testid="pv-nav-account-and-profiles-dropdown-trigger"]'
            ];
            for (const sel of rightNavSelectors) {
                const el = document.querySelector(sel);
                if (el && isRendered(el) && !el.closest('[data-testid*="dropdown-content"]') && !topNavItems.includes(el)) {
                    topNavItems.push(el);
                }
            }

            if (topNavItems.length > 0) {
                topNavItems.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                rows.push({ id: 'top-nav', type: 'top-nav', items: topNavItems });
            }

            // 2. Search Bar Input & Dropdown Suggestions
            const searchInput = document.querySelector(
                'input#pv-search-nav, [data-testid="pv-nav-search-dropdown-content"] input[type="search"]'
            );
            if (searchInput && isRendered(searchInput)) {
                rows.push({ id: 'search-input', type: 'search-input', items: [searchInput] });

                const suggestions = Array.from(
                    document.querySelectorAll('[data-testid="pv-nav-search-dropdown-content"] a[href]')
                ).filter(isRendered);
                if (suggestions.length > 0) {
                    rows.push(...clusterIntoGridRows(suggestions, 36, 'search-suggestion'));
                }
            }

            // 3. Hero Banner Carousel (Home / Movies / TV Shows)
            const heroCarousel = document.querySelector('ul[data-testid="fading-rotating-carousel"]');
            if (heroCarousel && isRendered(heroCarousel)) {
                const slides = Array.from(heroCarousel.children).filter(li => {
                    if (li.tagName !== 'LI') return false;
                    const s = getComputedStyle(li);
                    return s.display !== 'none' && s.visibility !== 'hidden' && parseFloat(s.opacity) > 0.5;
                });
                const activeSlide = slides[0] || heroCarousel.querySelector('li');
                const heroContainer = heroCarousel.parentElement || heroCarousel;

                const heroItems = [];
                const prevBtn = heroContainer.querySelector('button[data-testid="left-arrow"]');
                const nextBtn = heroContainer.querySelector('button[data-testid="right-arrow"]');

                if (prevBtn && isRendered(prevBtn)) heroItems.push(prevBtn);

                if (activeSlide) {
                    const actionControls = activeSlide.querySelectorAll(
                        '[data-testid="action-box"] a[href], ' +
                        '[data-testid="action-box"] button, ' +
                        'a[data-testid="play"], ' +
                        'a[aria-label^="More details" i]'
                    );
                    for (const ctrl of actionControls) {
                        if (isRendered(ctrl) && !heroItems.includes(ctrl)) {
                            heroItems.push(ctrl);
                        }
                    }
                }

                if (nextBtn && isRendered(nextBtn) && !heroItems.includes(nextBtn)) {
                    heroItems.push(nextBtn);
                }

                if (heroItems.length > 0) {
                    rows.push({ id: 'hero', type: 'hero', sectionEl: heroCarousel, items: heroItems });
                }
            }

            // 4. Detail Page Controls (/detail/...)
            if (!heroCarousel) {
                const seasonLabel = document.querySelector('[data-testid="dp-season-selector"] label');
                if (seasonLabel && isRendered(seasonLabel)) {
                    rows.push({ id: 'season-selector', type: 'season-select', items: [seasonLabel] });
                }

                const primaryPlayBtns = Array.from(
                    document.querySelectorAll(
                        'a[data-testid="dp-atf-play-button-spinner"], ' +
                        'button[data-testid="dp-atf-play-button-spinner"], ' +
                        '[data-automation-id="dp-atf-play-button-spinner"]'
                    )
                ).filter(isRendered);

                if (primaryPlayBtns.length > 0) {
                    rows.push({ id: 'detail-primary', type: 'detail-actions', items: primaryPlayBtns });
                }

                const secondaryCandidates = Array.from(
                    document.querySelectorAll(
                        'button[aria-label*="Watch trailer" i], ' +
                        'a[aria-label*="Watch trailer" i], ' +
                        'button[aria-label*="Watchlist" i], ' +
                        'button[aria-label="Like"], ' +
                        'button[aria-label="Not for me"], ' +
                        'button[aria-label="Share"], ' +
                        '[data-testid="action-box"] button, ' +
                        '[data-testid="action-box"] a[href]'
                    )
                ).filter(el => isRendered(el) && !primaryPlayBtns.includes(el));

                const secondaryBtns = [];
                for (const btn of secondaryCandidates) {
                    if (!secondaryBtns.includes(btn)) {
                        secondaryBtns.push(btn);
                    }
                }
                if (secondaryBtns.length > 0) {
                    secondaryBtns.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                    rows.push({ id: 'detail-secondary', type: 'detail-actions', items: secondaryBtns });
                }
            }

            // 5. Detail Page Tabs (Episodes / Related / Details)
            const tabs = Array.from(
                document.querySelectorAll('button[role="tab"][data-testid^="btf-"], [role="tablist"] button[role="tab"]')
            ).filter(isRendered);
            if (tabs.length > 0) {
                tabs.sort((a, b) => a.getBoundingClientRect().left - b.getBoundingClientRect().left);
                rows.push({ id: 'page-tabs', type: 'tabs', items: tabs });
            }

            // 6. Episode List 2D Grid (TV Show Detail Page)
            const episodeCards = Array.from(document.querySelectorAll('li[data-testid="episode-list-item"]')).filter(isRendered);
            if (episodeCards.length > 0) {
                const episodeTargets = [];
                for (const ep of episodeCards) {
                    const playBtn = ep.querySelector('[data-testid="episodes-playbutton"]');
                    const playTarget = (playBtn && isRendered(playBtn))
                        ? playBtn
                        : Array.from(ep.querySelectorAll('a[href], button')).find(isRendered);
                    if (playTarget) {
                        episodeTargets.push(playTarget);
                    }
                }
                if (episodeTargets.length > 0) {
                    rows.push(...clusterIntoGridRows(episodeTargets, 55, 'episode-row'));
                }
            }

            // 7. Standalone 2D Card Grids (Search Results, Category Pages, My Stuff)
            const standaloneCards = Array.from(document.querySelectorAll(CARD_SELECTOR))
                .filter(card => isRendered(card) && !card.closest(CAROUSEL_SELECTOR));
            if (standaloneCards.length > 0) {
                const gridTargets = [];
                for (const card of standaloneCards) {
                    const target = extractCardTarget(card, isRendered);
                    if (target && !gridTargets.includes(target)) {
                        gridTargets.push(target);
                    }
                }
                if (gridTargets.length > 0) {
                    rows.push(...clusterIntoGridRows(gridTargets, 55, 'grid-row'));
                }
            }

            // 8. Horizontal Content Carousels (Standard, Super, Beard-Supported)
            const carouselSections = Array.from(document.querySelectorAll(CAROUSEL_SELECTOR)).filter(isRendered);
            for (let i = 0; i < carouselSections.length; i++) {
                const sec = carouselSections[i];
                const items = queryCarouselItems(sec, isRendered);
                rows.push({
                    id: `rail-${i}`,
                    type: 'rail',
                    sectionEl: sec,
                    items
                });
            }

            window.__tvKioskPrimeBuildRows = () => this.buildSemanticRows(utils);
            return rows;
        }
    });
})();
