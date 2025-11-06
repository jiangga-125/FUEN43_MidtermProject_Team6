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
    // 讀取頁面提供的 Merge Tags（供 Unlayer 與主旨插入用）
    function readMergeTags() {
        try {
            var node = d.getElementById('merge-tags-json');
            var list = node ? (JSON.parse(node.textContent || '[]') || []) : [];
            console.log('[MergeTags] loaded:', list); // for debug
            return list;
        } catch (e) {
            console.warn('merge-tags-json parse failed:', e);
            return [];
        }
    }

    // 讀取「測試變數（JSON）」供預覽/試寄使用
    function readVarsJson() {
        try { return JSON.parse(q('#VarsJson')?.value || '{}') || {}; }
        catch { return {}; }
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
                editor: { minRows: 20, maxRows: 40 },
                mergeTags: readMergeTags()
            });

            // 載入設計 / 初始預覽
            w.unlayer.addEventListener('editor:ready', function () {
                console.log('[MergeTags] in editor:', readMergeTags()); // for debug

                // EDIT：嘗試載入既有設計或 HTML
                if (opts.mode === 'edit') {
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
                    return;
                }

                // CREATE：初始化後先做一次匯出→預覽同步
                self._refreshPreviewOnce();
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

                var formData = new FormData(form);

                fetch(form.action, {
                    method: 'POST',
                    body: formData
                })
                    .then(r => r.json())
                    .then(res => {
                        if (res.ok) {
                            alert('儲存成功！');
                            if (res.redirectUrl) {
                                window.location.href = res.redirectUrl;
                            }
                        } else {
                            var errorMsg = (res.errors && res.errors.join('\n')) || '儲存失敗，請檢查欄位。';
                            alert(errorMsg);
                        }
                    })
                    .catch(err => {
                        alert('儲存時發生網路錯誤：' + (err?.message || err));
                    })
                    .finally(() => {
                        if (saveButton) {
                            saveButton.disabled = false;
                            saveButton.innerHTML = originalButtonText;
                        }
                    });

            }).catch(function (err) {
                alert('Unlayer 匯出失敗：' + (err?.message || err));
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
                        name: q(opts.nameSelector)?.value || '',
                        vars: (q('#VarsJson')?.value || '').trim()
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
                    if (id === 'VarsJson') schedule();
                });
            });

            setInterval(update, 1200);
        },

        _writePreview: function (html) {
            var opts = this.opts;
            var s = q(opts.subjectSelector)?.value || '';
            var to = q(opts.recipientSelector)?.value || '';
            var name = q(opts.nameSelector)?.value || '';
            var tokenMap = Object.assign({}, readVarsJson(), {
                Recipient: to || '',
                Name: name || '',
                Subject: s || ''
            });
            var h = (html || '');
            Object.keys(tokenMap).forEach(function (key) {
                var val = tokenMap[key] ?? '';
                var re = new RegExp('\\{\\{\\s*' + key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s*\\}\\}', 'gi');
                h = h.replace(re, String(val));
            });

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
        },

        // 把 token 插入目前選到的「文字」區塊
        insertTokenToSelected: function (token) {
            try {
                var sel = unlayer.getSelected();
                if (!sel || sel.type !== 'text') {
                    alert('請先在編輯器內點一下「文字」區塊再試。');
                    return false;
                }
                var html = (sel.values && sel.values.text) || '';
                var updated = html + token; // 簡單：附加在結尾；若要游標位置需另做 caret 管理
                unlayer.setBlock(sel.id, { values: { text: updated } });
                return true;
            } catch (err) {
                console.warn('insertTokenToSelected failed:', err);
                alert('無法插入，請確認已選到文字區塊。');
                return false;
            }
        }
    };

    w.UnlayerMail = UnlayerMail;
})(window, document);
