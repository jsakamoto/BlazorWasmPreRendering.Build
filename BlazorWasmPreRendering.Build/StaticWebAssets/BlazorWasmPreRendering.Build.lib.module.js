const NULL = null;
const title = "title";
const TITLE = "TITLE";
const LINK = "LINK";
const HEADOUTLET_MARKER = "%%-PRERENDERING-HEADOUTLET";
const BEGIN_MARKER = HEADOUTLET_MARKER + "-BEGIN-%%";
const END_MARKER = HEADOUTLET_MARKER + "-END-%%";
const STASHED_TITLE_MARKER = /^%%-PRERENDERING-DEFAULT-TITLE-%%(?:\s+(\S*))?$/;
const CLEANUP_DELAY = 3000;
const document = globalThis.document;
const head = document?.head;

/** @typedef {{ begin: ChildNode, end: ChildNode, nodes: ChildNode[] }} PrerenderedRange */

/** @type {(node: Node) => string} */
const markerOf = node => node.nodeType === Node.COMMENT_NODE ? node.textContent.trim() : "";

/** @type {(href: string) => string} */
const urlOf = href => href.split("#")[0];

/** @type {(element: Element) => boolean} */
const isStylesheet = element => element.tagName === LINK && element.relList.contains("stylesheet");

/** @type {(element: Element, name: string) => string | null} */
const attrOf = (element, name) => element.getAttribute(name);

/** @type {(callback: MutationCallback) => MutationObserver} */
const createMutationObserver = (callback) => new MutationObserver(callback);

/**
 * Elements with the same key are counterparts of each other, even if their contents differ.
 * @type {(element: Element) => string}
 */
const keyOf = element => {
    const { tagName } = element;
    const name = tagName === "META" && ["name", "property", "http-equiv", "charset"].find(name => element.hasAttribute(name));
    if (name) return `${tagName}[${name}=${attrOf(element, name)}]`;
    if (tagName === LINK) return `${tagName}[r=${attrOf(element, "rel")}][h=${attrOf(element, "href")}]`;
    return element.outerHTML;
};

/**
 * Turns the stashed static titles back into title elements, and returns the text that HeadOutlet will take as its default title.
 * @returns {string | null}
 */
const restoreStaticTitles = () => {
    let defaultTitleText = NULL;
    for (const node of [...head.childNodes]) {
        const match = STASHED_TITLE_MARKER.exec(markerOf(node));
        if (match === NULL) continue;
        const titleElement = document.createElement(title);
        titleElement.textContent = defaultTitleText = decodeURIComponent(match[1] ?? "");
        node.remove();
        // Put it below the prerendered title, so the tab keeps the prerendered one and HeadOutlet takes this as the default title.
        // The text node keeps HeadOutlet from mistaking it for its own title, which follows a comment.
        head.append(document.createTextNode("\n"), titleElement);
    }
    return defaultTitleText;
};

/**
 * Returns the markers of the prerendered HeadOutlet contents and the nodes between them, or null if there are none.
 * @returns {PrerenderedRange | null}
 */
const findPrerenderedRange = () => {
    const nodes = [...head.childNodes];
    const begin = nodes.findIndex(node => markerOf(node) === BEGIN_MARKER);
    const end = nodes.findIndex(node => markerOf(node) === END_MARKER);
    return begin === -1 || end < begin ? NULL : { begin: nodes[begin], end: nodes[end], nodes: nodes.slice(begin + 1, end) };
};

/**
 * Starts removing the prerendered HeadOutlet contents one by one, only when Blazor renders their counterparts, so that nothing flickers.
 * @param {PrerenderedRange} range
 * @param {string | null} defaultTitleText
 * @returns {() => void} A function that removes everything left and stops watching.
 */
const startCleaner = (range, defaultTitleText) => {
    const elements = /** @type {Element[]} */ (range.nodes.filter(node => node.nodeType === Node.ELEMENT_NODE));
    const nodesToClean = new Set([range.begin, ...range.nodes, range.end]);
    const initialUrl = urlOf(location.href);
    let prerenderedTitle = elements.find(element => element.tagName === TITLE) ?? NULL;
    const remainingElements = elements.filter(element => element !== prerenderedTitle);

    /** @param {Element} renderedElement */
    const removeCounterpartOf = renderedElement => {
        const key = keyOf(renderedElement);
        const index = remainingElements.findIndex(element => keyOf(element) === key);
        if (index === -1) return;
        const [prerenderedElement] = remainingElements.splice(index, 1);
        if (!isStylesheet(renderedElement)) return prerenderedElement.remove();

        // Keep the prerendered stylesheet until the rendered one is loaded, even through the cleanup, to avoid a flash of unstyled content.
        nodesToClean.delete(prerenderedElement);
        const removePrerenderedElement = () => prerenderedElement.remove();
        renderedElement.addEventListener("load", removePrerenderedElement, { once: true });
        renderedElement.addEventListener("error", removePrerenderedElement, { once: true });
    };

    /** @returns {boolean} */
    const hasRenderedTitle = () => {
        // HeadOutlet's default title does not count, unless it reads the same as the prerendered one.
        /** @type {(titleElement: HTMLTitleElement) => boolean} */
        const isRendered = titleElement => titleElement.textContent !== defaultTitleText || titleElement.textContent === prerenderedTitle.textContent;
        return [...head.getElementsByTagName(title)].some(titleElement => titleElement !== prerenderedTitle && isRendered(titleElement));
    };

    // HeadOutlet removes the prerendered title and shows its default title until the page renders its own one.
    // The prerendered title is put back above them meanwhile, because the browser shows the first title.
    const keepTitleUntilRendered = () => {
        if (prerenderedTitle === NULL) return;
        if (hasRenderedTitle()) {
            prerenderedTitle.remove();
            prerenderedTitle = NULL;
        }
        else if (!prerenderedTitle.isConnected) range.begin.after(prerenderedTitle);
    };

    const finish = () => {
        headObserver.disconnect();
        bodyObserver.disconnect();
        for (const node of nodesToClean) node.remove();
    };

    /**
     * On the first page navigation, the prerendered contents left are not for the current page anymore.
     * @returns {boolean} true if the page has navigated and the cleanup is done.
     */
    const finishIfNavigated = () => {
        if (urlOf(location.href) === initialUrl) return false;
        finish();
        return true;
    };

    /** @param {MutationRecord[]} records */
    const onHeadChanged = records => {
        for (const node of records.flatMap(record => [...record.addedNodes])) {
            if (node instanceof Element && node.tagName !== TITLE) removeCounterpartOf(node);
        }
        if (!finishIfNavigated()) keepTitleUntilRendered();
    };

    // The head observer is created first, so that it handles the changes of a navigation before the body observer cleans up.
    const headObserver = createMutationObserver(onHeadChanged);
    headObserver.observe(head, { childList: true });
    const bodyObserver = createMutationObserver(finishIfNavigated);
    bodyObserver.observe(document.body, { childList: true, subtree: true });

    return finish;
};

/** @type {(() => void) | null} */
let finishCleaner = NULL;

const setup = () => {
    if (!head) return;
    const defaultTitleText = restoreStaticTitles();
    const range = findPrerenderedRange();
    if (range !== NULL) finishCleaner = startCleaner(range, defaultTitleText);
};

// The prerendered contents that Blazor has not rendered by then are not for the current page.
const scheduleCleanup = () => setTimeout(() => finishCleaner?.(), CLEANUP_DELAY);

export { setup as beforeStart, setup as beforeWebStart, scheduleCleanup as afterStarted, scheduleCleanup as afterWebStarted };
