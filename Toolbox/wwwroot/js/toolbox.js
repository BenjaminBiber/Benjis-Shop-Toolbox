window.toolboxScrollToBottom = function (elementClass) {
    try {
        var elements = document.getElementsByClassName(elementClass);
        if (!elements || elements.length === 0) return;
        var el = elements[0];
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    } catch (e) {
        // ignore
    }
};

