(() => {
    const root = document.documentElement;
    const storageKey = "zero72-theme";

    /**
     * 校验主题值，避免 Blazor 增强导航或旧缓存传入空值时误切换为深色。
     * @param {string|null} theme 待校验的主题值。
     * @returns {"light"|"dark"} 可安全应用的主题。
     */
    function normalizeTheme(theme) {
        return theme === "dark" ? "dark" : "light";
    }

    /** 在浏览器禁止本地存储时安全读取主题。 */
    function readStoredTheme() {
        try {
            return localStorage.getItem(storageKey);
        } catch {
            return null;
        }
    }

    /**
     * 将主题应用到根节点；只有用户主动切换时才写入本地缓存。
     * @param {string|null} theme 目标主题。
     * @param {boolean} persist 是否持久化。
     */
    function applyTheme(theme, persist) {
        const normalizedTheme = normalizeTheme(theme);
        const isDark = normalizedTheme === "dark";

        root.classList.toggle("is-dark", isDark);
        root.classList.toggle("is-light", !isDark);
        root.dataset.theme = normalizedTheme;
        root.style.colorScheme = normalizedTheme;

        if (persist) {
            try {
                localStorage.setItem(storageKey, normalizedTheme);
            } catch {
                // 隐私模式可能禁用本地存储，但当前页面仍应正常切换主题。
            }
        }
    }

    /** 在首次加载和增强导航结束后恢复用户已经选择的主题。 */
    function initializeTheme() {
        applyTheme(readStoredTheme(), false);
    }

    window.zero72Theme = {
        apply: theme => applyTheme(theme, true),
        initialize: initializeTheme,
        toggle: () => {
            const nextTheme = root.dataset.theme === "dark" ? "light" : "dark";
            applyTheme(nextTheme, true);
        }
    };

    initializeTheme();
    document.addEventListener("enhancedload", initializeTheme);
    window.addEventListener("pageshow", initializeTheme);
    window.addEventListener("storage", event => {
        if (event.key === storageKey) {
            applyTheme(event.newValue, false);
        }
    });
})();
