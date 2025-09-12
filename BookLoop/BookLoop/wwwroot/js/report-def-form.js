// @ts-nocheck
(function () {
    const $ = (id) => document.getElementById(id);

    // 從頁面注入的 API（Create/Edit 兩頁記得用 Scripts 區塊設好 window.__API_*__）
    const apiPreview = window.__API_PREVIEW__ || document.querySelector('script[src*="report-def-form.js"]')?.dataset?.apiPreview || (typeof previewUrl !== "undefined" ? previewUrl : null) || "@previewUrl";
    const apiCats = window.__API_CATS__ || (typeof catsUrl !== "undefined" ? catsUrl : null) || "@catsUrl";
    const apiDef = window.__DEF_URL__ || null; // 只有 Edit 頁有

    // ===== Feature flag：出版年份目前 DB 未做，關閉：不顯示且不送出 =====
    const FEATURE_PUBLISH_DECADE = false;

    // 取用目前「圖表種類」：支援 Create 的 <select id="Category"> 與 Edit 可能使用的 hidCategory / data-chart 按鈕
    function getChartCategoryUi() {
        const sel = $('Category') || $('category');
        if (sel && sel.value) return sel.value.toLowerCase();
        const hid = $('hidCategory');
        if (hid && hid.value) return hid.value.toLowerCase();
        const btn = document.querySelector('[data-chart].btn-primary,[data-chart].active');
        if (btn) return String(btn.getAttribute('data-chart')).toLowerCase();
        return 'line';
    }

    // ==== UI helpers ====
    function isBarPie() {
        const cat = getChartCategoryUi();
        return cat === 'bar' || cat === 'pie';
    }

    // 只負責「區塊有/無」：不處理訂單 option、粒度/標題的細節（下方 syncUiByCategoryAndKind 處理）
    function showSections() {
        const cat = getChartCategoryUi();
        const kind = ($('baseKind')?.value || 'sales').toLowerCase();

        // 折線圖才顯示 orders 區塊
        if ($('section-orders')) $('section-orders').style.display = (cat === 'line' && kind === 'orders') ? '' : 'none';

        // 銷售/借閱區塊：line 也可顯示（線圖的 sales/borrow 也會有日期/分類/價位）
        if ($('section-sales')) $('section-sales').style.display = (kind === 'sales') ? '' : 'none';
        if ($('section-borrow')) $('section-borrow').style.display = (kind === 'borrow') ? '' : 'none';

        // 排行區塊：只在 bar/pie 顯示
        if ($('rank-sales')) $('rank-sales').style.display = (isBarPie() && kind === 'sales') ? '' : 'none';
        if ($('rank-borrow')) $('rank-borrow').style.display = (isBarPie() && kind === 'borrow') ? '' : 'none';
    }

    // 依「圖表種類 + 基礎報表」同步 UI 細節：隱藏粒度、改標題、隱藏訂單 option、出版年行為
    function syncUiByCategoryAndKind() {
        const cat = getChartCategoryUi();                    // line | bar | pie
        const kindSel = $('baseKind');                        // Create 一定有；Edit 可能沒有
        const kind = (kindSel?.value || '').toLowerCase();
        const isBP = (cat === 'bar' || cat === 'pie');

        // 2) bar/pie 隱藏粒度 + 改標題文字
        const gran = $('granGroup');                          // 裝著「日/月/年」radio 的容器
        const dateLabel = $('dateLabel');                     // 標題文字：日期區間與粒度 / 日期區間
        if (gran) gran.style.display = isBP ? 'none' : '';
        if (dateLabel) dateLabel.textContent = isBP ? '日期區間' : '日期區間與粒度';

        // 3) bar/pie 隱藏「訂單」選項；若正好選到訂單，切回 sales/borrow
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
                        // 觸發 change 讓既有 showSections/preview 跑起來
                        kindSel.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                }
            }
        }

        // 1) 出版年份列（借閱+bar/pie 時才顯示；旗標關閉＝永遠不顯示）
        const decRow = $('publishDecadeRow');
        if (decRow) {
            const showDecade = FEATURE_PUBLISH_DECADE && isBP && (kind === 'borrow');
            decRow.style.display = showDecade ? '' : 'none';
        }
    }

    // ==== data sources ====
    async function loadCategories() {
        if (!apiCats) return;
        const res = await fetch(apiCats, { cache: 'no-store' });
        const list = await res.json();
        const fill = (selId) => {
            const sel = $(selId); if (!sel) return;
            sel.innerHTML = '';
            sel.add(new Option('（全部）', ''));
            list.forEach(x => sel.add(new Option(x.text, x.value)));
        };
        fill('salesCategories'); fill('borrowCategories');
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
        const end = now - (now % 10); // 本年代末（含今年對齊）
        sel.innerHTML = '';
        sel.add(new Option('（全部）', ''));
        for (let y = 1901; y <= end; y += 10) {
            const to = y + 9;
            sel.add(new Option(`${y}~${to}年`, `${y}-${to}`));
        }
    }

    // ==== build filters ====
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

            // 出版年份：目前關閉，不送到後端（未來要開只需把 FEATURE_PUBLISH_DECADE 改 true）
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
        else { // orders (only line)
            const metric = $('mCount')?.checked ? 'count' : 'amount';
            filters.push({ FieldName: 'Metric', ValueJson: JSON.stringify({ value: metric }) });

            //訂單狀態（包含式）：全選＝不送；全不勾＝送空集合（回空結果）
            const group = document.getElementById('orderStatusGroup');
            const all = document.getElementById('os_all');
            if (group && all) {
                const picks = Array.from(group.querySelectorAll('.os:checked')).map(cb => Number(cb.dataset.val));
                if (!all.checked) { // 不是全選時才送（包含式）
                    filters.push({
                        FieldName: 'OrderStatus', Operator: 'in',
                        ValueJson: JSON.stringify({ values: picks })
                    });
                }
            }

            const min = $('ordAmtMin')?.value, max = $('ordAmtMax')?.value;
            if (min || max) filters.push({ FieldName: 'OrderAmount', DataType: 'number', Operator: 'between', ValueJson: JSON.stringify({ min: Number(min || 0), max: Number(max || 999999) }) });
        }
        return filters;
    }

    // ★ 動態推導圖例（後端沒給 title 時的後備）
    function computeLegend(cat, baseKind, filters, json) {
        try {
            const kind = (baseKind || '').toLowerCase();
            if (cat === 'bar' || cat === 'pie') {
                // 取 RankRange.to 當 TopN（沒有就預設 10）
                let topN = 10;
                const fr = (filters || []).find(f => (f.FieldName || '') === 'RankRange');
                if (fr && fr.ValueJson) {
                    const v = JSON.parse(fr.ValueJson);
                    if (v && typeof v.to !== 'undefined') topN = Number(v.to) || 10;
                }
                if (kind === 'sales') return `銷售量 Top${topN}（本）`;
                if (kind === 'borrow') return `借閱量 Top${topN}（本）`;
                return '預覽';
            } else { // line
                if (kind === 'orders') {
                    // 看 Metric：count/amount
                    const fm = (filters || []).find(f => (f.FieldName || '') === 'Metric');
                    const mv = fm && fm.ValueJson ? (JSON.parse(fm.ValueJson).value || '') : '';
                    return (mv === 'count') ? '訂單筆數（筆）' : '銷售金額（元）';
                }
                if (kind === 'sales') return '銷售量（本）';
                if (kind === 'borrow') return '借閱量（本）';
                return '預覽';
            }
        } catch {
            return '預覽';
        }
    }

    // ==== preview (Chart.js) ====
    let chart;
    async function preview() {
        if (!apiPreview) return;

        const cat = getChartCategoryUi();
        const baseKind = ($('baseKind')?.value || 'sales');
        const filts = buildFilters();

        const payload = { Category: cat, BaseKind: baseKind, Filters: filts };

        let labels = [], values = [], title = ''; // ★ 預設不給字，由下方決定
        try {
            const res = await fetch(apiPreview, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
                cache: 'no-store'
            });
            const json = await res.json();

            // 相容兩種回傳格式
            if (Array.isArray(json.labels) && Array.isArray(json.data)) {
                labels = json.labels;
                // 強制轉數字，避免字串數字
                values = json.data.map(v => typeof v === 'number' ? v : Number(v) || 0); // ★
                title = json.title || ''; // ★ 等一下再用 computeLegend 補
            } else {
                const s = json.series || json.Series || [];
                labels = s.map(p => p.label ?? p.name ?? '');
                values = s.map(p => Number(p.value ?? p.y ?? 0)); // ★ 轉成 Number
                title = json.title || ''; // ★
            }

            // ★ 若後端沒有 title，用前端推導的 legend
            if (!title || !String(title).trim()) {
                title = computeLegend(cat, baseKind, filts);
            }
        } catch (err) {
            console.error('preview fetch error:', err);
            labels = ['無資料']; values = [0];
            title = computeLegend(cat, baseKind, filts) || '預覽'; // ★ 就算失敗也給個合理圖例
        }

        const ctx = $('previewChart')?.getContext('2d'); if (!ctx) return;
        if (chart) chart.destroy();
        chart = new Chart(ctx, {
            type: (cat === 'pie' ? 'pie' : (cat === 'bar' ? 'bar' : 'line')),
            data: { labels, datasets: [{ label: title, data: values }] }, // ★ 圖例名稱 = title（動態）
            options: {
                responsive: true,
                animation: false,
                scales: (cat === 'pie' ? {} : { y: { beginAtZero: true } })
            }
        });
    }

    // ==== hydrate for Edit ====
    async function hydrateFromServer() {
        if (!apiDef) return;
        const r = await fetch(apiDef, { cache: 'no-store' }); if (!r.ok) return;
        const d = await r.json();

        const cat = (d.Category || d.category || 'line').toLowerCase();
        const bk = (d.BaseKind || d.baseKind || 'sales').toLowerCase();
        if ($('Category')) $('Category').value = cat;
        if ($('baseKind')) $('baseKind').value = bk;

        const filters = d.Filters || d.filters || [];
        const find = (name) => filters.find(f => (f.FieldName || f.fieldName) === name) || null;
        const val = (f) => { try { return JSON.parse(f.ValueJson || f.valueJson || '{}'); } catch { return {} } };

        // 日期
        const dateField = (bk === 'borrow') ? 'BorrowDate' : 'OrderDate';
        const fDate = find(dateField);
        if (fDate) { const v = val(fDate); if (v.from) $('dateFrom').value = v.from; if (v.to) $('dateTo').value = v.to; const g = (v.gran || 'day').toLowerCase(); (g === 'month' ? $('gMonth') : g === 'year' ? $('gYear') : $('gDay')).checked = true; }

        // 共用顯示邏輯
        showSections();
        syncUiByCategoryAndKind();

        // sales
        if (bk === 'sales') {
            const fc = find('CategoryID'); if (fc) { const picks = (val(fc).values || []).map(Number); Array.from($('salesCategories').options).forEach(o => o.selected = picks.includes(Number(o.value))); }
            const fp = find('SalePrice'); if (fp) { const v = val(fp); if (v.min != null && v.max != null) $('priceRange').value = `${v.min}-${v.max}`; }
            const fr = find('RankRange'); if (fr) { const v = val(fr); if (v.from) $('rankFrom').value = String(v.from); if (v.to) $('rankTo').value = String(v.to); }
        }

        // borrow
        if (bk === 'borrow') {
            const fc = find('CategoryID'); if (fc) { const picks = (val(fc).values || []).map(Number); Array.from($('borrowCategories').options).forEach(o => o.selected = picks.includes(Number(o.value))); }
            if (FEATURE_PUBLISH_DECADE) {
                const fd = find('PublishDecade'); if (fd) { const v = val(fd); if (v.fromYear && v.toYear) $('publishDecade').value = `${v.fromYear}-${v.toYear}`; }
            }
            const fr = find('RankRange'); if (fr) { const v = val(fr); if (v.from) $('rankFromBorrow').value = String(v.from); if (v.to) $('rankToBorrow').value = String(v.to); }
        }
    }

    // 訂單狀態 checkbox：全選/半選 + 預設只勾已完成
    function wireStatusCheckboxes() {
        const boxAll = document.getElementById('os_all');
        const boxes = Array.from(document.querySelectorAll('#orderStatusGroup .os'));
        if (!boxAll || boxes.length === 0) return; // 沒有 orders UI 就略過

        const syncAllState = () => {
            const allChecked = boxes.every(b => b.checked);
            boxAll.checked = allChecked;
            boxAll.indeterminate = !allChecked && boxes.some(b => b.checked);
        };

        boxAll.addEventListener('change', () => {
            boxes.forEach(b => b.checked = boxAll.checked);
            syncAllState();
            if (typeof preview === 'function') preview();
        });
        boxes.forEach(b => b.addEventListener('change', () => {
            syncAllState();
            if (typeof preview === 'function') preview();
        }));

        // 預設只勾「已完成」
        boxes.forEach(b => b.checked = false);
        const done = document.getElementById('os_3');
        if (done) done.checked = true;
        syncAllState();
    }

    // ==== wire events ====
    function wire() {
        // submit：塞 FiltersJson
        document.addEventListener('submit', (ev) => {
            const form = ev.target;
            if (form && form.querySelector('#FiltersJson')) {
                $('FiltersJson').value = JSON.stringify(buildFilters());
            }
        });

        // 即時預覽：只要相關欄位變動，做兩件事：1)區塊顯示 2)細節同步（粒度/標題/訂單/出版年）→ 重畫
        ['Category', 'category', 'baseKind', 'dateFrom', 'dateTo', 'gDay', 'gMonth', 'gYear',
            'salesCategories', 'borrowCategories', 'priceRange',
            'rankFrom', 'rankTo', 'rankFromBorrow', 'rankToBorrow', 'publishDecade',
            'mAmount', 'mCount', 'ordAmtMin', 'ordAmtMax'
        ].forEach(id => {
            const el = $(id);
            if (el) el.addEventListener('change', () => { showSections(); syncUiByCategoryAndKind(); preview(); });
        });

        // 圖型切換按鈕（Edit 可能存在）
        document.querySelectorAll('[data-chart]').forEach(btn => {
            btn.addEventListener('click', () => setTimeout(() => { showSections(); syncUiByCategoryAndKind(); preview(); }, 0));
        });

        // 預覽按鈕
        $('btnPreview')?.addEventListener('click', preview);
    }

    // ==== init ====
    (async function init() {
        await loadCategories();
        genPrice();
        genRank('rankFrom', 'rankTo', 100, 1, 10);
        genRank('rankFromBorrow', 'rankToBorrow', 100, 1, 10);
        genDecades(); // 即使 flag 關閉也安全，因為整列預設隱藏

        // 預設日期 30 天
        const today = new Date(); const from = new Date(); from.setDate(today.getDate() - 29);
        if ($('dateFrom')) $('dateFrom').value ||= from.toISOString().slice(0, 10);
        if ($('dateTo')) $('dateTo').value ||= today.toISOString().slice(0, 10);

        showSections();
        syncUiByCategoryAndKind(); // 套用：隱藏粒度 / 訂單 / 出版年份
        wire();
        wireStatusCheckboxes();

        // Edit 頁：套回已存
        if (apiDef) { await hydrateFromServer(); showSections(); syncUiByCategoryAndKind(); }

        // 首次預覽
        preview();
    })();
})();
