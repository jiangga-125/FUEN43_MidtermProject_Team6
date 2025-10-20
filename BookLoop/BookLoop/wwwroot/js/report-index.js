// wwwroot/js/report-index.js
(function () {
    'use strict';
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


    const cfg = window.ReportPageConfig || { api: {}, urls: {}, titles: {} };
    const lower = s => (s ?? '').toString().trim().toLowerCase();
    const cssEscape = (window.CSS && CSS.escape) ? CSS.escape : (s) => String(s).replace(/["\\]/g, '\\$&');

    // === 狀態（沿用你頁面上的變數語意） ===
    const charts = { line: null, bar: null, pie: null };
    const state = {
        line: { baseKind: 'sales', granularity: 'day', valueMetric: 'amount' },
        bar: { baseKind: 'sales', valueMetric: 'quantity' },
        pie: { baseKind: 'borrow', valueMetric: 'count' }
    };

    // === 取得 Anti-Forgery Token（用你頁面現有的隱藏表單） ===
    function getAfToken() {
        return document.querySelector('#defDeleteForm input[name="__RequestVerificationToken"]')?.value || '';
    }

    // === 繪圖 ===
    function renderLine(labels, data, title) {
        const el = document.getElementById('chartLine');
        if (!el) return;
        if (charts.line && typeof charts.line.destroy === 'function') charts.line.destroy();
        charts.line = new Chart(el, {
            type: 'line',
            data: { labels: labels || [], datasets: [{ label: title || '折線圖', data: data || [] }] },
            options: {
                responsive: true, tension: .25, scales: { y: { beginAtZero: true } }, plugins: { 'no-data': { text: '無資料' } },animation: false}
        });
    }
    function renderBar(labels, data, title) {
        const el = document.getElementById('chartBar');
        if (!el) return;
        if (charts.bar && typeof charts.bar.destroy === 'function') charts.bar.destroy();
        charts.bar = new Chart(el, {
            type: 'bar',
            data: { labels: labels || [], datasets: [{ label: title || '長條圖', data: data || [] }] },
            options: { responsive: true, scales: { y: { beginAtZero: true } }, plugins: { 'no-data': { text: '無資料' } }, animation: false }
        });
    }
    function renderPie(labels, data, title) {
        const el = document.getElementById('chartPie');
        if (!el) return;
        if (charts.pie && typeof charts.pie.destroy === 'function') charts.pie.destroy();
        charts.pie = new Chart(el, {
            type: 'pie',
            data: { labels: labels || [], datasets: [{ label: title || '圓餅圖', data: data || [] }] },
            options: { responsive: true, plugins: { 'no-data': { text: '無資料' } }, animation: false }
        });
    }

    // === 從 Filters 同步粒度/指標（避免標籤不一致） ===
    function trySyncGranularityFromFilters(filters) {
        try {
            const g1 = (filters || []).find(f => {
                const n = lower(f.FieldName ?? f.fieldName);
                return n === 'granularity' || n === 'dategranularity';
            });
            if (g1) {
                const raw = g1.ValueJson ?? g1.valueJson ?? '{}';
                try {
                    const j = typeof raw === 'string' ? JSON.parse(raw) : raw;
                    const v = lower((j?.value ?? j));
                    if (['year', 'month', 'day'].includes(v)) { state.line.granularity = v; return true; }
                } catch {
                    const v = lower(String(raw));
                    if (['year', 'month', 'day'].includes(v)) { state.line.granularity = v; return true; }
                }
            }
            const g2 = (filters || []).find(f => {
                const dt = lower(f.DataType ?? f.dataType);
                const n = lower(f.FieldName ?? f.fieldName);
                return dt === 'date' || n.endsWith('date');
            });
            if (g2) {
                const raw = g2.ValueJson ?? g2.valueJson ?? '{}';
                let gVal = null;
                try {
                    const j = typeof raw === 'string' ? JSON.parse(raw) : raw;
                    const cand = (j?.gran ?? j?.granularity ?? j?.groupBy ?? j?.groupby);
                    if (typeof cand === 'string') gVal = lower(cand);
                } catch {
                    const s = lower(String(raw));
                    if (s.includes('year')) gVal = 'year';
                    else if (s.includes('month')) gVal = 'month';
                    else if (s.includes('day')) gVal = 'day';
                }
                if (['year', 'month', 'day'].includes(gVal)) { state.line.granularity = gVal; return true; }
            }
            return false;
        } catch { return false; }
    }
    function trySyncMetricFromFilters(filters, setter) {
        try {
            const mf = (filters || []).find(f => lower(f.FieldName ?? f.fieldName) === 'metric');
            if (!mf) return false;
            const raw = mf.ValueJson ?? mf.valueJson ?? '{}';
            let metric = null;
            try {
                const j = typeof raw === 'string' ? JSON.parse(raw) : raw;
                const v = lower((j?.value ?? j));
                if (['amount', 'count', 'quantity'].includes(v)) metric = v;
            } catch {
                const v = lower(String(raw));
                if (['amount', 'count', 'quantity'].includes(v)) metric = v;
            }
            if (metric) { setter(metric); return true; }
            return false;
        } catch { return false; }
    }

    // === 預設資料 ===
    async function loadDefault(kind) {
        const url = cfg.api[kind];
        if (!url) return;
        try {
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const json = await res.json();

            if (kind === 'line') {
                state.line.baseKind = 'sales';
                state.line.granularity = 'day';
                state.line.valueMetric = 'amount';
                renderLine(json.labels || [], json.data || [], json.title || '預設折線圖');
            } else if (kind === 'bar') {
                state.bar.baseKind = 'sales';
                state.bar.valueMetric = 'quantity';
                renderBar(json.labels || [], json.data || [], json.title || '預設長條圖');
            } else {
                state.pie.baseKind = 'borrow';
                state.pie.valueMetric = 'count';
                renderPie(json.labels || [], json.data || [], json.title || '預設圓餅圖');
            }
        } catch (e) {
            console.error(e);
            if (kind === 'line') renderLine([], [], '預設折線圖（載入失敗）');
            else if (kind === 'bar') renderBar([], [], '預設長條圖（載入失敗）');
            else renderPie([], [], '預設圓餅圖（載入失敗）');
        }
    }

    // === 依自訂定義載入 ===
    async function loadByDefinition(kind, defId) {
        if (!defId) return loadDefault(kind);
        try {
            const resDef = await fetch(`${cfg.api.defPayload}?id=${encodeURIComponent(defId)}&t=${Date.now()}`, { cache: 'no-store' });
            if (!resDef.ok) throw new Error(`DefinitionPayload HTTP ${resDef.status}`);
            const def = await resDef.json();

            const category = lower(def.category ?? def.Category);
            if (category !== kind) { alert(`此報表不是 ${kind} 類型`); return; }

            const filters = def.filters ?? def.Filters ?? [];
            const titleFromDdl = (() => {
                const ddlId = kind === 'line' ? 'ddlLine' : (kind === 'bar' ? 'ddlBar' : 'ddlPie');
                const ddl = document.getElementById(ddlId);
                return ddl?.selectedOptions?.[0]?.text?.trim() || '';
            })();
            const defName = (def.reportName ?? def.ReportName ?? '').toString().trim();
            const viewTitle = titleFromDdl || defName || (kind === 'line' ? '折線圖' : (kind === 'bar' ? '長條圖' : '圓餅圖'));

            // 狀態同步
            if (kind === 'line') {
                const k = lower(def.baseKind ?? def.BaseKind);
                if (!['sales', 'borrow', 'orders'].includes(k)) throw new Error('缺少來源類型');
                state.line.baseKind = k;
            } else if (kind === 'bar') {
                state.bar.baseKind = lower(def.baseKind ?? def.BaseKind) || 'sales';
            } else {
                state.pie.baseKind = lower(def.baseKind ?? def.BaseKind) || 'borrow';
            }

            // 組 Preview 請求
            const req = {
                Category: kind,
                BaseKind: kind === 'line' ? state.line.baseKind : (kind === 'bar' ? state.bar.baseKind : state.pie.baseKind),
                Filters: (filters || []).map(f => ({
                    FieldName: f.fieldName ?? f.FieldName,
                    Operator: f.operator ?? f.Operator,
                    DataType: f.dataType ?? f.DataType,
                    ValueJson: f.valueJson ?? f.ValueJson ?? null,
                    DefaultValue: f.defaultValue ?? f.DefaultValue ?? null,
                    Options: f.options ?? f.Options ?? null
                }))
            };

            // 同步粒度/指標
            if (kind === 'line') {
                trySyncGranularityFromFilters(filters);
                trySyncMetricFromFilters(filters, m => { state.line.valueMetric = m; });
            } else if (kind === 'bar') {
                trySyncMetricFromFilters(filters, m => { state.bar.valueMetric = m; });
            } else {
                trySyncMetricFromFilters(filters, m => { state.pie.valueMetric = m; });
            }

            const resPrev = await fetch(`${cfg.api.preview}?t=${Date.now()}`, {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' },
                body: JSON.stringify(req), cache: 'no-store'
            });
            if (!resPrev.ok) throw new Error(`PreviewDraft HTTP ${resPrev.status}`);
            const json = await resPrev.json();

            // 以回應 echo 再同步一次
            if (json?.echo?.filters) {
                if (kind === 'line') {
                    trySyncGranularityFromFilters(json.echo.filters);
                    trySyncMetricFromFilters(json.echo.filters, m => { state.line.valueMetric = m; });
                } else if (kind === 'bar') {
                    trySyncMetricFromFilters(json.echo.filters, m => { state.bar.valueMetric = m; });
                } else {
                    trySyncMetricFromFilters(json.echo.filters, m => { state.pie.valueMetric = m; });
                }
            }

            let labels = [], data = [];
            if (Array.isArray(json.labels) && Array.isArray(json.data)) {
                labels = json.labels; data = json.data;
            } else {
                const s = json.series || json.Series || [];
                labels = s.map(p => p.label ?? p.name ?? '');
                data = s.map(p => Number(p.value ?? p.y ?? 0));
            }

            if (kind === 'line') renderLine(labels, data, json.title || viewTitle);
            else if (kind === 'bar') renderBar(labels, data, json.title || viewTitle);
            else renderPie(labels, data, json.title || viewTitle);

        } catch (err) {
            console.error(`loadByDefinition(${kind}) failed:`, err);
            if (kind === 'line') renderLine([], [], '載入失敗');
            else if (kind === 'bar') renderBar([], [], '載入失敗');
            else renderPie([], [], '載入失敗');
        }
    }

    // === 刪除 Definition ===
    async function deleteDefinitionAjax(defId) {
        if (!defId) throw new Error('缺少 id');
        const url = cfg.urls.delete;
        const token = getAfToken();
        const body = new URLSearchParams({ id: defId, __RequestVerificationToken: token });

        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
            body
        });
        if (!(res.status >= 200 && res.status < 400)) {
            const t = await res.text().catch(() => '');
            throw new Error(`刪除失敗（HTTP ${res.status}）${t ? '：' + t : ''}`);
        }
    }

    async function removeOptionAndRefresh(selectId, deletedId, category) {
        const ddl = document.getElementById(selectId);
        if (!ddl) return;

        const wasSelected = ddl.value === String(deletedId);
        const opt = ddl.querySelector(`option[value="${cssEscape(String(deletedId))}"]`);
        if (opt) opt.remove();

        if (wasSelected) ddl.value = '';

        const current = ddl.value || '';
        if (category === 'line') { if (!current) await loadDefault('line'); else await loadByDefinition('line', current); }
        else if (category === 'bar') { if (!current) await loadDefault('bar'); else await loadByDefinition('bar', current); }
        else if (category === 'pie') { if (!current) await loadDefault('pie'); else await loadByDefinition('pie', current); }
    }

    // === UI 綁定 ===
    function wireBindings() {
        // 下拉切換
        const ddlLine = document.getElementById('ddlLine');
        const ddlBar = document.getElementById('ddlBar');
        const ddlPie = document.getElementById('ddlPie');

        ddlLine?.addEventListener('change', e => { const id = e.target.value || ''; if (!id) loadDefault('line'); else loadByDefinition('line', id); });
        ddlBar?.addEventListener('change', e => { const id = e.target.value || ''; if (!id) loadDefault('bar'); else loadByDefinition('bar', id); });
        ddlPie?.addEventListener('change', e => { const id = e.target.value || ''; if (!id) loadDefault('pie'); else loadByDefinition('pie', id); });

        // 編輯
        document.getElementById('btnEditLine')?.addEventListener('click', () => {
            const id = ddlLine?.value || '';
            if (!id) { alert('請先在折線圖下拉選擇一筆自訂報表'); return; }
            window.location.href = `${cfg.urls.editBase}/${id}`;
        });
        document.getElementById('btnEditBar')?.addEventListener('click', () => {
            const id = ddlBar?.value || '';
            if (!id) { alert('請先在長條圖下拉選擇一筆自訂報表'); return; }
            window.location.href = `${cfg.urls.editBase}/${id}`;
        });
        document.getElementById('btnEditPie')?.addEventListener('click', () => {
            const id = ddlPie?.value || '';
            if (!id) { alert('請先在圓餅圖下拉選擇一筆自訂報表'); return; }
            window.location.href = `${cfg.urls.editBase}/${id}`;
        });

        // 刪除
        document.getElementById('btnDelLine')?.addEventListener('click', async () => {
            const id = ddlLine?.value || '';
            if (!id) { alert('請先在折線圖下拉選擇要刪除的自訂報表'); return; }
            if (!confirm('確定要刪除此自訂報表嗎？此動作無法復原。')) return;
            try { await deleteDefinitionAjax(id); await removeOptionAndRefresh('ddlLine', id, 'line'); alert('✅ 已刪除'); }
            catch (err) { console.error(err); alert('❌ ' + (err?.message || err)); }
        });
        document.getElementById('btnDelBar')?.addEventListener('click', async () => {
            const id = ddlBar?.value || '';
            if (!id) { alert('請先在長條圖下拉選擇要刪除的自訂報表'); return; }
            if (!confirm('確定要刪除此自訂報表嗎？此動作無法復原。')) return;
            try { await deleteDefinitionAjax(id); await removeOptionAndRefresh('ddlBar', id, 'bar'); alert('✅ 已刪除'); }
            catch (err) { console.error(err); alert('❌ ' + (err?.message || err)); }
        });
        document.getElementById('btnDelPie')?.addEventListener('click', async () => {
            const id = ddlPie?.value || '';
            if (!id) { alert('請先在圓餅圖下拉選擇要刪除的自訂報表'); return; }
            if (!confirm('確定要刪除此自訂報表嗎？此動作無法復原。')) return;
            try { await deleteDefinitionAjax(id); await removeOptionAndRefresh('ddlPie', id, 'pie'); alert('✅ 已刪除'); }
            catch (err) { console.error(err); alert('❌ ' + (err?.message || err)); }
        });
    }

    // === 匯出（Modal：Excel / PDF + Email） ===
    function wireExportModal() {
        let currentSource = null; // 'line' | 'bar' | 'pie'
        const btnLine = document.getElementById('btnExportLine');
        const btnBar = document.getElementById('btnExportBar');
        const btnPie = document.getElementById('btnExportPie');

        const modalEl = document.getElementById('exportModal');
        if (!modalEl || !window.bootstrap || !bootstrap.Modal) { console.warn('找不到 exportModal 或 Bootstrap JS 未載入'); return; }
        const modal = new bootstrap.Modal(modalEl);

        if (btnLine) btnLine.addEventListener('click', (e) => { e.preventDefault(); currentSource = 'line'; modal.show(); });
        if (btnBar) btnBar.addEventListener('click', (e) => { e.preventDefault(); currentSource = 'bar'; modal.show(); });
        if (btnPie) btnPie.addEventListener('click', (e) => { e.preventDefault(); currentSource = 'pie'; modal.show(); });

        const form = document.getElementById('exportForm');
        form?.addEventListener('submit', async (e) => {
            e.preventDefault();

            const format = (document.querySelector('input[name="format"]:checked')?.value || 'xlsx').toLowerCase();
            const emailInput = document.getElementById('exportEmail');
            const email = (emailInput?.value || '').trim();
            const ok = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
            if (!ok) { emailInput?.classList.add('is-invalid'); return; }
            emailInput?.classList.remove('is-invalid');

            if (!currentSource) { alert('請先選擇要匯出的圖表。'); return; }

            // 組 payload
            function buildPayloadFor(category, chart, extra, email) {
                // 關動畫→同步刷新，避免截到半動畫的圖
                if (chart && chart.options) {
                    const prev = chart.options.animation;
                    chart.options.animation = false;
                    chart.update('none');
                    queueMicrotask(() => { chart.options.animation = prev; });
                }

                // 取資料
                const labels = Array.isArray(chart?.data?.labels)
                    ? chart.data.labels.map(x => `${x}`) // 確保是字串
                    : [];
                const values = Array.isArray(chart?.data?.datasets) && chart.data.datasets[0]?.data
                    ? chart.data.datasets[0].data
                    : [];

                // 決定標題（沿用你原本 ddl/預設標題邏輯）
                const ddlId = category === 'line' ? 'ddlLine' : (category === 'bar' ? 'ddlBar' : 'ddlPie');
                const ddl = document.getElementById(ddlId);
                const defTitle = (cfg?.titles && cfg.titles[category]) || '報表';
                const titleText = ddl?.options?.[ddl.selectedIndex]?.text?.trim() || defTitle;

                // 把 Chart.js 畫面轉為 PNG base64
                const imgDataUrl = (chart && typeof chart.toBase64Image === 'function')
                    ? chart.toBase64Image('image/png', 1.0)
                    : null;

                // 只回傳後端 DTO 需要的欄位（不再帶任何中文對照/Chips）
                return {
                    To: email,
                    Category: category,                    // line | bar | pie
                    Title: titleText,
                    SubTitle: `匯出日期：${new Date().toLocaleString()}`,
                    BaseKind: extra?.baseKind || '',       // borrow | sales | orders
                    Granularity: extra?.granularity || '', // 沒粒度就傳空字串
                    ValueMetric: extra?.valueMetric || '', // amount | count | quantity
                    Labels: labels,
                    Values: values,
                    ChartImageBase64: imgDataUrl,           // data:image/png;base64,...
                    DefinitionId: ddl?.value ? Number(ddl.value) : null // 若有選自訂報表就帶 ID；沒選就傳 null
                };
            }

            let payload;
            if (currentSource === 'line') {
                payload = buildPayloadFor('line', charts.line, {
                    baseKind: state.line.baseKind,
                    granularity: state.line.granularity,
                    valueMetric: state.line.valueMetric
                }, email);
            } else if (currentSource === 'bar') {
                payload = buildPayloadFor('bar', charts.bar, {
                    baseKind: state.bar.baseKind,
                    valueMetric: state.bar.valueMetric
                }, email);
            } else { // pie
                payload = buildPayloadFor('pie', charts.pie, {
                    baseKind: state.pie.baseKind,
                    valueMetric: state.pie.valueMetric
                }, email);
            }

            const url = (format === 'pdf') ? cfg.api.sendPdf : cfg.api.sendExcel;

            const submitBtn = form.querySelector('button[type="submit"]');
            submitBtn.disabled = true;
            try {
                const res = await fetch(url, {
                    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
                });
                if (!res.ok) {
                    const t = await res.text().catch(() => '');
                    throw new Error(`匯出失敗（HTTP ${res.status}）${t ? '：' + t : ''}`);
                }
                alert('✅ 已寄出到：' + email);
                modal.hide();
            } catch (err) {
                console.error(err);
                alert('❌ ' + (err?.message || err));
            } finally {
                submitBtn.disabled = false;
            }
        });
    }




    // === 首次載入 ===
    document.addEventListener('DOMContentLoaded', () => {
        loadDefault('line'); loadDefault('bar'); loadDefault('pie');
        wireBindings();
        wireExportModal();
    });

})();
