(function () {
    const appGrid = document.getElementById("appGrid");
    const ambientGlow = document.getElementById("ambientGlow");
    const clockDisplay = document.getElementById("clockDisplay");

    let cards = [];
    let selectedIndex = 0;

    function getConfiguredServices() {
        return Array.isArray(window.__KIOSK_SERVICES__) ? window.__KIOSK_SERVICES__ : [];
    }

    function updateClock() {
        const now = new Date();
        clockDisplay.textContent = now.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function hexToRgba(hex, alpha) {
        const clean = (hex || "#00E5FF").replace("#", "");
        if (clean.length !== 6) {
            return `rgba(0, 229, 255, ${alpha})`;
        }
        const r = parseInt(clean.substring(0, 2), 16);
        const g = parseInt(clean.substring(2, 4), 16);
        const b = parseInt(clean.substring(4, 6), 16);
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    function launchService(service) {
        if (!service || (!service.url && !service.executablePath)) return;

        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({
                type: "launchService",
                url: service.url || "",
                executablePath: service.executablePath || "",
                name: service.name
            });
        } else if (service.url) {
            window.location.href = service.url;
        }
    }

    function setFocusedCard(index) {
        if (cards.length === 0) return;
        selectedIndex = (index + cards.length) % cards.length;

        cards.forEach((card, i) => {
            if (i === selectedIndex) {
                card.classList.add("focused");
                card.focus({ preventScroll: true });
                const accent = card.dataset.accent || "#00E5FF";
                ambientGlow.style.background = `radial-gradient(circle at 50% 25%, ${hexToRgba(accent, 0.22)}, transparent 65%)`;
            } else {
                card.classList.remove("focused");
            }
        });
    }

    function moveByDirection(direction) {
        if (cards.length <= 1) return;

        const curRect = cards[selectedIndex].getBoundingClientRect();
        const cx = curRect.left + curRect.width / 2;
        const cy = curRect.top + curRect.height / 2;

        let bestIdx = -1;
        let bestScore = Infinity;

        for (let i = 0; i < cards.length; i++) {
            if (i === selectedIndex) continue;
            const r = cards[i].getBoundingClientRect();
            const tx = r.left + r.width / 2;
            const ty = r.top + r.height / 2;
            const dx = tx - cx;
            const dy = ty - cy;

            if (direction === "Right" && dx <= 10) continue;
            if (direction === "Left" && dx >= -10) continue;
            if (direction === "Down" && dy <= 10) continue;
            if (direction === "Up" && dy >= -10) continue;

            const primary = (direction === "Left" || direction === "Right") ? Math.abs(dx) : Math.abs(dy);
            const cross = (direction === "Left" || direction === "Right") ? Math.abs(dy) : Math.abs(dx);
            const score = primary + cross * 3.0;

            if (score < bestScore) {
                bestScore = score;
                bestIdx = i;
            }
        }

        if (bestIdx !== -1) {
            setFocusedCard(bestIdx);
        } else if (direction === "Right") {
            setFocusedCard(selectedIndex + 1);
        } else if (direction === "Left") {
            setFocusedCard(selectedIndex - 1);
        }
    }

    function renderServices(services) {
        const list = Array.isArray(services) ? services : [];
        appGrid.innerHTML = "";
        cards = [];

        list.forEach((service, idx) => {
            const card = document.createElement("div");
            card.className = "app-card";
            card.tabIndex = 0;
            card.setAttribute("role", "gridcell");
            card.dataset.accent = service.accentColor || "#00E5FF";
            card.style.setProperty("--card-accent", service.accentColor || "#00E5FF");
            card.style.setProperty("--card-glow", hexToRgba(service.accentColor || "#00E5FF", 0.45));

            let hostLabel = service.url || "";
            if (service.executablePath) {
                const parts = service.executablePath.split(/[\\/]/);
                hostLabel = parts[parts.length - 1] || "Desktop App";
            } else {
                try {
                    hostLabel = new URL(service.url).hostname.replace(/^www\./, "");
                } catch (_) {}
            }

            card.innerHTML = `
                <div class="app-card-body">
                    <div class="app-card-header">
                        <div class="app-card-accent"></div>
                    </div>
                    <div class="app-card-logo-container">
                        <img class="app-card-logo" alt="" />
                    </div>
                    <div class="app-card-title"></div>
                    <!-- <div class="app-card-subtitle"></div> -->
                </div>
                <div class="app-card-footer">
                    <span class="app-card-host"></span>
                    <span class="app-card-action">Press Enter <span class="app-card-action-arrow">➔</span></span>
                </div>
            `;

            const logoImg = card.querySelector(".app-card-logo");
            const logoContainer = card.querySelector(".app-card-logo-container");
            if (service.logo) {
                logoImg.src = service.logo;
                logoImg.alt = `${service.name || "App"} logo`;
                logoImg.onerror = () => {
                    logoContainer.style.display = "none";
                };
            } else {
                logoContainer.style.display = "none";
            }

            card.querySelector(".app-card-title").textContent = service.name || "App";
            // card.querySelector(".app-card-subtitle").textContent = service.subtitle || "";
            card.querySelector(".app-card-host").textContent = hostLabel;

            card.addEventListener("click", () => {
                setFocusedCard(idx);
                launchService(service);
            });

            card.addEventListener("mouseenter", () => {
                setFocusedCard(idx);
            });

            card.addEventListener("focus", () => {
                if (selectedIndex !== idx) {
                    setFocusedCard(idx);
                }
            });

            appGrid.appendChild(card);
            cards.push(card);
        });

        setFocusedCard(0);
    }

    window.addEventListener("focus", () => {
        if (cards.length > 0) {
            setFocusedCard(selectedIndex);
        }
    });

    window.addEventListener("keydown", (e) => {
        if (e.key === "ArrowRight" || e.key === "ArrowLeft" || e.key === "ArrowUp" || e.key === "ArrowDown") {
            e.preventDefault();
            e.stopPropagation();
            moveByDirection(e.key.replace("Arrow", ""));
            return;
        }

        if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            e.stopPropagation();
            const list = getConfiguredServices();
            if (list[selectedIndex]) {
                launchService(list[selectedIndex]);
            }
        }
    });

    window.__setKioskServices = function (services) {
        window.__KIOSK_SERVICES__ = services;
        renderServices(services);
    };

    updateClock();
    setInterval(updateClock, 10000);
    renderServices(getConfiguredServices());
})();
