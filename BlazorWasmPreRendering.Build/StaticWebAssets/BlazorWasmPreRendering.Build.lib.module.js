const BEGIN_MARKER = "%%-PRERENDERING-HEADOUTLET-BEGIN-%%";
const END_MARKER = "%%-PRERENDERING-HEADOUTLET-END-%%";
const STASHED_TITLE_MARKER = /^%%-PRERENDERING-DEFAULT-TITLE-%%(?:\s+(\S*))?$/;
const CLEANUP_DELAY = 3000;

const markerOf = node => node.nodeType === Node.COMMENT_NODE ? node.textContent.trim() : "";
const urlOf = href => href.split("#")[0];
const isStylesheet =element => element.tagName === "LINK" && element.relList.contains("stylesheet");

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
        this.begin = range.begin;
        this.nodesToClean = new Set([range.begin, ...range.nodes, range.end]);
        this.defaultTitleText = defaultTitleText;
        this.title = elements.find(element => element.tagName === "TITLE") ?? null;
        this.remainingElements = elements.filter(element => element !== this.title);
        this.initialUrl = urlOf(location.href);
        // The head observer is created first, so that it handles the changes of a navigation before the body observer cleans up.
        this.headObserver = new MutationObserver(records => this.onHeadChanged(records));
        this.headObserver.observe(document.head, { childList: true });
        this.bodyObserver = new MutationObserver(() => this.finishIfNavigated());
        this.bodyObserver.observe(document.body, { childList: true, subtree: true });
    }

    onHeadChanged(records) {
        for (const record of records) {
            for (const node of record.addedNodes) {
                if (node.nodeType === Node.ELEMENT_NODE && node.tagName !== "TITLE") this.removeCounterpartOf(node);
            }
        }
        if (!this.finishIfNavigated()) this.keepTitleUntilRendered();
    }

    removeCounterpartOf(renderedElement) {
        const key = keyOf(renderedElement);
        const index = this.remainingElements.findIndex(element => keyOf(element) === key);
        if (index === -1) return;
        const [prerenderedElement] = this.remainingElements.splice(index, 1);
        if (!isStylesheet(renderedElement)) return prerenderedElement.remove();

        // Keep the prerendered stylesheet until the rendered one is loaded, even through the cleanup, to avoid a flash of unstyled content.
        this.nodesToClean.delete(prerenderedElement);
        const removePrerenderedElement = () => prerenderedElement.remove();
        renderedElement.addEventListener("load", removePrerenderedElement, { once: true });
        renderedElement.addEventListener("error", removePrerenderedElement, { once: true });
    }

    // HeadOutlet removes the prerendered title and shows its default title until the page renders its own one.
    // The prerendered title is put back above them meanwhile, because the browser shows the first title.
    keepTitleUntilRendered() {
        if (this.title === null) return;
        if (this.hasRenderedTitle()) {
            this.title.remove();
            this.title = null;
        }
        else if (!this.title.isConnected) this.begin.after(this.title);
    }

    hasRenderedTitle() {
        // HeadOutlet's default title does not count, unless it reads the same as the prerendered one.
        const isRendered = title => title.textContent !== this.defaultTitleText || title.textContent === this.title.textContent;
        return Array.from(document.head.getElementsByTagName("title")).some(title => title !== this.title && isRendered(title));
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
        for (const node of this.nodesToClean) node.remove();
    }
}

let cleaner = null;

const setup = () => {
    if (typeof WorkerGlobalScope !== "undefined") return;
    const defaultTitleText = restoreStaticTitles();
    const range = findPrerenderedRange();
    if (range !== null) cleaner = new PrerenderedHeadCleaner(range, defaultTitleText);
};

// The prerendered contents that Blazor has not rendered by then are not for the current page.
const scheduleCleanup = () => setTimeout(() => cleaner?.finish(), CLEANUP_DELAY);

export function beforeStart() {
    setup();
}

export function beforeWebStart() {
    setup();
}

export function afterStarted() {
    scheduleCleanup();
}

export function afterWebStarted() {
    scheduleCleanup();
}
