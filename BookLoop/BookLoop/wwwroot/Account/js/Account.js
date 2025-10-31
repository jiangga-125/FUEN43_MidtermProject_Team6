(function () {
    function qs(s, el) { return (el || document).querySelector(s); }
    function qsa(s, el) { return Array.from((el || document).querySelectorAll(s)); }

    // Account 區域列表頁共用啟動器
    window.listPageBoot = function (opt) {
        const form = qs('#filter-form');
        const container = qs('#list-container');
        const keyword = qs('#keyword');
        const anti = form?.querySelector('input[name="__RequestVerificationToken"]');
        let inflight = null, timer = null;

        function paramsFromForm(extra = {}) {
            const params = new URLSearchParams(new FormData(form));
            // 任何篩選條件變動都回到第 1 頁
            params.set('page', 1);
            for (const [k, v] of Object.entries(extra)) params.set(k, v);
            return params.toString();
        }

        async function loadList(paramsString) {
            const url = `${opt.listUrl}?${paramsString}`;
            if (inflight) inflight.abort();
            const ctrl = new AbortController(); inflight = ctrl;
            try {
                const resp = await fetch(url, { signal: ctrl.signal, headers: { "X-Requested-With": "XMLHttpRequest" } });
                if (!resp.ok) throw new Error('network');
                const html = await resp.text();
                container.innerHTML = html;
                const fullUrl = `${opt.indexUrl}?${paramsString}`;
                history.replaceState(null, '', fullUrl);
                bindListHandlers();
                opt.onAfterRender && opt.onAfterRender();
            } catch (e) { if (e.name !== 'AbortError') console.error(e); }
            finally { if (inflight === ctrl) inflight = null; }
        }

        function debounced() {
            clearTimeout(timer);
            timer = setTimeout(() => loadList(paramsFromForm()), 300);
        }

        function bindListHandlers() {
            qsa('a.ajax-link', container).forEach(a => {
                a.addEventListener('click', e => {
                    const href = a.getAttribute('href') || '';
                    if (href.includes(opt.listUrl)) {
                        e.preventDefault();
                        const url = new URL(a.href);
                        loadList(url.searchParams.toString());
                    }
                });
            });

            if (opt.del) {
                qsa('.js-del', container).forEach(btn => {
                    btn.addEventListener('click', () => {
                        qs('#del-id').value = btn.dataset.id;
                        qs('#del-name').textContent = btn.dataset.name || '-';
                        qs('#del-email').textContent = btn.dataset.email || '-';
                        new bootstrap.Modal(qs(`#${opt.del.modalId}`)).show();
                    });
                });
            }
        }

        // initial wire-up
        keyword && keyword.addEventListener('input', debounced);
        form && qsa('.js-auto-submit', form).forEach(sel => sel.addEventListener('change', () => loadList(paramsFromForm())));
        bindListHandlers();

        // delete flow
        if (opt.del) {
            const delForm = qs(`#${opt.del.formId}`);
            delForm?.addEventListener('submit', async (e) => {
                e.preventDefault();
                const id = qs('#del-id').value;
                const url = opt.del.postUrl + '/' + encodeURIComponent(id);
                try {
                    const resp = await fetch(url, {
                        method: 'POST',
                        headers: {
                            'X-Requested-With': 'XMLHttpRequest',
                            'Content-Type': 'application/x-www-form-urlencoded'
                        },
                        body: new URLSearchParams({
                            '__RequestVerificationToken': anti?.value ?? '',
                            'id': id
                        })
                    });
                    if (resp.status === 401) { location.href = '/Auth/Login'; return; }
                    if (resp.status === 403) { alert('沒有權限執行刪除。'); return; }
                    const data = await resp.json();
                    if (data.ok) {
                        bootstrap.Modal.getInstance(qs(`#${opt.del.modalId}`))?.hide();
                        opt.afterDelete ? opt.afterDelete() : loadList(paramsFromForm());
                    } else {
                        alert(data.message || '刪除失敗');
                    }
                } catch (err) {
                    console.error(err); alert('網路異常，請稍後再試。');
                }
            });
        }

        return { reload: () => loadList(paramsFromForm()) };
    };
})();
