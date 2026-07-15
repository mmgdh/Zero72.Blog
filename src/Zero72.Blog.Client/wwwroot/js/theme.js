window.zero72Theme = {
    accents: ["pink", "green", "blue", "lavender", "yellow"],
    apply: (theme) => {
        const nextTheme = theme === "light" ? "light" : "dark";
        const root = document.documentElement;

        root.classList.toggle("is-light", nextTheme === "light");
        root.classList.toggle("is-dark", nextTheme === "dark");
        root.style.colorScheme = nextTheme;
        window.localStorage.setItem("zero72-theme", nextTheme);
    },
    applyColor: (accent) => {
        const nextAccent = window.zero72Theme.accents.includes(accent) ? accent : "pink";
        document.documentElement.dataset.accent = nextAccent;
        window.localStorage.setItem("zero72-accent", nextAccent);
    },
    toggle: () => {
        const root = document.documentElement;
        const nextTheme = root.classList.contains("is-light") ? "dark" : "light";
        window.zero72Theme.apply(nextTheme);
    },
    toggleColor: () => {
        const root = document.documentElement;
        const currentAccent = root.dataset.accent || "pink";
        const currentIndex = window.zero72Theme.accents.indexOf(currentAccent);
        const nextAccent = window.zero72Theme.accents[(currentIndex + 1) % window.zero72Theme.accents.length];
        window.zero72Theme.applyColor(nextAccent);
    },
    initialize: () => {
        window.zero72Theme.apply(window.localStorage.getItem("zero72-theme") || "dark");
        window.zero72Theme.applyColor(window.localStorage.getItem("zero72-accent") || "pink");
    }
};

window.zero72Theme.initialize();
