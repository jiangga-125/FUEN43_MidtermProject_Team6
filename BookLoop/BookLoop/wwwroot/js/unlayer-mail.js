// wwwroot/js/unlayer-mail.js
(function (w, d) {
    function q(sel) { return d.querySelector(sel); }
    function byId(id) { return d.getElementById(id); }

    // 嘗試解析 JSON（含保底 decode）
    function tryParseJson(raw) {
        if (!raw || typeof raw !== 'string') return null;
        try { return JSON.parse(raw); } catch (_) { /* 再試一次做 decode */ }
        try {
            var ta = d.createElement('textarea');
            ta.innerHTML = raw;
            var decoded = ta.value || ta.textContent || '';
            if (decoded) return JSON.parse(decoded);
        } catch (_) { }
        return null;
    }

    var UnlayerMail = {
        init: function (opts) {
            this.opts = opts;
            this._setupEditor();
            this._bind();
            this._autoPreview();
        },

        _setupEditor: function () {
            var self = this;
            var opts = self.opts;

            w.unlayer.init({
                id: opts.editorId || 'editor',
                locale: 'zh-TW',
                displayMode: 'email',
                appearance: { theme: 'light', panels: { tools: { dock: 'left' } } },
                editor: { minRows: 20, maxRows: 40 }
            });

            w.unlayer.addEventListener('editor:ready', function () {
                if (opts.mode !== 'edit') return;

                var rawDesign = byId(opts.designFieldId)?.value || '';
                var rawHtml = byId(opts.htmlFieldId)?.value || '';

                console.log('[UnlayerMail] editor:ready', {
                    designLen: rawDesign?.length || 0,
                    htmlLen: rawHtml?.length || 0
                });

                // 1) 優先載 DesignJson
                var designObj = tryParseJson(rawDesign);
                if (designObj) {
                    try {
                        console.log('[UnlayerMail] loadDesign(from DesignJson)');
                        w.unlayer.loadDesign(designObj);
                        self._refreshPreviewOnce?.();
                        return;
                    } catch (e) {
                        console.warn('[UnlayerMail] loadDesign 失敗（DesignJson）→ fallback HTML：', e);
                    }
                }

                // 2) 從 Html 取 <body> 內容（或清掉外層標籤）
                var cleaned = '';
                if (rawHtml && rawHtml.trim()) {
                    try {
                        var doc = new DOMParser().parseFromString(rawHtml, 'text/html');
                        cleaned = (doc && doc.body) ? doc.body.innerHTML : '';
                    } catch (_) { }
                    if (!cleaned) {
                        cleaned = rawHtml
                            .replace(/<!doctype[^>]*>/ig, '')
                            .replace(/<\/?html[^>]*>/ig, '')
                            .replace(/<\/?head[^>]*>[\s\S]*?<\/head>/ig, '')
                            .replace(/<\/?body[^>]*>/ig, '');
                    }
                }

                // 3) 嘗試 SDK API（你的 build 沒有 import / convert，所以會跳到 fallback）
                try {
                    if (typeof w.unlayer.importHtml === 'function') {
                        console.log('[UnlayerMail] importHtml available');
                        w.unlayer.importHtml(cleaned);
                        self._refreshPreviewOnce?.();
                        return;
                    }
                } catch (e1) {
                    console.warn('[UnlayerMail] importHtml 失敗：', e1);
                }

                if (typeof w.unlayer.convertHtmlToDesign === 'function') {
                    try {
                        console.log('[UnlayerMail] convertHtmlToDesign available');
                        w.unlayer.convertHtmlToDesign(cleaned, function (d) {
                            w.unlayer.loadDesign(d);
                            self._refreshPreviewOnce?.();
                        });
                        return;
                    } catch (e2) {
                        console.warn('[UnlayerMail] convertHtmlToDesign 失敗：', e2);
                    }
                }

                // 4) 最終保底：用 HTML Tool 包成單一區塊設計，讓編輯器至少載得起來
                console.warn('[UnlayerMail] 沒有 import/convert，使用 HTML 區塊 fallback 載入');
                var fallbackDesign = {
                    body: {
                        rows: [{
                            id: 'row-1',
                            cells: [1],
                            columns: [{
                                id: 'col-1',
                                contents: [{
                                    id: 'html-1',
                                    type: 'html', // Unlayer 內建 HTML tool
                                    values: {
                                        html: cleaned || '<p></p>'
                                    }
                                }]
                            }]
                        }]
                    },
                    schemaVersion: 16 // 合理版本號，避免驗證錯誤
                };

                try {
                    w.unlayer.loadDesign(fallbackDesign);
                    self._refreshPreviewOnce?.();
                } catch (e3) {
                    console.error('[UnlayerMail] 連 fallbackDesign 都載不進：', e3);
                }
            });

            // 圖片上傳 callback
            w.unlayer.registerCallback('image', function (file, done) {
                var fd = new FormData();
                fd.append('file', file.attachments?.[0] || file);
                fetch(opts.uploadUrl, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': opts.antiForgery },
                    body: fd
                })
                    .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(t)))
                    .then(ret => done({ progress: 100, url: ret.url }))
                    .catch(err => { alert('上傳失敗：' + (err?.message || err)); done({ progress: 100, url: '' }); });
            });
        },

        // 立即匯出一次並刷新預覽（載入完成後叫用）
        _refreshPreviewOnce: function () {
            var self = this;
            w.unlayer.exportHtml(function (data) {
                var html = (data && data.html) || '';
                byId(self.opts.htmlFieldId).value = html;
                self._writePreview(html);
            });
        },

        // 單鍵儲存：saveDesign + exportHtml → 寫入 hidden → submit
        saveAllAndSubmit: function () {
            var self = this, opts = self.opts;
            var designP = new Promise(function (resolve) { w.unlayer.saveDesign(resolve); });
            var exportP = new Promise(function (resolve) { w.unlayer.exportHtml(resolve); });

            Promise.all([designP, exportP]).then(function ([design, htmlObj]) {
                byId(opts.designFieldId).value = JSON.stringify(design || {});
                byId(opts.htmlFieldId).value = (htmlObj?.html || '');
                byId(opts.formId).submit();
            }).catch(function (err) {
                alert('儲存失敗：' + (err?.message || err));
            });
        },

        // 試寄：若當下還沒匯出，先匯出一次（不送表單）
        testSend: function () {
            var self = this, opts = self.opts;
            var to = q(opts.recipientSelector)?.value || '';
            if (!to) { alert('請先輸入收件者'); return; }

            new Promise(function (resolve) { w.unlayer.exportHtml(resolve); })
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
                            'RequestVerificationToken': opts.antiForgery
                        },
                        body: JSON.stringify(payload)
                    });
                })
                .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(t)))
                .then(ret => ret?.ok ? alert('已送出試寄') : alert('試寄失敗：' + (ret?.error || '')))
                .catch(err => alert('試寄失敗：' + (err?.message || err)));
        },

        _autoPreview: function () {
            var self = this, opts = self.opts;
            setInterval(function () {
                w.unlayer.exportHtml(function (data) {
                    var html = (data && data.html) || '';
                    byId(opts.htmlFieldId).value = html;
                    self._writePreview(html);
                });
            }, 1200);
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
            var doc = frame.contentDocument;
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
                e.preventDefault();
                self.saveAllAndSubmit();
            });
            q(opts.testButtonSelector)?.addEventListener('click', function (e) {
                e.preventDefault();
                self.testSend();
            });
        }
    };

    w.UnlayerMail = UnlayerMail;
})(window, document);
