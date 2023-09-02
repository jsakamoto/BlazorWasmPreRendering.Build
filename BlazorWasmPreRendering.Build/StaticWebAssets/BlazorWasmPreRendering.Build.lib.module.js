export const afterStarted = () => {
    ((document, marker, nextSibling) => {
        var nodeIterator = document.createNodeIterator(document.head, 128/*NodeFilter.SHOW_COMMENT*/);
        while (nodeIterator.nextNode()) {
            var node = nodeIterator.referenceNode;
            if (marker.test(node.textContent.trim())) {
                while (nextSibling = node.nextSibling) {
                    var textContent = nextSibling.textContent.trim();
                    nextSibling.remove();
                    if (marker.test(textContent)) break;
                }
                node.remove();
                break;
            }
        }
    })(document, /^%%-PRERENDERING-HEADOUTLET-(BEGIN|END)-%%$/);
}