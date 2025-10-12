// @ts-nocheck
(function () {
    function __narrowPage() {
        // 1) 注入樣式：限制寬度並置中
        if (!document.getElementById('app-narrow-css')) {
            const s = document.createElement('style');
            s.id = 'app-narrow-css';
            s.textContent = `
      .app-narrow { max-width: 1100px; margin: 0 auto; padding-left: 12px; padding-right: 12px; }
    `;
            document.head.appendChild(s);
        }
        // 2) 嘗試把 class 套在常見外層
        const targets = [
            document.querySelector('main'),
            document.querySelector('.container-fluid'),
            document.querySelector('.container'),
            document.body.firstElementChild
        ];
        const host = targets.find(Boolean);
        if (host) host.classList.add('app-narrow');
    }
    // 在 init() 一開始呼叫
    __narrowPage();

    const $ = (id) => document.getElementById(id);

    // === API 來源（維持你原本的注入方式） ===
    const apiPreview = window.__API_PREVIEW__
        || document.querySelector('script[src*="report-def-form.js"]')?.dataset?.apiPreview
        || (typeof previewUrl !== "undefined" ? previewUrl : null) || "@previewUrl";
    const apiCats = window.__API_CATS__
        || (typeof catsUrl !== "undefined" ? catsUrl : null)
        || "/ReportMail/Lookup/Categories"; // ← 安全預設：走 Areas/ReportMail 既有路由
    const apiDef = window.__DEF_URL__ || null; // 只有 Edit 頁有

    // 出版年功能旗標（暫關）
    const FEATURE_PUBLISH_DECADE = false;

    // ===== Tom Select：動態載入 & 單一框樣式 =====
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
/* 單一框外觀（貼近 Bootstrap .form-select），不顯示外層箭頭 */
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
/* 多選：單行逗號分隔（不要彩色小籤） */
select[multiple].form-select + .ts-wrapper.form-select .ts-control{
  overflow:hidden; white-space:nowrap; text-overflow:ellipsis;
}
select[multiple].form-select + .ts-wrapper.form-select .ts-control .item{
  background:transparent; border:0; padding:0; margin:0 .25rem 0 0;
}
select[multiple].form-select + .ts-wrapper.form-select .ts-control .item ~ .item::before{
  content:"、"; margin-right:.25rem;
}
select[multiple].form-select + .ts-wrapper.form-select .ts-control .remove{ display:none !important; }
/* 下拉面板與搜尋框 */
.ts-dropdown{
border:1px solid var(--bs-border-color,#dee2e6);
border-radius:.375rem; box-shadow:0 .5rem 1rem rgba(0,0,0,.15);
margin-top:.25rem;
max-height: 24rem;    /* 顯示更多行 */
overflow: auto;       /* 允許滾動（關鍵） */
 }
 /* Tom Select 預設讓 .ts-dropdown-content 負責捲動；這裡同步放大高度 */
.ts-dropdown .ts-dropdown-content{
max-height: inherit;
overflow-y: auto;
 }
.ts-dropdown .ts-dropdown-input{
  margin:0; padding:.5rem .75rem;
  border:0; border-bottom:1px solid var(--bs-border-color,#dee2e6);
  border-radius:0;
}
.ts-dropdown .option{ padding:.375rem .75rem; }
.ts-dropdown .option.active{ background: var(--bs-primary-bg-subtle,#e7f1ff); color:inherit; }
/* 驗證/禁用沿用 select 類別 */
select.is-invalid + .ts-wrapper .ts-control{ border-color: var(--bs-danger,#dc3545); box-shadow:0 0 0 .25rem rgba(220,53,69,.25); }
select.is-valid   + .ts-wrapper .ts-control{ border-color: var(--bs-success,#198754); box-shadow:0 0 0 .25rem rgba(25,135,84,.25); }
select:disabled   + .ts-wrapper .ts-control{ background-color: var(--bs-secondary-bg,#e9ecef); opacity:.65; pointer-events:none; }
`;
        document.head.appendChild(style);

    }

    // 可搜尋下拉初始化（按下不動作、放開時依停留選項做加入/取消）
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
            hideSelected: false,                 // 已選仍顯示，才能被再次取消
            maxOptions: 1000,
            sortField: { field: 'text', direction: 'asc' },
            render: { option: (d, esc) => `<div>${esc(d.text || (d.value === '' ? '（全部）' : ''))}</div>` },
            onInitialize() { this.wrapper.classList.add('form-select'); }
        }, extra || {}));

        const ddContent = ts.dropdown_content || ts.dropdown;
        if (!ddContent) return;

        let armed = false;

        // 攔 mousedown（捕獲）→ 不讓 TS 在按下就處理；改等 mouseup
        ddContent.addEventListener('mousedown', (evt) => {
            if (evt.button !== 0) return; // 左鍵
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

            // 以當下 hover 的選項為準；若沒有，嘗試用事件目標
            let optEl = ts.activeOption;
            if (!optEl) {
                const maybe = evt.target && (evt.target.closest ? evt.target.closest('.ts-dropdown .option[data-value]') : null);
                if (maybe) optEl = maybe;
            }
            if (!optEl) return;

            const value = optEl.getAttribute('data-value');

            // 空值（「全部」）→ 清空
            if (value === '') {
                ts.clear();
                if (!isMulti) ts.close();
                ts.refreshOptions(false);
                return;
            }

            if (!isMulti) {
                // 單選：放開在「目前已選」→ 清空；否則選取該值
                if (ts.items.length && ts.items[0] === value) {
                    ts.clear();
                    ts.close();
                } else {
                    ts.setValue(value, true);
                    ts.close();
                }
                ts.refreshOptions(false);
                return;
            }

            // 多選：toggle
            if (ts.items.includes(value) || optEl.classList.contains('selected')) {
                ts.removeItem(value);
            } else {
                ts.addItem(value);
            }
            ts.refreshOptions(false);
            ts.setCaret(ts.items.length);
        };

        el.__rfDocMouseup = handleDocMouseup;
        document.addEventListener('mouseup', handleDocMouseup, true);
    }

    // ===== 你的現有 UI/邏輯 =====
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
    // 讓高頻事件（input/blur）彙整成一次刷新
    function __debounce(fn, delay = 250) {
        let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); };
    }

    // 日期變更後要做的事：重抓分類、重算排行、刷新 UI/預覽
    const __onDateChange = __debounce(async () => {
        try {
            await loadCategories();
            await __updateMaxRank('sales');
            await __updateMaxRank('borrow');
            if (typeof showSections === 'function') showSections();
            if (typeof syncUiByCategoryAndKind === 'function') syncUiByCategoryAndKind();
            if (typeof preview === 'function') preview();
        } catch (err) {
            console.warn('date change refresh failed:', err);
        }
    }, 250);


    // === Data sources ===
    function applyCategoryOptions(selId, items) {
        const sel = (typeof selId === 'string') ? document.getElementById(selId) : selId;
        if (!sel) return;

        // 1) 資料清洗：去空/去重（保留你的規則）
        const seen = new Set();
        const cleaned = (Array.isArray(items) ? items : [])
            .filter(x => x && x.value !== null && x.value !== undefined && String(x.value).trim() !== '' && String(x.value) !== '0')
            .filter(x => { const k = String(x.value); if (seen.has(k)) return false; seen.add(k); return true; })
            .map(x => ({ value: String(x.value), text: String(x.text ?? '') }));

        // 2) 若已有 Tom Select 實例，先記住選取值，然後**乾淨地摧毀**
        let keep = [];
        if (sel.tomselect) {
            try { keep = sel.tomselect.items.filter(v => v !== ''); } catch { }
            try { sel.tomselect.destroy(); } catch { }
        }

        // 3) 重建原生 <option>
        sel.innerHTML = '';
        sel.add(new Option('（全部）', '')); // 統一的空值
        cleaned.forEach(o => sel.add(new Option(o.text, o.value)));

        // 4) 重建 Tom Select（用你原本的初始化函式）
        try {
            if (typeof __enhanceSearchableSelect === 'function') {
                __enhanceSearchableSelect(selId);
            }
        } catch (e) {
            console.warn('Tom Select 初始化失敗，先使用原生下拉：', e);
        }

        // 5) 還原選取（僅還原仍存在於新清單的）
        const exists = new Set(Array.from(sel.options).map(o => o.value));
        keep = (keep || []).filter(v => exists.has(v));
        if (sel.tomselect) {
            if (keep.length) sel.tomselect.setValue(keep, true);
            else sel.tomselect.clear(true);
        } else {
            // 尚未有 TS，就用原生方式還原
            keep.forEach(v => { const o = Array.from(sel.options).find(o => o.value === v); if (o) o.selected = true; });
        }
    }

    async function loadCategories(opts = {}) {
        if (!apiCats) return;
        const { reinit = true } = opts;

        const fetchList = async (kind) => {
            try {
                const url = new URL(apiCats, window.location.origin);
                url.searchParams.set('baseKind', kind);
                const df = $('dateFrom')?.value;
                const dt = $('dateTo')?.value;
                if (df) url.searchParams.set('start', df);
                if (dt) url.searchParams.set('end', dt);

                // 改：帶上 cookie 並關閉快取
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

    // === Filters ===
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
            const cats = Array.from($('salesCategories')?.selectedOptions || []).map(o => Number(o.value)).filter(v => !!v);
            if (cats.length) filters.push({ FieldName: 'CategoryID', DataType: 'select', Operator: 'in', ValueJson: JSON.stringify({ values: cats }) });
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
            const cats = Array.from($('borrowCategories')?.selectedOptions || []).map(o => Number(o.value)).filter(v => !!v);
            if (cats.length) filters.push({ FieldName: 'CategoryID', DataType: 'select', Operator: 'in', ValueJson: JSON.stringify({ values: cats }) });

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

    // 動態推導圖例（後端沒給 title 時）
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

    // === Chart Preview ===
    let chart;
    async function preview() {
        if (!apiPreview) return;

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
        if (chart) chart.destroy();
        chart = new Chart(ctx, {
            type: (cat === 'pie' ? 'pie' : (cat === 'bar' ? 'bar' : 'line')),
            data: { labels, datasets: [{ label: title, data: values }] },
            options: { responsive: true, animation: false, scales: (cat === 'pie' ? {} : { y: { beginAtZero: true } }) }
        });
    }

    // === Hydrate（Edit 回填） ===
    async function hydrateFromServer() {
        if (!apiDef) return;
        const r = await fetch(apiDef, { cache: 'no-store', credentials: 'same-origin' }); if (!r.ok) return;
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
            const fc = find('CategoryID');
            if (fc) {
                const picks = (val(fc).values || []).map(Number);
                const el = $('salesCategories');
                Array.from(el.options).forEach(o => o.selected = picks.includes(Number(o.value)));
                if (el?.tomselect) el.tomselect.setValue(picks, true);
            }
            const fp = find('SalePrice'); if (fp) { const v = val(fp); if (v.min != null && v.max != null) $('priceRange').value = `${v.min}-${v.max}`; }
            const fr = find('RankRange'); if (fr) { const v = val(fr); if (v.from) $('rankFrom').value = String(v.from); if (v.to) $('rankTo').value = String(v.to); }
        }
        if (bk === 'borrow') {
            const fc = find('CategoryID');
            if (fc) {
                const picks = (val(fc).values || []).map(Number);
                const el = $('borrowCategories');
                Array.from(el.options).forEach(o => o.selected = picks.includes(Number(o.value)));
                if (el?.tomselect) el.tomselect.setValue(picks, true);
            }
            if (FEATURE_PUBLISH_DECADE) {
                const fd = find('PublishDecade');
                if (fd) {
                    const v = val(fd);
                    if (v.fromYear && v.toYear) $('publishDecade').value = `${v.fromYear}-${v.toYear}`;
                }
            }
            const fr = find('RankRange'); if (fr) { const v = val(fr); if (v.from) $('rankFromBorrow').value = String(v.from); if (v.to) $('rankToBorrow').value = String(v.to); }
        }

        // 回填完成後，動態重算可排行上限
        await __updateMaxRank('sales');
        await __updateMaxRank('borrow');
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

        // 預設只勾「已完成」
        boxes.forEach(b => b.checked = false);
        const done = document.getElementById('os_3');
        if (done) done.checked = true;
        syncAllState();
    }

    // ===== 排行上限：前端+（可選）後端 =====
    function __getMaxRankFromSelect(selectEl) {
        if (!selectEl) return 0;
        const picked = Array.from(selectEl.selectedOptions || []).filter(o => o.value !== '');
        if (picked.length > 0) return picked.length;
        return Array.from(selectEl.options || []).filter(o => o.value !== '').length;
    }
    // 依 max 重新填 rank，下方新增 opts.forceToMax 來控制是否把「迄」強制設為最大值
    function __fillRankSelects(fromId, toId, max, opts = {}) {
        const { forceToMax = false } = opts;

        const fromEl = document.getElementById(fromId);
        const toEl = document.getElementById(toId);
        if (!fromEl || !toEl) return;

        const oldFrom = Number(fromEl.value || 1);
        const oldTo = Number(toEl.value || 10);

        const upper = Math.max(1, max);
        const refill = (el) => { el.innerHTML = ''; for (let i = 1; i <= upper; i++) el.add(new Option(String(i), String(i))); };
        refill(fromEl); refill(toEl);

        fromEl.value = String(Math.min(Math.max(1, oldFrom || 1), upper));
        // ★ 關鍵：如果指定 forceToMax，就把「迄」設到最大；否則維持舊值（超出則壓回上限）
        toEl.value = forceToMax ? String(upper) : String(Math.min(Math.max(1, oldTo || Math.min(10, upper)), upper));
    }
    // 重新計算並套用「可排行到幾名」；opts 可帶 { forceToMax:true }
    async function __updateMaxRank(kind, opts = {}) {
        const isSales = kind === 'sales';
        const selEl = document.getElementById(isSales ? 'salesCategories' : 'borrowCategories');

        // 先用前端估算（勾選數量或全部類別數）
        const localMax = __getMaxRankFromSelect(selEl);
        if (isSales) __fillRankSelects('rankFrom', 'rankTo', localMax, opts);
        else __fillRankSelects('rankFromBorrow', 'rankToBorrow', localMax, opts);

        // 若提供後端 API，再覆蓋（含日期等條件）
        const apiMax = (window.__API_MAXRANK__)
            || (typeof apiCats === 'string' && apiCats ? apiCats.replace(/Categories\b/i, 'MaxRank') : null);
        if (!apiMax) return;

        try {
            const currentFilters = (typeof buildFilters === 'function')
                ? buildFilters().filter(f => (f.FieldName || '') !== 'RankRange')
                : [];
            const payload = { BaseKind: kind, Filters: currentFilters };

            const res = await fetch(apiMax, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), credentials: 'same-origin' });
            if (!res.ok) return;
            const j = await res.json();
            const m = Number(j?.maxRank || 0);
            if (m > 0) {
                if (isSales) __fillRankSelects('rankFrom', 'rankTo', m, opts);
                else __fillRankSelects('rankFromBorrow', 'rankToBorrow', m, opts);
            }
        } catch { }
    }

    // === Wire ===
    function wire() {
        // submit：塞 FiltersJson
        document.addEventListener('submit', (ev) => {
            const form = ev.target;
            if (form && form.querySelector('#FiltersJson')) {
                $('FiltersJson').value = JSON.stringify(buildFilters());
            }
        });

        // 即時預覽觸發
        const handleUiChange = () => { showSections(); syncUiByCategoryAndKind(); preview(); };

        ['Category', 'category', 'gDay', 'gMonth', 'gYear', 'salesCategories', 'borrowCategories', 'priceRange',
            'rankFrom', 'rankTo', 'rankFromBorrow', 'rankToBorrow', 'publishDecade',
            'mAmount', 'mCount', 'ordAmtMin', 'ordAmtMax'
        ].forEach(id => {
            const el = $(id);
            if (el) el.addEventListener('change', handleUiChange);
        });

        // —— 會影響排行上限的欄位 ——

        // 書籍種類變動：把「迄」強制設為最大
        const elSalesCats = document.getElementById('salesCategories');
        const elBorrowCats = document.getElementById('borrowCategories');
        if (elSalesCats) {
            elSalesCats.addEventListener('change', () => {
                __updateMaxRank('sales', { forceToMax: true });
            });
        }
        if (elBorrowCats) {
            elBorrowCats.addEventListener('change', () => {
                __updateMaxRank('borrow', { forceToMax: true });
            });
        }

        // 日期變動：改為監聽 change + input + blur，並用 debounce 彙整
        const elDateFrom = document.getElementById('dateFrom');
        const elDateTo = document.getElementById('dateTo');
        ['change', 'input', 'blur'].forEach(evt => {
            elDateFrom?.addEventListener(evt, __onDateChange);
            elDateTo?.addEventListener(evt, __onDateChange);
        });

        const elBaseKind = $('baseKind');
        if (elBaseKind) elBaseKind.addEventListener('change', async () => {
            await loadCategories();
            await __updateMaxRank('sales');
            await __updateMaxRank('borrow');
            handleUiChange();
        });

        // 圖型切換按鈕（Edit 可能存在）
        document.querySelectorAll('[data-chart]').forEach(btn => {
            btn.addEventListener('click', () => setTimeout(() => { showSections(); syncUiByCategoryAndKind(); preview(); }, 0));
        });

        // 預覽按鈕
        $('btnPreview')?.addEventListener('click', preview);
    }

    // === init ===
    (async function init() {
        // 預設日期 30 天
        const today = new Date(); const from = new Date(); from.setDate(today.getDate() - 29);
        if ($('dateFrom')) $('dateFrom').value ||= from.toISOString().slice(0, 10);
        if ($('dateTo')) $('dateTo').value ||= today.toISOString().slice(0, 10);

        await loadCategories({ reinit: false });

        try {
            await __ensureTomSelect();
            __enhanceSearchableSelect('salesCategories');
            __enhanceSearchableSelect('borrowCategories');
        } catch (e) {
            console.warn('Tom Select 初始化失敗，改用原生下拉：', e);
        }

        genPrice();
        genDecades(); // 即使旗標關閉也安全

        showSections();
        syncUiByCategoryAndKind();
        wire();
        wireStatusCheckboxes();

        // 初次動態排行上限（取代原本固定 1..100）
        await __updateMaxRank('sales');
        await __updateMaxRank('borrow');

        // Edit 頁：載回已存
        if (apiDef) {
            await hydrateFromServer();
            try {
                const elS = $('salesCategories'); if (elS?.tomselect) elS.tomselect.setValue([...elS.selectedOptions].map(o => o.value), true);
                const elB = $('borrowCategories'); if (elB?.tomselect) elB.tomselect.setValue([...elB.selectedOptions].map(o => o.value), true);
            } catch { }
            showSections(); syncUiByCategoryAndKind();
        }

        // 首次預覽
        preview();
    })();
})();
