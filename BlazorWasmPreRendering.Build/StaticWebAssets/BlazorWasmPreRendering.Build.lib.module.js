const setup = () => {
    if (typeof WorkerGlobalScope !== "undefined") return;
    ((document, test, nextSibling, nodeIterator, node) => {
        nodeIterator = document.createNodeIterator(document.head, 128/*NodeFilter.SHOW_COMMENT*/);
        while (nodeIterator.nextNode()) {
            node = nodeIterator.referenceNode;
            if (test(node)) {
                do {
                    nextSibling = node.nextSibling
                    nextSibling.remove();
                } while (!test(nextSibling));
                node.remove();
                break;
            }
        }
        Array.from(document.querySelectorAll('script')).pop()?.remove();
    })(document, node => /^%%-PRERENDERING-HEADOUTLET-(BEGIN|END)-%%$/.test(node.textContent.trim()));
}
export function afterStarted() {
    setup();
}
export function afterWebStarted() {
    setup();
}