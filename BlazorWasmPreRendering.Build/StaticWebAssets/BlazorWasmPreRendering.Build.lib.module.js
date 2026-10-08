const BEGIN_MARKER = "%%-PRERENDERING-HEADOUTLET-BEGIN-%%";
const END_MARKER = "%%-PRERENDERING-HEADOUTLET-END-%%";
const STASHED_TITLE_MARKER = /^%%-PRERENDERING-DEFAULT-TITLE-%%(?:\s+(\S*))?$/;

const markerOf = node => node.nodeType === Node.COMMENT_NODE ? node.textContent.trim() : "";
const urlOf = href => href.split("#")[0];
const isStylesheet = element => element.tagName === "LINK" && element.relList.contains("stylesheet");

// Elements with the same key are counterparts of each other, even if their contents differ.
const keyOf = element => {
    const { tagName } = element;
    const name = tagName === "META" && ["name", "property", "http-equiv", "charset"].find(name => element.hasAttribute(name));
    if (name) return `${tagName}[${name}=${element.getAttribute(name)}]`;
    if (tagName === "LINK") return `${tagName}[rel=${element.getAttribute("rel")}][href=${element.getAttribute("href")}]`;
    return element.outerHTML;
};

/** Turns the stashed static titles back into title elements, and returns the text that HeadOutlet will take as its default title. */
const restoreStaticTitles = () => {
    let defaultTitleText = null;
    for (const node of Array.from(document.head.childNodes)) {
        const match = STASHED_TITLE_MARKER.exec(markerOf(node));
        if (match === null) continue;
        const title = document.createElement("title");
        title.textContent = defaultTitleText = decodeURIComponent(match[1] ?? "");
        node.remove();
        // Put it below the prerendered title, so the tab keeps the prerendered one and HeadOutlet takes this as the default title.
        // The text node keeps HeadOutlet from mistaking it for its own title, which follows a comment.
        document.head.append(document.createTextNode("\n"), title);
    }
    return defaultTitleText;
};

/** Returns the markers of the prerendered HeadOutlet contents and the nodes between them, or null if there are none. */
const findPrerenderedRange = () => {
    const nodes = Array.from(document.head.childNodes);
    const begin = nodes.findIndex(node => markerOf(node) === BEGIN_MARKER);
    const end = nodes.findIndex(node => markerOf(node) === END_MARKER);
    return begin === -1 || end < begin ? null : { begin: nodes[begin], end: nodes[end], nodes: nodes.slice(begin + 1, end) };
};

/** Removes the prerendered HeadOutlet contents one by one, only when Blazor renders their counterparts, so that nothing flickers. */
class PrerenderedHeadCleaner {
    constructor(range, defaultTitleText) {
        const elements = range.nodes.filter(node => node.nodeType === Node.ELEMENT_NODE);
        this.range = range;
        this.defaultTitleText = defaultTitleText;
        this.title = elements.find(element => element.tagName === "TITLE") ?? null;
        this.remainingElements = elements.filter(element => element !== this.title);
        this.loadingElements = new Set();
        this.initialUrl = urlOf(location.href);
        this.headObserver = new MutationObserver(records => this.onHeadChanged(records));
        this.bodyObserver = new MutationObserver(() => this.finishIfNavigated());
    }

    start() {
        this.headObserver.observe(document.head, { childList: true, subtree: true, characterData: true });
        this.bodyObserver.observe(document.body, { childList: true, subtree: true });
        this.finishIfDone();
    }

    onHeadChanged(records) {
        for (const record of records) {
            for (const node of record.removedNodes) this.forget(node);
            for (const node of record.addedNodes) {
                if (node.nodeType === Node.ELEMENT_NODE && node.tagName !== "TITLE") this.removeCounterpartOf(node);
            }
        }
        // Match the elements of the new page before the cleanup, so that shared stylesheets wait for loading too.
        if (this.finishIfNavigated()) return;
        this.keepTitleUntilRendered();
        this.finishIfDone();
    }

    forget(prerenderedElement) {
        const index = this.remainingElements.indexOf(prerenderedElement);
        if (index !== -1) this.remainingElements.splice(index, 1);
    }

    removeCounterpartOf(renderedElement) {
        const key = keyOf(renderedElement);
        const prerenderedElement = this.remainingElements.find(element => keyOf(element) === key);
        if (prerenderedElement === undefined) return;
        this.forget(prerenderedElement);
        if (!isStylesheet(renderedElement)) return prerenderedElement.remove();

        // Keep the prerendered stylesheet until the rendered one is loaded, to avoid a flash of unstyled content.
        this.loadingElements.add(prerenderedElement);
        const onLoaded = () => {
            prerenderedElement.remove();
            this.loadingElements.delete(prerenderedElement);
            this.finishIfDone();
        };
        renderedElement.addEventListener("load", onLoaded, { once: true });
        renderedElement.addEventListener("error", onLoaded, { once: true });
    }

    // HeadOutlet removes the prerendered title and shows its default title until the page renders its own one.
    // The prerendered title is put back above them meanwhile, because the browser shows the first title.
    keepTitleUntilRendered() {
        if (this.title === null) return;
        if (this.hasRenderedTitle()) {
            this.title.remove();
            this.title = null;
        }
        else if (!this.title.isConnected) this.range.begin.after(this.title);
    }

    hasRenderedTitle() {
        // HeadOutlet's default title does not count, unless it reads the same as the prerendered one.
        const isRendered = title => title.textContent !== this.defaultTitleText || title.textContent === this.title.textContent;
        return Array.from(document.head.getElementsByTagName("title")).some(title => title !== this.title && isRendered(title));
    }

    finishIfDone() {
        if (this.remainingElements.length === 0 && this.loadingElements.size === 0 && this.title === null) this.finish();
    }

    // On the first page navigation, the prerendered contents left are not for the current page anymore.
    finishIfNavigated() {
        if (urlOf(location.href) === this.initialUrl) return false;
        this.finish();
        return true;
    }

    finish() {
        this.headObserver.disconnect();
        this.bodyObserver.disconnect();
        const { begin, nodes, end } = this.range;
        for (const node of [begin, ...nodes, end]) {
            if (!this.loadingElements.has(node)) node.remove();
        }
    }
}

const setup = () => {
    if (typeof WorkerGlobalScope !== "undefined") return;
    const defaultTitleText = restoreStaticTitles();
    const range = findPrerenderedRange();
    if (range !== null) new PrerenderedHeadCleaner(range, defaultTitleText).start();
};

export function beforeStart() {
    setup();
}

export function beforeWebStart() {
    setup();
}
