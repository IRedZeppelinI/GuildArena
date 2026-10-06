export function create(dialog, reference, returnFocusSelector) {
    // Some launchers become disabled as soon as preparation opens (exchange).
    const trigger = returnFocusSelector ? document.querySelector(returnFocusSelector) : document.activeElement;
    let dismissible = false;
    let disposed = false;
    let pointerStartedOutside = false;
    const outside = event => {
        const box = dialog.getBoundingClientRect();
        return event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom;
    };
    const dismiss = () => { if (dismissible && !disposed) reference.invokeMethodAsync('DismissAsync'); };
    const cancel = event => { event.preventDefault(); dismiss(); };
    const pointerDown = event => { pointerStartedOutside = event.target === dialog && outside(event); };
    const pointerUp = event => {
        if (pointerStartedOutside && event.target === dialog && outside(event)) dismiss();
        pointerStartedOutside = false;
    };
    const keyDown = event => {
        if (event.key === 'Escape') {
            event.preventDefault();
            dismiss();
            return;
        }
        if (event.key !== 'Tab') return;
        const items = [...dialog.querySelectorAll('button:not(:disabled), [tabindex="0"]')]
            .filter(element => element.getClientRects().length > 0);
        const first = items[0], last = items.at(-1);
        if ((event.shiftKey && document.activeElement === first) || (!event.shiftKey && document.activeElement === last)) {
            event.preventDefault();
            (event.shiftKey ? last : first)?.focus({ preventScroll: true });
        }
    };
    const focus = () => {
        const initial = dialog.querySelector('[autofocus]:not(:disabled)') ?? dialog.querySelector('h2');
        if (!initial.hasAttribute('tabindex') && !initial.matches('button')) initial.tabIndex = -1;
        initial.focus({ preventScroll: true });
    };
    // Browser Back / close watchers can emit a non-cancelable close. Only component
    // disposal is an accepted close; pending commands and results must stay modal.
    const close = () => { if (!disposed) { dialog.showModal(); focus(); } };
    dialog.addEventListener('cancel', cancel);
    dialog.addEventListener('close', close);
    dialog.addEventListener('pointerdown', pointerDown);
    dialog.addEventListener('pointerup', pointerUp);
    dialog.addEventListener('keydown', keyDown);
    return {
        update(value) {
            if (disposed) return;
            dismissible = value;
            if (!dialog.open) { dialog.showModal(); focus(); }
            else if (!dialog.contains(document.activeElement) && dialog.matches(':modal')) focus();
        },
        focus,
        async dispose() {
            disposed = true;
            dialog.removeEventListener('cancel', cancel);
            dialog.removeEventListener('close', close);
            dialog.removeEventListener('pointerdown', pointerDown);
            dialog.removeEventListener('pointerup', pointerUp);
            dialog.removeEventListener('keydown', keyDown);
            if (dialog.open) dialog.close();
            // Let Blazor re-enable the launcher before attempting focus return.
            await new Promise(resolve => setTimeout(resolve, 0));
            // A terminal result or the orientation gate may already own focus.
            if (!document.querySelector('dialog[open], .result-dialog')) {
                const target = trigger?.isConnected && !trigger.matches(':disabled') ? trigger
                    : document.querySelector('.end-turn-btn:not(:disabled), .arena-surrender:not(:disabled), .feedback-message');
                target?.focus({ preventScroll: true });
            }
        }
    };
}
