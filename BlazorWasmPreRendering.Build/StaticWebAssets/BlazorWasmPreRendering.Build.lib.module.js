const BEGIN_MARKER = "%%-PRERENDERING-HEADOUTLET-BEGIN-%%";
const END_MARKER = "%%-PRERENDERING-HEADOUTLET-END-%%";
const STASHED_TITLE_MARKER = /^%%-PRERENDERING-DEFAULT-TITLE-%%(?:\s+(\S*))?$/;
const CLEANUP_DELAY = 3000;

/** @typedef {{ begin: ChildNode, end: ChildNode, nodes: ChildNode[] }} PrerenderedRange */

/** @type {(node: Node) => string} */
const markerOf = node => node.nodeType === Node.COMMENT_NODE ? node.textContent.trim() : "";

/** @type {(href: string) => string} */
const urlOf = href => href.split("#")[0];

/** @type {(element: Element) => boolean} */
const isStylesheet = element => element instanceof HTMLLinkElement && element.relList.contains("stylesheet");

/**
 * Elements with the same key are counterparts of each other, even if their contents differ.
 * @type {(element: Element) => string}
 */
const keyOf = element => {
    const { tagName } = element;
    const name = tagName === "META" && ["name", "property", "http-equiv", "charset"].find(name => element.hasAttribute(name));
    if (name) return `${tagName}[${name}=${element.getAttribute(name)}]`;
    if (tagName === "LINK") return `${tagName}[rel=${element.getAttribute("rel")}][href=${element.getAttribute("href")}]`;
    return element.outerHTML;
};

/**
 * Turns the stashed static titles back into title elements, and returns the text that HeadOutlet will take as its default title.
 * @returns {string | null}
 */
const restoreStaticTitles = () => {
    let defaultTitleText = null;
    for (const node of [...document.head.childNodes]) {
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

/**
 * Returns the markers of the prerendered HeadOutlet contents and the nodes between them, or null if there are none.
 * @returns {PrerenderedRange | null}
 */
const findPrerenderedRange = () => {
    const nodes = [...document.head.childNodes];
    const begin = nodes.findIndex(node => markerOf(node) === BEGIN_MARKER);
    const end = nodes.findIndex(node => markerOf(node) === END_MARKER);
    return begin === -1 || end < begin ? null : { begin: nodes[begin], end: nodes[end], nodes: nodes.slice(begin + 1, end) };
};

/** Removes the prerendered HeadOutlet contents one by one, only when Blazor renders their counterparts, so that nothing flickers. */
class PrerenderedHeadCleaner {
    /**
     * @param {PrerenderedRange} range
     * @param {string | null} defaultTitleText
     */
    constructor(range, defaultTitleText) {
        const elements = /** @type {Element[]} */ (range.nodes.filter(node => node.nodeType === Node.ELEMENT_NODE));
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

    /** @param {MutationRecord[]} records */
    onHeadChanged(records) {
        for (const node of records.flatMap(record => [...record.addedNodes])) {
            if (node instanceof Element && node.tagName !== "TITLE") this.removeCounterpartOf(node);
        }
        if (!this.finishIfNavigated()) this.keepTitleUntilRendered();
    }

    /** @param {Element} renderedElement */
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

    /** @returns {boolean} */
    hasRenderedTitle() {
        // HeadOutlet's default title does not count, unless it reads the same as the prerendered one.
        /** @type {(title: HTMLTitleElement) => boolean} */
        const isRendered = title => title.textContent !== this.defaultTitleText || title.textContent === this.title.textContent;
        return [...document.head.getElementsByTagName("title")].some(title => title !== this.title && isRendered(title));
    }

    /**
     * On the first page navigation, the prerendered contents left are not for the current page anymore.
     * @returns {boolean} true if the page has navigated and the cleanup is done.
     */
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

/** @type {PrerenderedHeadCleaner | null} */
let cleaner = null;

const setup = () => {
    if (typeof WorkerGlobalScope !== "undefined") return;
    const defaultTitleText = restoreStaticTitles();
    const range = findPrerenderedRange();
    if (range !== null) cleaner = new PrerenderedHeadCleaner(range, defaultTitleText);
};

// The prerendered contents that Blazor has not rendered by then are not for the current page.
const scheduleCleanup = () => setTimeout(() => cleaner?.finish(), CLEANUP_DELAY);

export { setup as beforeStart, setup as beforeWebStart, scheduleCleanup as afterStarted, scheduleCleanup as afterWebStarted };
