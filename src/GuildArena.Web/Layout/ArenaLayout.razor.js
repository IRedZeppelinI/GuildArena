export function createOrientationGate(dialog, button, note) {
    const portrait = matchMedia('(orientation: portrait) and (max-width: 950px), (orientation: portrait) and (pointer: coarse)');
    let active = false;
    let disposed = false;
    let enteredFullscreen = false;
    let lockedOrientation = false;

    const update = () => {
        if (disposed) return;
        if (active && portrait.matches) {
            if (!dialog.open) dialog.showModal();
        } else if (dialog.open) {
            dialog.close();
        }
    };
    // Escape must not bypass the landscape requirement. This does not pause combat.
    const preventDismissal = event => event.preventDefault();
    const enterLandscape = async () => {
        try {
            if (!document.fullscreenElement) {
                await document.documentElement.requestFullscreen();
                enteredFullscreen = true;
            }
            if (disposed) {
                if (enteredFullscreen && document.fullscreenElement) await document.exitFullscreen();
                return;
            }
            if (!screen.orientation?.lock) throw new Error('Orientation lock unavailable');
            await screen.orientation.lock('landscape');
            lockedOrientation = true;
            if (disposed) screen.orientation.unlock();
        } catch {
            if (!disposed) note.textContent = 'Automatic rotation is unavailable in this browser. Rotate your device manually to continue. Combat remains active.';
        }
    };
    portrait.addEventListener('change', update);
    dialog.addEventListener('cancel', preventDismissal);
    // Some browsers may close a dialog without a cancelable event (e.g. Back).
    // Reapply the gate while portrait combat is still active.
    dialog.addEventListener('close', update);
    button.addEventListener('click', enterLandscape);

    return {
        setActive(value) { active = value; update(); },
        async dispose() {
            disposed = true;
            portrait.removeEventListener('change', update);
            dialog.removeEventListener('cancel', preventDismissal);
            dialog.removeEventListener('close', update);
            button.removeEventListener('click', enterLandscape);
            if (dialog.open) dialog.close();
            if (lockedOrientation) screen.orientation.unlock();
            if (enteredFullscreen && document.fullscreenElement === document.documentElement) {
                try { await document.exitFullscreen(); } catch { /* Already exited by the browser. */ }
            }
        }
    };
}
