// wwwroot/js/unlayer-mail.js
(function (w, d) {
    function q(sel) { return d.querySelector(sel); }
    function byId(id) { return d.getElementById(id); }

    // 解析 JSON（含 HTML decode 保底）
    function tryParseJson(raw) {
        if (!raw) return null;
        if (typeof raw !== 'string') return raw;
        try { return JSON.parse(raw); } catch (_) { }
        try {
            var ta = d.createElement('textarea');
            ta.innerHTML = raw;
            var decoded = ta.value || ta.textContent || '';
            return decoded ? JSON.parse(decoded) : null;
        } catch (_) { return null; }
    }

    var UnlayerMail = {
        init: function (opts) {
            if (this.__inited) return;
            this.__inited = true;

            this.opts = opts || {};
            this._setupEditor();
            this._bind();
            this._autoPreview();
        },

        _setupEditor: function () {
            var self = this, opts = self.opts;

            w.unlayer.init({
                id: opts.editorId || 'editor',
                locale: 'zh-TW',
                displayMode: 'email',
                appearance: { theme: 'light', panels: { tools: { dock: 'left' } } },
                editor: { minRows: 20, maxRows: 40 }
            });

            // Edit 模式才嘗試載入設計
            w.unlayer.addEventListener('editor:ready', function () {
                if (opts.mode !== 'edit') return;

                var rawDesign = byId(opts.designFieldId)?.value || '';
                var rawHtml = byId(opts.htmlFieldId)?.value || '';

                var designObj = tryParseJson(rawDesign);
                if (designObj) {
                    try { w.unlayer.loadDesign(designObj); self._refreshPreviewOnce(); return; }
                    catch (e) { console.warn('[UnlayerMail] loadDesign 失敗，改試 HTML：', e); }
                }

                var cleaned = '';
                if (rawHtml && rawHtml.trim()) {
                    try {
                        var doc = new DOMParser().parseFromString(rawHtml, 'text/html');
                        cleaned = doc?.body ? doc.body.innerHTML : '';
                    } catch (_) { }
                    if (!cleaned) {
                        cleaned = rawHtml
                            .replace(/<!doctype[^>]*>/ig, '')
                            .replace(/<\/?html[^>]*>/ig, '')
                            .replace(/<\/?head[^>]*>[\s\S]*?<\/head>/ig, '')
                            .replace(/<\/?body[^>]*>/ig, '');
                    }
                }

                try {
                    if (typeof w.unlayer.importHtml === 'function') {
                        w.unlayer.importHtml(cleaned || '');
                        self._refreshPreviewOnce();
                        return;
                    }
                } catch (e1) { console.warn('[UnlayerMail] importHtml 失敗：', e1); }

                if (typeof w.unlayer.convertHtmlToDesign === 'function') {
                    try {
                        w.unlayer.convertHtmlToDesign(cleaned || '', function (d) {
                            w.unlayer.loadDesign(d); self._refreshPreviewOnce();
                        });
                        return;
                    } catch (e2) { console.warn('[UnlayerMail] convertHtmlToDesign 失敗：', e2); }
                }

                // fallback：HTML Tool
                var fallbackDesign = {
                    body: { rows: [{ id: 'r1', cells: [1], columns: [{ id: 'c1', contents: [{ id: 'h1', type: 'html', values: { html: cleaned || '<p></p>' } }] }] }] },
                    schemaVersion: 16
                };
                try { w.unlayer.loadDesign(fallbackDesign); self._refreshPreviewOnce(); }
                catch (e3) { console.error('[UnlayerMail] fallback 載入失敗：', e3); }
            });

            // 圖片上傳 callback（URL/Token 由外層傳入）
            w.unlayer.registerCallback('image', function (file, done) {
                var fd = new FormData();
                fd.append('file', file.attachments?.[0] || file);
                fetch(opts.uploadUrl, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': opts.antiForgery || '' },
                    body: fd
                })
                    .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(t)))
                    .then(ret => done({ progress: 100, url: ret.url }))
                    .catch(err => { alert('上傳失敗：' + (err?.message || err)); done({ progress: 100, url: '' }); });
            });
        },

        _refreshPreviewOnce: function () {
            var self = this, opts = self.opts;
            w.unlayer.exportHtml(function (data) {
                var html = (data && data.html) || '';
                byId(opts.htmlFieldId).value = html;
                self._writePreview(html);
            });
        },

        // 儲存：saveDesign + exportHtml → hidden → AJAX Submit
        saveAllAndSubmit: function () {
            var self = this, opts = self.opts;
            var form = byId(opts.formId);
            if (!form) {
                alert('找不到表單，無法送出'); return;
            }

            // 找到儲存按鈕並暫時禁用
            var saveButton = q(opts.saveButtonSelector);
            var originalButtonText = '';
            if (saveButton) {
                originalButtonText = saveButton.innerHTML;
                saveButton.disabled = true;
                saveButton.innerHTML = '儲存中...';
            }

            var designP = new Promise(r => w.unlayer.saveDesign(r));
            var exportP = new Promise(r => w.unlayer.exportHtml(r));

            Promise.all([designP, exportP]).then(function ([design, htmlObj]) {
                var dEl = byId(opts.designFieldId), hEl = byId(opts.htmlFieldId);
                if (dEl) dEl.value = JSON.stringify(design || {});
                if (hEl) hEl.value = (htmlObj?.html || '');

                // === 變更點：從 form.submit() 改為 fetch() ===

                var formData = new FormData(form);

                fetch(form.action, { // form.action 應為 /Mail/TemplateVersions/Create?templateId=...
                    method: 'POST',

                    body: formData
                })
                    .then(r => r.json()) // 假設伺服器一定會返回 JSON
                    .then(res => {
                        if (res.ok) {
                            alert('儲存成功！');
                            // 根據 Controller 返回的 URL 重導
                            if (res.redirectUrl) {
                                window.location.href = res.redirectUrl;
                            }
                        } else {
                            // 儲存失敗，顯示錯誤
                            var errorMsg = (res.errors && res.errors.join('\n')) || '儲存失敗，請檢查欄位。';
                            alert(errorMsg);
                        }
                    })
                    .catch(err => {
                        alert('儲存時發生網路錯誤：' + (err?.message || err));
                    })
                    .finally(() => {
                        // 無論成功失敗，都恢復按鈕
                        if (saveButton) {
                            saveButton.disabled = false;
                            saveButton.innerHTML = originalButtonText;
                        }
                    });

            }).catch(function (err) {
                alert('Unlayer 匯出失敗：' + (err?.message || err));
                // 恢復按鈕
                if (saveButton) {
                    saveButton.disabled = false;
                    saveButton.innerHTML = originalButtonText;
                }
            });
        },
        // 試寄：先匯出 HTML 再打 API
        testSend: function () {
            var self = this, opts = self.opts;
            var to = q(opts.recipientSelector)?.value || '';
            if (!to) { alert('請先輸入收件者'); return; }

            new Promise(r => w.unlayer.exportHtml(r))
                .then(function (data) {
                    var html = (data && data.html) || '';
                    byId(opts.htmlFieldId).value = html;

                    var payload = {
                        to,
                        subject: q(opts.subjectSelector)?.value || '',
                        bodyHtml: html,
                        name: q(opts.nameSelector)?.value || ''
                    };
                    return fetch(opts.testSendUrl, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json; charset=utf-8',
                            'RequestVerificationToken': opts.antiForgery || ''
                        },
                        body: JSON.stringify(payload)
                    });
                })
                .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(t)))
                .then(ret => ret?.ok ? alert('已送出試寄') : alert('試寄失敗：' + (ret?.error || '')))
                .catch(err => alert('試寄失敗：' + (err?.message || err)));
        },

        // 自動預覽 & 即時刷新
        _autoPreview: function () {
            var self = this, opts = self.opts, t = null;
            function update() {
                w.unlayer.exportHtml(function (data) {
                    var html = (data && data.html) || '';
                    byId(opts.htmlFieldId).value = html;
                    self._writePreview(html);
                });
            }
            function schedule() { clearTimeout(t); t = setTimeout(update, 200); }

            ['input', 'change', 'keyup'].forEach(evt => {
                d.addEventListener(evt, function (e) {
                    var name = e.target.name || '';
                    var id = e.target.id || '';
                    if (name === 'Subject' || id === (opts.recipientSelector || '').replace('#', '') || id === (opts.nameSelector || '').replace('#', '')) {
                        schedule();
                    }
                });
            });

            setInterval(update, 1200);
        },

        _writePreview: function (html) {
            var opts = this.opts;
            var s = q(opts.subjectSelector)?.value || '';
            var to = q(opts.recipientSelector)?.value || '';
            var name = q(opts.nameSelector)?.value || '';
            var h = (html || '')
                .replace(/\{\{\s*Recipient\s*\}\}/gi, to || '')
                .replace(/\{\{\s*Name\s*\}\}/gi, name || '');

            var frame = byId(opts.previewFrameId);
            if (!frame) return;
            var doc = frame.contentDocument || frame.contentWindow?.document;
            doc.open();
            doc.write('<!doctype html><html><head><meta charset="utf-8"><title></title></head><body>');
            doc.write(h);
            doc.write('</body></html>');
            doc.close();

            var subjOut = q(opts.subjectOutSelector);
            if (subjOut) subjOut.textContent = s;
        },

        _bind: function () {
            var self = this, opts = self.opts;
            q(opts.saveButtonSelector)?.addEventListener('click', function (e) {
                e.preventDefault(); self.saveAllAndSubmit();
            });
            q(opts.testButtonSelector)?.addEventListener('click', function (e) {
                e.preventDefault(); self.testSend();
            });
        }
    };

    w.UnlayerMail = UnlayerMail;
})(window, document);
