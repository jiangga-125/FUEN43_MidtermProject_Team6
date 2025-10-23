// @ts-nocheck
(function () {
    // === NoData 外掛：labels 為空，或所有 data 皆為 0/null → 顯示「無資料」 ===
    const NoDataPlugin = {
        id: 'no-data',
        afterDraw(chart, _args, opts) {
            const labels = chart?.data?.labels || [];
            const ds = chart?.data?.datasets || [];
            const hasData =
                Array.isArray(labels) && labels.length > 0 &&
                ds.some(d => Array.isArray(d.data) && d.data.some(v => v != null && Number(v) !== 0));

            if (hasData) return;

            const { ctx, chartArea } = chart;
            if (!chartArea) return;
            const { left, top, width, height } = chartArea;

            ctx.save();
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.font = (opts?.font) || '14px system-ui, -apple-system, Segoe UI, Roboto';
            ctx.fillStyle = (opts?.color) || '#666';
            // 若想保留格線就註解掉下一行
            // ctx.clearRect(left, top, width, height);
            ctx.fillText((opts?.text) || '無資料', left + width / 2, top + height / 2);
            ctx.restore();
        }
    };
    if (window.Chart && Chart.register) Chart.register(NoDataPlugin);

    function __narrowPage() {
        if (!document.getElementById('app-narrow-css')) {
            const s = document.createElement('style');
            s.id = 'app-narrow-css';
            s.textContent = `
      .app-narrow { max-width: 1100px; margin: 0 auto; padding-left: 12px; padding-right: 12px; }
    `;
            document.head.appendChild(s);
        }
        const targets = [
            document.querySelector('main'),
            document.querySelector('.container-fluid'),
            document.querySelector('.container'),
            document.body.firstElementChild
        ];
        const host = targets.find(Boolean);
        if (host) host.classList.add('app-narrow');
    }
    __narrowPage();

    const $ = (id) => document.getElementById(id);

    // === API 來源（升級：穩定讀取 data-* 或全域變數） ===
    const SCRIPT_EL =
        document.currentScript
        || document.querySelector('script[data-api-preview],script[data-def-url],script[data-cats-url]')
        || document.querySelector('script[src*="report-def-form.js"]');

    const apiPreview =
        window.__API_PREVIEW__
        || SCRIPT_EL?.dataset?.apiPreview
        || (typeof previewUrl !== "undefined" ? previewUrl : null)
        || null;

    const apiCats =
        window.__API_CATS__
        || SCRIPT_EL?.dataset?.catsUrl
        || (typeof catsUrl !== "undefined" ? catsUrl : null)
        || "/ReportMail/Lookup/Categories";

    const apiDef =
        window.__DEF_URL__
        || SCRIPT_EL?.dataset?.defUrl
        || (typeof defUrl !== "undefined" ? defUrl : null)
        || null;

    const FEATURE_PUBLISH_DECADE = false;

    // 全域：分類「全集」（不含空值），用於精準判斷是否全選
    const __CATS_ALL_IDS__ = { sales: [], borrow: [] };

    // ===== Tom Select 資源與外觀 =====
    async function __ensureAsset(id, tag, attrs) {
        if (document.getElementById(id)) return;
        await new Promise((res, rej) => {
            const el = document.createElement(tag);
            el.id = id; for (const k in attrs) el.setAttribute(k, attrs[k]);
            if (tag === 'script') { el.onload = res; el.onerror = rej; document.body.appendChild(el); }
            else { document.head.appendChild(el); res(); }
        });
    }
    async function __ensureTomSelect() {
        await __ensureAsset('tomselect-css', 'link', { rel: 'stylesheet', href: 'https://cdn.jsdelivr.net/npm/tom-select/dist/css/tom-select.css' });
        await __ensureAsset('tomselect-js', 'script', { src: 'https://cdn.jsdelivr.net/npm/tom-select/dist/js/tom-select.complete.min.js' });
    }
    function __injectSingleBoxCSS() {
        if (document.getElementById('rf-singlebox-css')) return;
        const style = document.createElement('style');
        style.id = 'rf-singlebox-css';
        style.textContent = `
select.form-select + .ts-wrapper.form-select { border:0; background:transparent; padding:0; }
select.form-select + .ts-wrapper.form-select .ts-control{
  min-height: calc(1.5em + .75rem + 2px);
  padding: .375rem .75rem;
  width:100%;
  border: 1px solid var(--bs-border-color,#dee2e6);
  border-radius: .375rem;
  background-color: var(--bs-body-bg,#fff);
  line-height:1.5; box-shadow:none; cursor:pointer;
}
select.form-select + .ts-wrapper.form-select .ts-control::after{ display:none !important; }
select.form-select + .ts-wrapper.form-select .ts-control:focus,
select.form-select + .ts-wrapper.form-select .ts-control:focus-within{
  border-color: var(--bs-primary,#0d6efd);
  box-shadow: 0 0 0 .25rem rgba(13,110,253,.25);
  outline:0;
}
select[multiple].form-select + .ts-wrapper.form-select .ts-control{
  overflow:hidden; white-space:nowrap; text-overflow:ellipsis;
}
select[multiple].form-select + .ts-wrapper.form-select .ts-control .item{
  background:transparent; border:0; padding:0; margin:0 .25rem 0 0;
}
select[multiple].form-select + .ts-wrapper.form-select .ts-control .remove{ display:none !重要; }
.ts-dropdown{
  border:1px solid var(--bs-border-color,#dee2e6);
  border-radius:.375rem; box-shadow:0 .5rem 1rem rgba(0,0,0,.15);
  margin-top:.25rem;
  max-height: 24rem;
  overflow: auto;
}
.ts-dropdown .ts-dropdown-content{ max-height: inherit; overflow-y: auto; }
.ts-dropdown .ts-dropdown-input{
  margin:0; padding:.5rem .75rem;
  border:0; border-bottom:1px solid var(--bs-border-color,#dee2e6);
  border-radius:0;
}
.ts-dropdown .option{ padding:.375rem .75rem; }
.ts-dropdown .option.active{ background: var(--bs-primary-bg-subtle,#e7f1ff); color:inherit; }
select.is-invalid + .ts-wrapper .ts-control{ border-color: var(--bs-danger,#dc3545); box-shadow:0 0 0 .25rem rgba(220,53,69,.25); }
select.is-valid   + .ts-wrapper .ts-control{ border-color: var(--bs-success,#198754); box-shadow:0 0 0 .25rem rgba(25,135,84,.25); }
select:disabled   + .ts-wrapper .ts-control{ background-color: var(--bs-secondary-bg,#e9ecef); opacity:.65; pointer-events:none; }
`;
        document.head.appendChild(style);
    }

    // 可搜尋下拉：mouseup 決定，且每次變動都 dispatch 'change'
    function __enhanceSearchableSelect(selId, extra) {
        const el = document.getElementById(selId);
        if (!el) return;

        el.classList.add('form-select');
        if (el.tomselect) { try { el.tomselect.destroy(); } catch { } }

        if (typeof __injectSingleBoxCSS === 'function') __injectSingleBoxCSS();

        const isMulti = !!el.multiple;

        const ts = new TomSelect(el, Object.assign({
            plugins: ['dropdown_input'],
            allowEmptyOption: true,
            openOnFocus: true,
            closeAfterSelect: !isMulti,
            hideSelected: false,
            maxOptions: 1000,
            sortField: { field: 'text', direction: 'asc' },
            render: { option: (d, esc) => `<div>${esc(d.text || (d.value === '' ? '（全部）' : ''))}</div>` },
            onInitialize() { this.wrapper.classList.add('form-select'); }
        }, extra || {}));

        const ddContent = ts.dropdown_content || ts.dropdown;
        if (!ddContent) return;

        let armed = false;

        ddContent.addEventListener('mousedown', (evt) => {
            if (evt.button !== 0) return;
            const opt = evt.target.closest('.option[data-value]');
            if (!opt) return;
            armed = true;
            evt.preventDefault();
            evt.stopPropagation();
            ts.focus();
        }, { capture: true });

        if (el.__rfDocMouseup) {
            document.removeEventListener('mouseup', el.__rfDocMouseup, true);
        }

        const handleDocMouseup = (evt) => {
            if (!armed) return;
            armed = false;

            let optEl = ts.activeOption;
            if (!optEl) {
                const maybe = evt.target && (evt.target.closest ? evt.target.closest('.ts-dropdown .option[data-value]') : null);
                if (maybe) optEl = maybe;
            }
            if (!optEl) return;

            const value = optEl.getAttribute('data-value');

            if (value === '') {
                ts.clear();
                if (!isMulti) ts.close();
                ts.refreshOptions(false);
                el.dispatchEvent(new Event('change', { bubbles: true }));
                return;
            }

            if (!isMulti) {
                if (ts.items.length && ts.items[0] === value) {
                    ts.clear();
                    ts.close();
                } else {
                    ts.setValue(value, true);
                    ts.close();
                }
                ts.refreshOptions(false);
                el.dispatchEvent(new Event('change', { bubbles: true }));
                return;
            }

            if (ts.items.includes(value) || optEl.classList.contains('selected')) {
                ts.removeItem(value);
            } else {
                ts.addItem(value);
            }
            ts.refreshOptions(false);
            ts.setCaret(ts.items.length);
            el.dispatchEvent(new Event('change', { bubbles: true }));
        };

        el.__rfDocMouseup = handleDocMouseup;
        document.addEventListener('mouseup', handleDocMouseup, true);
    }

    // ===== UI 切換 =====
    function getChartCategoryUi() {
        const sel = $('Category') || $('category');
        if (sel && sel.value) return sel.value.toLowerCase();
        const hid = $('hidCategory');
        if (hid && hid.value) return hid.value.toLowerCase();
        const btn = document.querySelector('[data-chart].btn-primary,[data-chart].active');
        if (btn) return String(btn.getAttribute('data-chart')).toLowerCase();
        return 'line';
    }
    function isBarPie() {
        const cat = getChartCategoryUi();
        return cat === 'bar' || cat === 'pie';
    }
    function showSections() {
        const cat = getChartCategoryUi();
        const kind = ($('baseKind')?.value || 'sales').toLowerCase();
        if ($('section-orders')) $('section-orders').style.display = (cat === 'line' && kind === 'orders') ? '' : 'none';
        if ($('section-sales')) $('section-sales').style.display = (kind === 'sales') ? '' : 'none';
        if ($('section-borrow')) $('section-borrow').style.display = (kind === 'borrow') ? '' : 'none';
        if ($('rank-sales')) $('rank-sales').style.display = (isBarPie() && kind === 'sales') ? '' : 'none';
        if ($('rank-borrow')) $('rank-borrow').style.display = (isBarPie() && kind === 'borrow') ? '' : 'none';
    }
    function syncUiByCategoryAndKind() {
        const cat = getChartCategoryUi();
        const kindSel = $('baseKind');
        const kind = (kindSel?.value || '').toLowerCase();
        const isBP = (cat === 'bar' || cat === 'pie');

        const gran = $('granGroup');
        const dateLabel = $('dateLabel');
        if (gran) gran.style.display = isBP ? 'none' : '';
        if (dateLabel) dateLabel.textContent = isBP ? '日期區間' : '日期區間與粒度';

        if (kindSel) {
            const optOrders = kindSel.querySelector('option[value="orders"]');
            if (optOrders) {
                optOrders.hidden = isBP;
                optOrders.disabled = isBP;
                if (isBP && kindSel.value === 'orders') {
                    const fallback = kindSel.querySelector('option[value="sales"]') ? 'sales'
                        : (kindSel.querySelector('option[value="borrow"]') ? 'borrow' : '');
                    if (fallback) {
                        kindSel.value = fallback;
                        kindSel.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                }
            }
        }

        const decRow = $('publishDecadeRow');
        if (decRow) {
            const showDecade = FEATURE_PUBLISH_DECADE && isBP && (kind === 'borrow');
            decRow.style.display = showDecade ? '' : 'none';
        }
    }
    function __debounce(fn, delay = 250) {
        let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); };
    }

    // 日期改變：預設把排行迄設為上限
    const __onDateChange = __debounce(async () => {
        try {
            await loadCategories();
            await __updateMaxRank('sales', { forceToMax: true });
            await __updateMaxRank('borrow', { forceToMax: true });
            if (typeof showSections === 'function') showSections();
            if (typeof syncUiByCategoryAndKind === 'function') syncUiByCategoryAndKind();
            if (typeof preview === 'function') preview();
        } catch (err) {
            console.warn('date change refresh failed:', err);
        }
    }, 250);

    // ===== 下拉資料 =====
    function applyCategoryOptions(selId, items) {
        const sel = (typeof selId === 'string') ? document.getElementById(selId) : selId;
        if (!sel) return;

        const seen = new Set();
        const cleaned = (Array.isArray(items) ? items : [])
            .filter(x => x && x.value !== null && x.value !== undefined && String(x.value).trim() !== '' && String(x.value) !== '0')
            .filter(x => { const k = String(x.value); if (seen.has(k)) return false; seen.add(k); return true; })
            .map(x => ({ value: String(x.value), text: String(x.text ?? '') }));

        let keep = [];
        if (sel.tomselect) {
            try { keep = sel.tomselect.items.filter(v => v !== ''); } catch { }
            try { sel.tomselect.destroy(); } catch { }
        }

        sel.innerHTML = '';
        sel.add(new Option('（全部）', ''));
        cleaned.forEach(o => sel.add(new Option(o.text, o.value)));

        try { __enhanceSearchableSelect(selId); } catch (e) { console.warn('Tom Select 初始化失敗：', e); }

        const exists = new Set(Array.from(sel.options).map(o => o.value));
        keep = (keep || []).filter(v => exists.has(v));
        if (sel.tomselect) {
            if (keep.length) sel.tomselect.setValue(keep, true);
            else sel.tomselect.clear(true);
        } else {
            keep.forEach(v => { const o = Array.from(sel.options).find(o => o.value === v); if (o) o.selected = true; });
        }

        // 更新分類全集（不含空值）
        try {
            const ids = Array.from(sel.options || []).map(o => o.value).filter(v => v !== '');
            if (sel.id === 'salesCategories') __CATS_ALL_IDS__.sales = ids;
            if (sel.id === 'borrowCategories') __CATS_ALL_IDS__.borrow = ids;
        } catch { }
    }

    async function loadCategories() {
        if (!apiCats) return;

        const fetchList = async (kind) => {
            try {
                const url = new URL(apiCats, window.location.origin);
                url.searchParams.set('baseKind', kind);
                const df = $('dateFrom')?.value;
                the_dt = $('dateTo')?.value;
                const dt = the_dt; // 兼容舊版
                if (df) url.searchParams.set('start', df);
                if (dt) url.searchParams.set('end', dt);
                const res = await fetch(url.toString(), { cache: 'no-store', credentials: 'same-origin' });
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const json = await res.json();
                return Array.isArray(json) ? json : [];
            } catch (err) {
                console.warn('載入分類清單失敗：', err);
                return [];
            }
        };

        const [salesList, borrowList] = await Promise.all([
            fetchList('sales'),
            fetchList('borrow')
        ]);

        applyCategoryOptions('salesCategories', salesList);
        applyCategoryOptions('borrowCategories', borrowList);
    }

    function genPrice() {
        const sel = $('priceRange'); if (!sel) return;
        sel.innerHTML = ''; sel.add(new Option('（全部）', ''));
        for (let s = 1; s <= 1000; s += 100) {
            const e = Math.min(s + 99, 1000);
            sel.add(new Option(`${s}~${e}`, `${s}-${e}`));
        }
    }
    function genRank(selectFromId, selectToId, max = 100, defFrom = 1, defTo = 10) {
        const sFrom = $(selectFromId), sTo = $(selectToId);
        if (!sFrom || !sTo) return;
        const fill = (sel) => { sel.innerHTML = ''; for (let i = 1; i <= max; i++) sel.add(new Option(String(i), String(i))); };
        fill(sFrom); fill(sTo);
        sFrom.value = String(defFrom); sTo.value = String(defTo);
    }
    function genDecades() {
        const sel = $('publishDecade'); if (!sel) return;
        const now = new Date().getFullYear();
        const end = now - (now % 10);
        sel.innerHTML = '';
        sel.add(new Option('（全部）', ''));
        for (let y = 1901; y <= end; y += 10) {
            const to = y + 9;
            sel.add(new Option(`${y}~${to}年`, `${y}-${to}`));
        }
    }

    // ===== Filters =====
    function buildFilters() {
        const cat = getChartCategoryUi();
        const kind = ($('baseKind')?.value || 'sales').toLowerCase();
        const df = $('dateFrom')?.value;
        const dt = $('dateTo')?.value;
        const gran = document.querySelector('input[name="gran"]:checked')?.value || 'day';

        const filters = [];
        const dateField = (kind === 'borrow') ? 'BorrowDate' : 'OrderDate';
        filters.push({ FieldName: dateField, DataType: 'date', Operator: 'between', ValueJson: JSON.stringify({ from: df, to: dt, gran }) });

        if (kind === 'sales') {
            const elS = $('salesCategories');
            const picked = elS?.tomselect
                ? elS.tomselect.items.map(v => String(v)).filter(v => v !== '')
                : Array.from(elS?.selectedOptions || []).map(o => String(o.value)).filter(v => v !== '');
            const allIds = (__CATS_ALL_IDS__.sales || []).map(String);
            const pickedSet = new Set(picked);
            const isNone = pickedSet.size === 0;
            const isAll = pickedSet.size === allIds.length && allIds.every(id => pickedSet.has(id));
            if (!isNone && !isAll) {
                filters.push({ FieldName: 'CategoryID', DataType: 'select', Operator: 'in', ValueJson: JSON.stringify({ values: picked.map(Number) }) });
            }

            const pr = $('priceRange')?.value;
            if (pr) {
                const [min, max] = pr.split('-').map(Number);
                filters.push({ FieldName: 'SalePrice', DataType: 'number', Operator: 'between', ValueJson: JSON.stringify({ min, max }) });
            }
            if (isBarPie()) {
                const from = Number($('rankFrom')?.value || 1);
                const to = Number($('rankTo')?.value || 10);
                filters.push({ FieldName: 'RankRange', Operator: 'between', ValueJson: JSON.stringify({ from, to }) });
            }
        }
        else if (kind === 'borrow') {
            const elB = $('borrowCategories');
            const picked = elB?.tomselect
                ? elB.tomselect.items.map(v => String(v)).filter(v => v !== '')
                : Array.from(elB?.selectedOptions || []).map(o => String(o.value)).filter(v => v !== '');
            const allIds = (__CATS_ALL_IDS__.borrow || []).map(String);
            const pickedSet = new Set(picked);
            const isNone = pickedSet.size === 0;
            const isAll = pickedSet.size === allIds.length && allIds.every(id => pickedSet.has(id));
            if (!isNone && !isAll) {
                filters.push({ FieldName: 'CategoryID', DataType: 'select', Operator: 'in', ValueJson: JSON.stringify({ values: picked.map(Number) }) });
            }

            if (FEATURE_PUBLISH_DECADE) {
                const dec = $('publishDecade')?.value;
                if (dec) {
                    const [fromYear, toYear] = dec.split('-').map(Number);
                    filters.push({ FieldName: 'PublishDecade', Operator: 'between', ValueJson: JSON.stringify({ fromYear, toYear }) });
                }
            }
            if (isBarPie()) {
                const from = Number($('rankFromBorrow')?.value || 1);
                const to = Number($('rankToBorrow')?.value || 10);
                filters.push({ FieldName: 'RankRange', Operator: 'between', ValueJson: JSON.stringify({ from, to }) });
            }
        }
        else { // orders (line only)
            const metric = $('mCount')?.checked ? 'count' : 'amount';
            filters.push({ FieldName: 'Metric', ValueJson: JSON.stringify({ value: metric }) });

            const group = document.getElementById('orderStatusGroup');
            const all = document.getElementById('os_all');
            if (group && all) {
                const picks = Array.from(group.querySelectorAll('.os:checked')).map(cb => Number(cb.dataset.val));
                if (!all.checked) {
                    filters.push({ FieldName: 'OrderStatus', Operator: 'in', ValueJson: JSON.stringify({ values: picks }) });
                }
            }
            const min = $('ordAmtMin')?.value, max = $('ordAmtMax')?.value;
            if (min || max) filters.push({ FieldName: 'OrderAmount', DataType: 'number', Operator: 'between', ValueJson: JSON.stringify({ min: Number(min || 0), max: Number(max || 999999) }) });
        }
        return filters;
    }

    function computeLegend(cat, baseKind, filters) {
        try {
            const kind = (baseKind || '').toLowerCase();
            if (cat === 'bar' || cat === 'pie') {
                let topN = 10;
                const fr = (filters || []).find(f => (f.FieldName || '') === 'RankRange');
                if (fr && fr.ValueJson) {
                    const v = JSON.parse(fr.ValueJson);
                    if (v && typeof v.to !== 'undefined') topN = Number(v.to) || 10;
                }
                if (kind === 'sales') return `銷售量 Top${topN}（本）`;
                if (kind === 'borrow') return `借閱量 Top${topN}（本）`;
                return '預覽';
            } else {
                if (kind === 'orders') {
                    const fm = (filters || []).find(f => (f.FieldName || '') === 'Metric');
                    const mv = fm && fm.ValueJson ? (JSON.parse(fm.ValueJson).value || '') : '';
                    return (mv === 'count') ? '訂單筆數（筆）' : '銷售金額（元）';
                }
                if (kind === 'sales') return '銷售量（本）';
                if (kind === 'borrow') return '借閱量（本）';
                return '預覽';
            }
        } catch { return '預覽'; }
    }

    // === 預覽 ===
    let chart;
    async function preview() {
        if (!apiPreview) return;

        // 升級：若頁面未預載 Chart.js，自動載一次
        if (typeof window.Chart === 'undefined') {
            await new Promise((resolve, reject) => {
                const s = document.createElement('script');
                s.src = 'https://cdn.jsdelivr.net/npm/chart.js';
                s.onload = resolve; s.onerror = reject;
                document.head.appendChild(s);
            }).catch(() => console.warn('Chart.js load failed'));
            if (typeof window.Chart === 'undefined') return;
        }

        const cat = getChartCategoryUi();
        const baseKind = ($('baseKind')?.value || 'sales');
        const filts = buildFilters();
        const payload = { Category: cat, BaseKind: baseKind, Filters: filts };

        let labels = [], values = [], title = '';
        try {
            const res = await fetch(apiPreview, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), cache: 'no-store', credentials: 'same-origin' });
            const json = await res.json();

            if (Array.isArray(json.labels) && Array.isArray(json.data)) {
                labels = json.labels;
                values = json.data.map(v => typeof v === 'number' ? v : Number(v) || 0);
                title = json.title || '';
            } else {
                const s = json.series || json.Series || [];
                labels = s.map(p => p.label ?? p.name ?? '');
                values = s.map(p => Number(p.value ?? p.y ?? 0));
                title = json.title || '';
            }
            if (!title || !String(title).trim()) title = computeLegend(cat, baseKind, filts);
        } catch (err) {
            console.error('preview fetch error:', err);
            labels = ['無資料']; values = [0];
            title = computeLegend(cat, baseKind, filts) || '預覽';
        }

        const ctx = $('previewChart')?.getContext('2d'); if (!ctx) return;

        // ★★ 新增：把最新的 labels/values 暫存給匯出用 ★★
        window.__lastPreviewLabels = labels;
        window.__lastPreviewValues = values;

        if (chart) chart.destroy();
        chart = new Chart(ctx, {
            type: (cat === 'pie' ? 'pie' : (cat === 'bar' ? 'bar' : 'line')),
            data: { labels, datasets: [{ label: title, data: values }] },
            options: {
                responsive: true, animation: false, scales: (cat === 'pie' ? {} : { y: { beginAtZero: true } }),plugins: { 'no-data': { text: '無資料' } }
}
        });
    }

    // === Edit 回填（升級：防呆＆只處理 JSON） ===
    async function hydrateFromServer() {
        if (!apiDef || apiDef === '#') {
            console.warn('hydrate skipped: apiDef is empty', apiDef);
            return;
        }
        try {
            const r = await fetch(apiDef, { cache: 'no-store', credentials: 'same-origin' });
            if (!r.ok) { console.warn('hydrate http', r.status); return; }
            const ct = (r.headers.get('content-type') || '').toLowerCase();
            if (!ct.includes('application/json')) {
                console.warn('hydrate content-type not json:', ct);
                return;
            }
            const d = await r.json();

            const cat = (d.Category || d.category || 'line').toLowerCase();
            const bk = (d.BaseKind || d.baseKind || 'sales').toLowerCase();
            if ($('Category')) $('Category').value = cat;
            if ($('baseKind')) $('baseKind').value = bk;

            const filters = d.Filters || d.filters || [];
            const find = (name) => filters.find(f => (f.FieldName || f.fieldName) === name) || null;
            const val = (f) => { try { return JSON.parse(f.ValueJson || f.valueJson || '{}'); } catch { return {} } };

            const dateField = (bk === 'borrow') ? 'BorrowDate' : 'OrderDate';
            const fDate = find(dateField);
            if (fDate) {
                const v = val(fDate);
                if ($('dateFrom') && v.from) $('dateFrom').value = v.from;
                if ($('dateTo') && v.to) $('dateTo').value = v.to;
                const g = (v.gran || 'day').toLowerCase();
                (g === 'month' ? $('gMonth') : g === 'year' ? $('gYear') : $('gDay')).checked = true;
            }

            showSections();
            syncUiByCategoryAndKind();
            await loadCategories();

            if (bk === 'sales') {
                const fc = find('CategoryID'); if (fc) {
                    const picks = (val(fc).values || []).map(String);
                    const el = $('salesCategories'); if (el?.tomselect) el.tomselect.setValue(picks, true);
                }
                const fr = find('RankRange'); if (fr) { const v = val(fr); if (v.from) $('rankFrom').value = String(v.from); if (v.to) $('rankTo').value = String(v.to); $('rankTo').dataset.touched = '1'; $('rankFrom').dataset.touched = '1'; }
            } else if (bk === 'borrow') {
                const fc = find('CategoryID'); if (fc) {
                    const picks = (val(fc).values || []).map(String);
                    const el = $('borrowCategories'); if (el?.tomselect) el.tomselect.setValue(picks, true);
                }
                const fr = find('RankRange'); if (fr) { const v = val(fr); if (v.from) $('rankFromBorrow').value = String(v.from); if (v.to) $('rankToBorrow').value = String(v.to); $('rankToBorrow').dataset.touched = '1'; $('rankFromBorrow').dataset.touched = '1'; }
            }

            await __updateMaxRank('sales');
            await __updateMaxRank('borrow');
            showSections(); syncUiByCategoryAndKind();
            preview();
        } catch (e) {
            console.warn('hydrateFromServer failed:', e);
        }
    }

    // 訂單狀態群組
    function wireStatusCheckboxes() {
        const boxAll = document.getElementById('os_all');
        const boxes = Array.from(document.querySelectorAll('#orderStatusGroup .os'));
        if (!boxAll || boxes.length === 0) return;

        const syncAllState = () => {
            const allChecked = boxes.every(b => b.checked);
            boxAll.checked = allChecked;
            boxAll.indeterminate = !allChecked && boxes.some(b => b.checked);
        };

        boxAll.addEventListener('change', () => {
            boxes.forEach(b => b.checked = boxAll.checked);
            syncAllState(); preview();
        });
        boxes.forEach(b => b.addEventListener('change', () => { syncAllState(); preview(); }));

        boxes.forEach(b => b.checked = false);
        const done = document.getElementById('os_3');
        if (done) done.checked = true;
        syncAllState();
    }

    // ===== 排行上限 =====
    function __getMaxRankFromSelect_TotalOptions(selectEl) {
        if (!selectEl) return 0;
        return Array.from(selectEl.options || []).filter(o => o.value !== '').length;
    }
    function __fillRankSelects(fromId, toId, max, opts = {}) {
        const { forceToMax = false } = opts;

        const fromEl = document.getElementById(fromId);
        const toEl = document.getElementById(toId);
        if (!fromEl || !toEl) return;

        const oldFrom = Number(fromEl.value || 1);
        const oldTo = Number(toEl.value || 10);

        const upper = Math.max(1, Number(max) || 1);
        const refill = (el) => { el.innerHTML = ''; for (let i = 1; i <= upper; i++) el.add(new Option(String(i), String(i))); };
        refill(fromEl); refill(toEl);

        const userTouchedTo = !!toEl.dataset.touched;
        const shouldMax = forceToMax || !userTouchedTo;

        fromEl.value = String(Math.min(Math.max(1, oldFrom || 1), upper));
        toEl.value = shouldMax ? String(upper) : String(Math.min(Math.max(1, oldTo || Math.min(10, upper)), upper));
    }
    async function __updateMaxRank(kind, opts = {}) {
        const isSales = kind === 'sales';
        const selEl = document.getElementById(isSales ? 'salesCategories' : 'borrowCategories');

        // 本地先用「非空選項總數」填充，避免已選數造成假下降
        const totalNonEmpty = __getMaxRankFromSelect_TotalOptions(selEl);
        if (totalNonEmpty > 0) {
            if (isSales) __fillRankSelects('rankFrom', 'rankTo', totalNonEmpty, opts);
            else __fillRankSelects('rankFromBorrow', 'rankToBorrow', totalNonEmpty, opts);
        }

        // 呼叫後端覆蓋（成功才更新）
        const apiMax = (window.__API_MAXRANK__)
            || (typeof apiCats === 'string' && apiCats ? apiCats.replace(/Categories\b/i, 'MaxRank') : null);
        if (!apiMax) return;

        try {
            const currentFilters = (typeof buildFilters === 'function')
                ? buildFilters().filter(f => (f.FieldName || '') !== 'RankRange')
                : [];
            const payload = { BaseKind: kind, Filters: currentFilters };

            const res = await fetch(apiMax, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), credentials: 'same-origin' });
            if (!res.ok) { console.warn('MaxRank API HTTP', res.status); return; }

            const j = await res.json();
            const m = Number(j?.maxRank || 0);
            if (m > 0) {
                if (isSales) __fillRankSelects('rankFrom', 'rankTo', m, opts);
                else __fillRankSelects('rankFromBorrow', 'rankToBorrow', m, opts);
            }
        } catch (e) {
            console.warn('MaxRank API error:', e);
        }
    }

    // === Wire ===
    function wire() {
        document.addEventListener('submit', (ev) => {
            const form = ev.target;
            if (form && form.querySelector('#FiltersJson')) {
                $('FiltersJson').value = JSON.stringify(buildFilters());
            }
        });

        const handleUiChange = () => { showSections(); syncUiByCategoryAndKind(); preview(); };

        ['Category', 'category', 'gDay', 'gMonth', 'gYear', 'salesCategories', 'borrowCategories', 'priceRange',
            'rankFrom', 'rankTo', 'rankFromBorrow', 'rankToBorrow', 'publishDecade',
            'mAmount', 'mCount', 'ordAmtMin', 'ordAmtMax'
        ].forEach(id => {
            const el = $(id);
            if (el) el.addEventListener('change', handleUiChange);
        });

        const elSalesCats = document.getElementById('salesCategories');
        const elBorrowCats = document.getElementById('borrowCategories');
        if (elSalesCats) {
            elSalesCats.addEventListener('change', async () => {
                await __updateMaxRank('sales', { forceToMax: true });
                preview();
            });
        }
        if (elBorrowCats) {
            elBorrowCats.addEventListener('change', async () => {
                await __updateMaxRank('borrow', { forceToMax: true });
                preview();
            });
        }

        const elDateFrom = document.getElementById('dateFrom');
        const elDateTo = document.getElementById('dateTo');
        ['change', 'input', 'blur'].forEach(evt => {
            elDateFrom?.addEventListener(evt, __onDateChange);
            elDateTo?.addEventListener(evt, __onDateChange);
        });

        const elBaseKind = $('baseKind');
        if (elBaseKind) elBaseKind.addEventListener('change', async () => {
            await loadCategories();
            await __updateMaxRank('sales', { forceToMax: true });
            await __updateMaxRank('borrow', { forceToMax: true });
            handleUiChange();
        });

        document.querySelectorAll('[data-chart]').forEach(btn => {
            btn.addEventListener('click', () => setTimeout(() => { showSections(); syncUiByCategoryAndKind(); preview(); }, 0));
        });

        $('btnPreview')?.addEventListener('click', preview);

        // 使用者調整排行 → 打上 touched，避免自動覆蓋
        ['rankFrom', 'rankTo', 'rankFromBorrow', 'rankToBorrow'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.addEventListener('change', () => { el.dataset.touched = '1'; });
        });
    }

    // === init ===
    (async function init() {
        const today = new Date(); const from = new Date(); from.setDate(today.getDate() - 29);
        if ($('dateFrom')) $('dateFrom').value ||= from.toISOString().slice(0, 10);
        if ($('dateTo')) $('dateTo').value ||= today.toISOString().slice(0, 10);

        await loadCategories();

        try {
            await __ensureTomSelect();
            __enhanceSearchableSelect('salesCategories');
            __enhanceSearchableSelect('borrowCategories');
        } catch (e) {
            console.warn('Tom Select 初始化失敗，改用原生下拉：', e);
        }

        genPrice();
        genDecades();

        showSections();
        syncUiByCategoryAndKind();
        wire();
        wireStatusCheckboxes();

        // 初次：把排行迄設為上限
        await __updateMaxRank('sales', { forceToMax: true });
        await __updateMaxRank('borrow', { forceToMax: true });

        if (apiDef) {
            await hydrateFromServer();
            try {
                const elS = $('salesCategories'); if (elS?.tomselect) elS.tomselect.setValue([...elS.selectedOptions].map(o => o.value), true);
                const elB = $('borrowCategories'); if (elB?.tomselect) elB.tomselect.setValue([...elB.selectedOptions].map(o => o.value), true);
            } catch { }
            showSections(); syncUiByCategoryAndKind();
        }

        preview();
    })();

    // ====== ★ 新增：攔截你原本「匯出」按鈕 → 同頁開 Modal、送出 ======
    (function wireExportModal() {
        // 嘗試常見 selector；若你的按鈕不是這些，直接把 selector 加到陣列即可
        const SELECTORS = ['#btnExport', '.js-export', '[data-action="export"]'];
        let exportBtn = null;
        for (const s of SELECTORS) { const el = document.querySelector(s); if (el) { exportBtn = el; break; } }
        const modalEl = document.getElementById('exportModal');
        if (!exportBtn || !modalEl || !window.bootstrap || !bootstrap.Modal) return;
        const modal = new bootstrap.Modal(modalEl);

        exportBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            modal.show();
        });

        const form = document.getElementById('exportForm');
        form?.addEventListener('submit', async (e) => {
            e.preventDefault();
            const emailInput = document.getElementById('exportEmail');
            const email = (emailInput?.value || '').trim();
            const ok = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
            if (!ok) { emailInput?.classList.add('is-invalid'); return; }
            emailInput?.classList.remove('is-invalid');

            const format = (document.querySelector('input[name="format"]:checked')?.value || 'xlsx').toLowerCase();
            const Category = (typeof getChartCategoryUi === 'function') ? getChartCategoryUi() : 'line';
            const BaseKind = document.getElementById('baseKind')?.value || '';
            const Granularity = document.querySelector('input[name="gran"]:checked')?.value
                || document.getElementById('granularity')?.value || '';
            const ValueMetric = document.getElementById('valueMetric')?.value || '';
            const Title = document.getElementById('title')?.value || '';
            const SubTitle = document.getElementById('subTitle')?.value || '';

            const Labels = Array.isArray(window.__lastPreviewLabels) ? window.__lastPreviewLabels : [];
            const Values = Array.isArray(window.__lastPreviewValues) ? window.__lastPreviewValues : [];

            const dto = { Category, BaseKind, Granularity, ValueMetric, Title, SubTitle, Labels, Values, To: email };

            const url = (format === 'pdf')
                ? '/ReportMail/ReportExport/SendPdf'
                : '/ReportMail/ReportExport/SendExcel';

            const submitBtn = form.querySelector('button[type="submit"]');
            submitBtn.disabled = true;
            try {
                const res = await fetch(url, {
                    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(dto)
                });
                if (!res.ok) {
                    const t = await res.text();
                    alert('匯出失敗：' + t);
                    return;
                }
                alert('已寄出到：' + email);
                modal.hide();
            } catch (err) {
                console.error(err);
                alert('匯出時發生錯誤');
            } finally {
                submitBtn.disabled = false;
            }
        });
    })();

})();
