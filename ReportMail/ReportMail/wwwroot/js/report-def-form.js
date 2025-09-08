// @ts-nocheck

(function () {
    const root = document.getElementById('rd-form');
    if (!root) return;

    const mode = root.dataset.mode || 'create';
    const defId = Number(root.dataset.defId || 0);
    const apiDef = root.dataset.apiDef;
    const apiCats = root.dataset.apiCats;
    const apiPreview = root.dataset.apiPreview;

    const $ = (id) => document.getElementById(id);

    function showSection(kind) {
        $('section-sales').style.display = (kind === 'sales' ? '' : 'none');
        $('section-borrow').style.display = (kind === 'borrow' ? '' : 'none');
        $('section-orders').style.display = (kind === 'orders' ? '' : 'none');
    }

    function genPrice() {
        const sel = $('priceRange');
        if (!sel) return;
        sel.innerHTML = '';
        sel.add(new Option('（全部）', ''));
        for (let s = 1; s <= 1000; s += 100) {
            const e = Math.min(s + 99, 1000);
            sel.add(new Option(`${s}~${e}`, `${s}-${e}`));
        }
    }

    async function loadCategories() {
        if (!apiCats) return;
        const res = await fetch(apiCats, { cache: 'no-store' });
        const list = await res.json();
        const fill = (selId) => {
            const sel = $(selId);
            if (!sel) return;
            sel.innerHTML = '';
            sel.add(new Option('（全部）', ''));
            list.forEach(x => sel.add(new Option(x.text, x.value)));
        };
        fill('salesCategories');
        fill('borrowCategories');
    }

    // 訂單狀態全選
    (function wireStatusCheckboxes() {
        const boxAll = $('os_all');
        const boxes = Array.from(document.querySelectorAll('#orderStatusGroup .os'));
        const syncAllState = () => {
            const allChecked = boxes.every(b => b.checked);
            boxAll.checked = allChecked;
            boxAll.indeterminate = !allChecked && boxes.some(b => b.checked);
        };
        if (boxAll) {
            boxAll.addEventListener('change', () => {
                boxes.forEach(b => b.checked = boxAll.checked);
                syncAllState(); preview();
            });
            boxes.forEach(b => b.addEventListener('change', () => { syncAllState(); preview(); }));
        }
    })();

    function buildFilters() {
        const kind = $('baseKind').value;
        const df = $('dateFrom').value;
        const dt = $('dateTo').value;
        const gran = document.querySelector('input[name="gran"]:checked')?.value || 'day';

        const filters = [];
        filters.push({
            FieldName: 'DateRange', DataType: 'date', Operator: 'between',
            ValueJson: JSON.stringify({ from: df, to: dt, gran })
        });

        if (kind === 'sales') {
            const cats = Array.from($('salesCategories').selectedOptions).map(o => Number(o.value)).filter(v => !!v);
            if (cats.length) filters.push({ FieldName: 'CategoryID', DataType: 'select', Operator: 'in', ValueJson: JSON.stringify({ values: cats }) });
            const pr = $('priceRange').value;
            if (pr) {
                const [min, max] = pr.split('-').map(Number);
                filters.push({ FieldName: 'SalePrice', DataType: 'number', Operator: 'between', ValueJson: JSON.stringify({ min, max }) });
            }
        } else if (kind === 'borrow') {
            const cats = Array.from($('borrowCategories').selectedOptions).map(o => Number(o.value)).filter(v => !!v);
            if (cats.length) filters.push({ FieldName: 'CategoryID', DataType: 'select', Operator: 'in', ValueJson: JSON.stringify({ values: cats }) });
        } else {
            const metric = $('mCount').checked ? 'count' : 'amount';
            filters.push({ FieldName: 'Metric', ValueJson: JSON.stringify({ value: metric }) });

            const picks = Array.from(document.querySelectorAll('#orderStatusGroup .os:checked')).map(cb => Number(cb.getAttribute('data-val')));
            const allChecked = $('os_all').checked;
            if (!allChecked) {
                filters.push({ FieldName: 'OrderStatus', Operator: 'in', ValueJson: JSON.stringify({ values: picks }) });
            }
            const min = $('ordAmtMin').value;
            const max = $('ordAmtMax').value;
            if (min || max) {
                filters.push({ FieldName: 'OrderAmount', DataType: 'number', Operator: 'between', ValueJson: JSON.stringify({ min: Number(min || 0), max: Number(max || 999999) }) });
            }
        }
        return filters;
    }

    // ====== 預覽（Chart.js）======
    let chart;
    async function preview() {
        if (!apiPreview) return;

        const payload = { Category: 'line', BaseKind: $('baseKind').value, Filters: buildFilters() };
        const res = await fetch(apiPreview, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' },
            body: JSON.stringify(payload),
            cache: 'no-store'
        });
        const json = await res.json();

        // 同時相容兩種回傳格式：
        // 1) 舊：{ labels:[], data:[], title:"..." }
        // 2) 新：{ series:[{label,value},...], echo:{...} }
        let labels = [], values = [], title = '預覽';
        if (Array.isArray(json.labels) && Array.isArray(json.data)) {
            labels = json.labels; values = json.data; title = json.title || title;
        } else if (Array.isArray(json.series) || Array.isArray(json.Series)) {
            const s = json.series || json.Series;
            labels = s.map(p => p.label ?? p.date ?? p.x ?? '');
            values = s.map(p => p.value ?? p.y ?? 0);
            title = (json.echo?.baseKind ? `折線圖 - ${json.echo.baseKind}` : title);
        }

        const cvs = $('previewChart');
        if (!cvs || !window.Chart) return;
        const ctx = cvs.getContext('2d');
        if (chart && chart.destroy) chart.destroy();
        chart = new Chart(ctx, {
            type: 'line',
            data: { labels, datasets: [{ label: title, data: values }] },
            options: { responsive: true, animation: false, scales: { y: { beginAtZero: true } } }
        });
    }

    async function hydrateForEdit() {
        if (!apiDef || !defId) return;
        const res = await fetch(`${apiDef}?id=${defId}`, { cache: 'no-store' });
        const d = await res.json();

        $('hidCategory').value = (d.Category || 'line');
        const bk = (d.BaseKind || 'sales');
        $('baseKind').value = bk; showSection(bk);

        const fr = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'daterange');
        if (fr && fr.ValueJson) {
            const v = JSON.parse(fr.ValueJson);
            if (v.from) $('dateFrom').value = v.from;
            if (v.to) $('dateTo').value = v.to;
            const g = (v.gran || 'day').toLowerCase();
            (document.getElementById(g === 'month' ? 'gMonth' : (g === 'year' ? 'gYear' : 'gDay'))).checked = true;
        }

        if (bk === 'sales') {
            const fc = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'categoryid' && (f.Operator || '').toLowerCase() === 'in');
            if (fc && fc.ValueJson) {
                const picks = (JSON.parse(fc.ValueJson).values || []).map(Number);
                Array.from($('salesCategories').options).forEach(o => { o.selected = picks.includes(Number(o.value)); });
            }
            const fp = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'saleprice' && (f.Operator || '').toLowerCase() === 'between');
            if (fp && fp.ValueJson) {
                const { min, max } = JSON.parse(fp.ValueJson);
                if (min && max) $('priceRange').value = `${min}-${max}`;
            }
        }

        if (bk === 'borrow') {
            const fc = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'categoryid' && (f.Operator || '').toLowerCase() === 'in');
            if (fc && fc.ValueJson) {
                const picks = (JSON.parse(fc.ValueJson).values || []).map(Number);
                Array.from($('borrowCategories').options).forEach(o => { o.selected = picks.includes(Number(o.value)); });
            }
        }

        if (bk === 'orders') {
            const fm = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'metric');
            if (fm && fm.ValueJson) {
                const val = (JSON.parse(fm.ValueJson).value || 'amount').toLowerCase();
                (val === 'count' ? $('mCount') : $('mAmount')).checked = true;
            }
            const fos = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'orderstatus' && (f.Operator || '').toLowerCase() === 'in');
            if (fos && fos.ValueJson) {
                const picks = (JSON.parse(fos.ValueJson).values || []).map(Number);
                const boxes = Array.from(document.querySelectorAll('#orderStatusGroup .os'));
                boxes.forEach(b => b.checked = picks.includes(Number(b.getAttribute('data-val'))));
                const all = $('os_all');
                const allChecked = boxes.every(b => b.checked);
                all.checked = allChecked; all.indeterminate = !allChecked && boxes.some(b => b.checked);
            } else {
                $('os_all').checked = true;
                Array.from(document.querySelectorAll('#orderStatusGroup .os')).forEach(b => b.checked = true);
            }
            const fa = (d.Filters || []).find(f => (f.FieldName || '').toLowerCase() === 'orderamount' && (f.Operator || '').toLowerCase() === 'between');
            if (fa && fa.ValueJson) {
                const { min, max } = JSON.parse(fa.ValueJson);
                if (min != null) $('ordAmtMin').value = min;
                if (max != null) $('ordAmtMax').value = max;
            }
        }
    }

    $('baseKind').addEventListener('change', (e) => { showSection(e.target.value); preview(); });
    document.addEventListener('submit', (ev) => {
        const form = ev.target;
        if (form && form.querySelector('#FiltersJson')) {
            $('FiltersJson').value = JSON.stringify(buildFilters());
        }
    });

    // 初始化：共用邏輯
    (async function init() {
        await loadCategories();
        genPrice();

        // 預設近 30 天
        const today = new Date(); const from = new Date(); from.setDate(today.getDate() - 29);
        $('dateFrom').value ||= from.toISOString().slice(0, 10);
        $('dateTo').value ||= today.toISOString().slice(0, 10);
        $('gDay').checked = true;

        // create：顯示 sales 區塊；edit：灌資料
        if (mode === 'edit') {
            await hydrateForEdit();
        } else {
            showSection($('baseKind').value || 'sales');
        }

        // 預覽一次
        preview();
    })();
})();
