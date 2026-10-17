(function () {
    // 1. Suppress scrollbars early on document creation
    const injectScrollbarStyle = () => {
        const styleId = '__tv_kiosk_scrollbar_style__';
        if (!document.getElementById(styleId) && (document.head || document.documentElement)) {
            const style = document.createElement('style');
            style.id = styleId;
            style.textContent = `
                ::-webkit-scrollbar { display: none !important; width: 0 !important; height: 0 !important; }
                html, body { scrollbar-width: none !important; -ms-overflow-style: none !important; }
            `;
            (document.head || document.documentElement).appendChild(style);
        }
    };
    injectScrollbarStyle();
    document.addEventListener('DOMContentLoaded', injectScrollbarStyle);

    // Global kiosk hotkey bridge (ensures F12 exit and F1 home work even when Chromium swallows F12/F1 accelerators)
    window.addEventListener('keydown', (e) => {
        if (e.key === 'F12') {
            e.preventDefault();
            e.stopPropagation();
            window.chrome?.webview?.postMessage({ type: 'hotkey', key: 'F12' });
        } else if (e.key === 'F1') {
            e.preventDefault();
            e.stopPropagation();
            window.chrome?.webview?.postMessage({ type: 'hotkey', key: 'F1' });
        }
    }, true);

    // 2. If running on YouTube TV (Cobalt / Smart TV User-Agent), spoof Cobalt environment:
    //    - Hide desktop Client Hints & unregister desktop service workers
    //    - Emulate Cobalt's MediaSource.isTypeSupported parameter validation (rejecting framerate=9999 probes so 4K60 & HDR unlock)
    //    - Expose window.h5vcc.system.getVideoContainerSizeOverride returning 7680x4320 so 250% Windows DPI scaling doesn't cap resolution
    if (navigator.userAgent.includes('Cobalt') || navigator.userAgent.includes('SMART-TV') || navigator.userAgent.includes('Tizen')) {
        try {
            Object.defineProperty(navigator, 'userAgentData', {
                get: () => undefined,
                configurable: true
            });
            Object.defineProperty(navigator, 'maxTouchPoints', {
                get: () => 0,
                configurable: true
            });
        } catch (_) {}

        // Unregister any cached desktop YouTube service workers that could hijack /tv navigation
        if (location.hostname.includes('youtube.com') && 'serviceWorker' in navigator) {
            navigator.serviceWorker.getRegistrations().then(regs => {
                for (const reg of regs) {
                    reg.unregister();
                }
            }).catch(() => {});
        }

        // Expose Cobalt h5vcc interface to unlock full 4K/8K resolution regardless of OS DPI scaling (e.g. 250% -> 1536x864 CSS pixels)
        try {
            window.h5vcc = window.h5vcc || {};
            window.h5vcc.system = window.h5vcc.system || {};
            window.h5vcc.system.getVideoContainerSizeOverride = () => '7680x4320';
        } catch (_) {}

        // Make MediaSource.isTypeSupported validate Cobalt MIME parameters (width, height, framerate, bitrate, eotf).
        // YouTube TV probes absurd values like framerate=9999 expecting false; Chromium normally ignores them and returns true,
        // which causes YouTube TV to distrust the browser and cap streams at 720p30 SDR.
        if (typeof MediaSource !== 'undefined' && typeof MediaSource.isTypeSupported === 'function') {
            const nativeIsTypeSupported = MediaSource.isTypeSupported.bind(MediaSource);
            const isHdrDisplay = () => window.matchMedia('(dynamic-range: high)').matches;
            const isHdrCodec = (codec) => {
                const parts = codec.split('.');
                const transfer = parts[0] === 'vp09' ? parts[6] : (parts[0] === 'av01' ? parts[7] : undefined);
                return transfer === '16' || transfer === '18';
            };
            const paramChecks = {
                width: (v) => Number(v) > 0 && Number(v) <= 7680,
                height: (v) => Number(v) > 0 && Number(v) <= 4320,
                framerate: (v) => Number(v) > 0 && Number(v) <= 60,
                bitrate: (v) => Number(v) > 0 && Number(v) <= 200000000,
                eotf: (v) => v === 'bt709' || ((v === 'smpte2084' || v === 'arib-std-b67') && isHdrDisplay()),
                channels: (v) => Number(v) > 0 && Number(v) <= 8,
                cryptoblockformat: (v) => v === 'subsample',
                'decode-to-texture': (v) => v === 'true' || v === 'false'
            };

            MediaSource.isTypeSupported = function (type) {
                const [mimeType, ...params] = String(type).split(';').map(p => p.trim());
                const kept = [mimeType];

                for (const param of params) {
                    const [rawKey, ...rest] = param.split('=');
                    const key = rawKey.trim().toLowerCase();
                    const value = rest.join('=').trim().replace(/^"|"$/g, '');

                    if (key === 'codecs') {
                        if (value.split(',').some(c => isHdrCodec(c.trim())) && !isHdrDisplay()) {
                            return false;
                        }
                        kept.push(param);
                    } else if (paramChecks[key]) {
                        if (!paramChecks[key](value)) {
                            return false;
                        }
                    } else {
                        kept.push(param);
                    }
                }

                return nativeIsTypeSupported(kept.join('; '));
            };
        }
    }
})();

