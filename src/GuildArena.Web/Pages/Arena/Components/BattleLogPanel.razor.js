const panels = new WeakMap();

export function update(scroll, updates, count) {
    let state = panels.get(scroll);
    if (!state) {
        state = { count, following: true, unread: 0 };
        const notify = () => {
            updates.hidden = state.unread === 0;
            updates.querySelector('[role="status"]').textContent = state.unread === 0 ? ''
                : `${state.unread} new ${state.unread === 1 ? 'entry' : 'entries'}`;
        };
        state.onScroll = () => {
            // Hidden/closed panels have no meaningful scroll geometry.
            if (!scroll.clientHeight) return;
            state.following = scroll.scrollHeight - scroll.clientHeight - scroll.scrollTop <= 4;
            if (state.following && state.unread) {
                state.unread = 0;
                notify();
            }
        };
        state.latest = () => {
            state.following = true;
            state.unread = 0;
            notify();
            scroll.scrollTop = scroll.scrollHeight;
        };
        state.notify = notify;
        state.button = updates.querySelector('button');
        state.onLatest = () => {
            state.latest();
            scroll.focus({ preventScroll: true });
        };
        state.resize = new ResizeObserver(() => {
            if (state.following) scroll.scrollTop = scroll.scrollHeight;
        });
        state.resize.observe(scroll);
        scroll.addEventListener('scroll', state.onScroll, { passive: true });
        state.button.addEventListener('click', state.onLatest);
        panels.set(scroll, state);
        state.latest();
        return;
    }

    if (count < state.count) state.latest();
    else if (count > state.count) {
        if (state.following) state.latest();
        else {
            state.unread += count - state.count;
            state.notify();
        }
    }
    state.count = count;
}

export function dispose(scroll) {
    const state = panels.get(scroll);
    if (!state) return;
    state.resize.disconnect();
    scroll.removeEventListener('scroll', state.onScroll);
    state.button.removeEventListener('click', state.onLatest);
    panels.delete(scroll);
}
