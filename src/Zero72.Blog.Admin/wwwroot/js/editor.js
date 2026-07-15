window.zero72AdminEditor = {
    insertAtCursor: (element, value) => {
        if (!element) {
            return value;
        }

        const start = element.selectionStart ?? element.value.length;
        const end = element.selectionEnd ?? element.value.length;
        const before = element.value.substring(0, start);
        const after = element.value.substring(end);
        const nextValue = `${before}${value}${after}`;

        element.value = nextValue;
        const nextCursor = start + value.length;
        element.focus();
        element.setSelectionRange(nextCursor, nextCursor);

        return nextValue;
    }
};
