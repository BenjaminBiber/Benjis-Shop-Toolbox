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

window.toolboxGetElementCenter = function (elementId) {
    try {
        var el = document.getElementById(elementId);
        if (!el) return null;
        var r = el.getBoundingClientRect();
        return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    } catch (e) {
        return null;
    }
};

