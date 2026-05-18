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

window.toolboxTutorialGetRect = function (selector) {
    try {
        var el = document.querySelector(selector);
        if (!el) return null;
        var r = el.getBoundingClientRect();
        var vw = window.innerWidth;
        var vh = window.innerHeight;
        var left = Math.max(0, Math.floor(r.left));
        var top = Math.max(0, Math.floor(r.top));
        var right = Math.min(vw, Math.ceil(r.right));
        var bottom = Math.min(vh, Math.ceil(r.bottom));
        return {
            top: top,
            left: left,
            width: Math.max(0, right - left),
            height: Math.max(0, bottom - top),
            right: right,
            bottom: bottom,
            viewportWidth: vw,
            viewportHeight: vh
        };
    } catch (e) {
        return null;
    }
};

window.toolboxTutorialRegisterClick = function (selector, dotNetRef) {
    try {
        var el = document.querySelector(selector);
        if (!el) return false;

        if (el.__toolboxTutorialHandler) {
            el.removeEventListener("click", el.__toolboxTutorialHandler);
        }

        el.__toolboxTutorialRegisteredAt = Date.now();
        el.__toolboxTutorialHandler = function (ev) {
            try {
                if (ev && ev.isTrusted === false) return;
                var elapsed = Date.now() - (el.__toolboxTutorialRegisteredAt || 0);
                if (elapsed < 200) return;
                dotNetRef.invokeMethodAsync("OnTutorialTargetClicked");
            } catch (e) {
                // ignore
            }
        };

        el.addEventListener("click", el.__toolboxTutorialHandler, { once: true });
        return true;
    } catch (e) {
        return false;
    }
};

window.toolboxTutorialScrollIntoView = function (selector) {
    try {
        var el = document.querySelector(selector);
        if (!el) return;
        el.scrollIntoView({ behavior: "smooth", block: "center", inline: "center" });
    } catch (e) {
        // ignore
    }
};

window.toolboxTutorialSetHighlightTarget = function (selector) {
    try {
        var prev = window.__toolboxTutorialTarget;
        if (prev && prev.classList) {
            if (prev.__toolboxTutorialPrevPosition !== undefined) {
                prev.style.position = prev.__toolboxTutorialPrevPosition;
            }
            if (prev.__toolboxTutorialPrevZIndex !== undefined) {
                prev.style.zIndex = prev.__toolboxTutorialPrevZIndex;
            }
            if (prev.__toolboxTutorialPrevPointerEvents !== undefined) {
                prev.style.pointerEvents = prev.__toolboxTutorialPrevPointerEvents;
            }
            prev.classList.remove("tutorial-highlight-target");
        }

        var el = selector ? document.querySelector(selector) : null;
        if (el && el.classList) {
            el.__toolboxTutorialPrevPosition = el.style.position;
            el.__toolboxTutorialPrevZIndex = el.style.zIndex;
            el.__toolboxTutorialPrevPointerEvents = el.style.pointerEvents;

            var computed = window.getComputedStyle(el);
            if (!computed || computed.position === "static" || !computed.position) {
                el.style.position = "relative";
            }
            el.style.zIndex = "1402";
            el.style.pointerEvents = "auto";
            el.classList.add("tutorial-highlight-target");
        }

        window.__toolboxTutorialTarget = el;
        return !!el;
    } catch (e) {
        return false;
    }
};

window.toolboxTutorialClearHighlightTarget = function () {
    try {
        var prev = window.__toolboxTutorialTarget;
        if (prev && prev.classList) {
            if (prev.__toolboxTutorialPrevPosition !== undefined) {
                prev.style.position = prev.__toolboxTutorialPrevPosition;
            }
            if (prev.__toolboxTutorialPrevZIndex !== undefined) {
                prev.style.zIndex = prev.__toolboxTutorialPrevZIndex;
            }
            if (prev.__toolboxTutorialPrevPointerEvents !== undefined) {
                prev.style.pointerEvents = prev.__toolboxTutorialPrevPointerEvents;
            }
            prev.classList.remove("tutorial-highlight-target");
        }
        window.__toolboxTutorialTarget = null;
    } catch (e) {
        // ignore
    }
};
