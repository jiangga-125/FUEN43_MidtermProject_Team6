/* wwwroot/js/mail-preview.js  — 寄出一致版
   目標：預覽 = 寄出（用 CKEditor getData() 的原始 HTML，不改結構/尺寸）
*/
(function (w, d) {
    var MailPreview = {
        init: function (opts) {
            this.editorId = opts.editorId || 'editor';
            this.subjectSelector = opts.subjectSelector || '[name="Subject"]';
            this.recipientId = opts.recipientId || 'pv-recipient';
            this.nameId = opts.nameId || 'pv-name';
            this.jsonId = opts.jsonId || 'pv-token';
            this.subjectOutId = opts.subjectOutId || 'pv-subject';
            this.frameId = opts.frameId || 'pv-frame';
            this.copyBtnId = opts.copyBtnId || 'btn-copy-json';

            this.$subjectIn = d.querySelector(this.subjectSelector);
            this.$recipient = d.getElementById(this.recipientId);
            this.$name = d.getElementById(this.nameId);
            this.$json = d.getElementById(this.jsonId);
            this.$subjectOut = d.getElementById(this.subjectOutId);
            this.$frame = d.getElementById(this.frameId);

            this._bind();
            this._schedule();
        },

        // 直接讀輸入欄位
        _getSubject: function () {
            return this.$subjectIn ? this.$subjectIn.value : '';
        },

        // 直接取 CKEditor getData()；僅移除編輯器控制節點/屬性，不改動內容結構/尺寸
        _getBodyHtml: function () {
            var raw = (w.editorInstance && typeof w.editorInstance.getData === 'function')
                ? w.editorInstance.getData()
                : (d.getElementById(this.editorId)?.value || '');

            // 轉成 DOM 只為了移除控制節點，保持內容原樣
            var doc = new DOMParser().parseFromString(raw, 'text/html');
            var root = doc.body;

            // 移除 CK 控制用節點（不影響內容）
            root.querySelectorAll('.ck-widget__type-around, .ck-widget__resizer, .ck-size-view, .ck-fake-selection-container')
                .forEach(function (el) { el.remove(); });

            // 去除編輯屬性（避免在 iframe 內可編輯）
            root.querySelectorAll('[contenteditable],[draggable]')
                .forEach(function (el) { el.removeAttribute('contenteditable'); el.removeAttribute('draggable'); });

            return root.innerHTML;
        },

        // 簡單 Token 渲染（與伺服器端邏輯對齊時，可替換成同一段渲染器）
        _renderTokens: function (tpl, dict) {
            return (tpl || '').replace(/\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}/g, function (_, k) {
                return (dict && Object.prototype.hasOwnProperty.call(dict, k) && dict[k] != null) ? String(dict[k]) : '';
            });
        },

        // 寫入 iframe：最小樣式，尊重原本寬度/結構；只讓對齊 class 有明顯效果
        _writeFrame: function (html) {
            if (!this.$frame) return;

            // 最小樣式：不要強制壓圖寬，保持原樣；只做清浮動與基本排版
            var css = [
                'html,body{margin:0;padding:0;background:#fff;}',
                'html,body{ margin:0; padding:12px; background:#fff; ' +
                'overflow-wrap:anywhere; word-break:break-word; }',

                // 預覽容器：讓我們可以對整頁內容做等比縮放
                '#pv-root{position:relative; transform-origin: top left;}',
                // 內容外再墊一層，避免 transform 對 body 造成影響
                '#pv-content{padding:12px; box-sizing:border-box;}',
                // 清浮動
                '.ck-content::after{content:"";display:block;clear:both;}',
                // 圖片等比例，但不要強制 100% 寬（保留原尺寸）
                'figure.image > img{display:block; height:auto !important;}',
                // 舊法保底：<p style="text-align:.."><img>
                'p[style*="text-align:center"] > img{display:inline-block;}',
                'p[style*="text-align:right"]  > img{display:inline-block;}',
                // 標題
                'figure.image figcaption{font-size:.85em;color:#666;text-align:center;margin-top:.25rem;}'
            ].join('');

            var doc = this.$frame.contentDocument || this.$frame.contentWindow.document;

            try {
                doc.open();
                doc.write('<!doctype html><html><head><meta charset="utf-8"><style>' + css + '</style></head><body>');
                // 用兩層包住內容：pv-root 做 scale，pv-content 放真正內容
                doc.write('<div id="pv-root"><div id="pv-content" class="ck-content">');
                doc.write(html || '');
                doc.write('</div></div></body></html>');
                doc.close();
            } catch (e) {
                try { doc.body.innerHTML = '<div id="pv-root"><div id="pv-content" class="ck-content">' + (html || '') + '</div></div>'; } catch (_) { }
            }

            // === 等比縮放（只縮小、不放大）===
            var frame = this.$frame;
            var idoc = frame.contentDocument || frame.contentWindow.document;

            var root = idoc.getElementById('pv-root');
            var content = idoc.getElementById('pv-content');
            if (!root || !content) return;

            var fit = () => {
                // 內容「自然寬度 / 高度」（未縮放狀態）
                // 用 scrollWidth/Height 取自然大小，較不受邊界影響
                var naturalW = content.scrollWidth || content.getBoundingClientRect().width || 1;
                var naturalH = content.scrollHeight || content.getBoundingClientRect().height || 1;

                // 預覽欄的可用寬度（扣掉滾動條）
                var viewportW = frame.clientWidth || frame.getBoundingClientRect().width || naturalW;

                // 只縮小、不放大：當內容比面板寬才縮放
                var scale = Math.min(1, viewportW / naturalW);

                // 等比縮放整頁
                root.style.transform = 'scale(' + scale + ')';
                root.style.width = naturalW + 'px';                // 以自然寬度為基準
                root.style.height = naturalH + 'px';

                // 讓 iframe 內頁高度剛好能捲動完整內容
                idoc.body.style.height = (naturalH * scale) + 'px';
                idoc.documentElement.style.height = (naturalH * scale) + 'px';
            };

            // 初次套用 & 延後一個 frame 等圖片排版完成
            fit();
            this.$frame.contentWindow.requestAnimationFrame(fit);

            // 視窗改變寬度（例如頁面 layout 調整）時重算
            var hostRecalc = () => fit();
            window.addEventListener('resize', hostRecalc);

            // 內容有圖片載入完成後再重算一次（避免延載圖片造成量測太小）
            Array.from(content.querySelectorAll('img')).forEach(function (img) {
                if (!img.complete) img.addEventListener('load', fit, { once: true });
            });
        },

        // 組合流程：Subject + Token + HTML → 寫入 iframe
        _update: function () {
            var extra = {};
            try { extra = (this.$json && this.$json.value) ? JSON.parse(this.$json.value) : {}; } catch (_) { }

            var model = Object.assign({}, extra, {
                Recipient: this.$recipient?.value || '',
                Name: this.$name?.value || ''
            });

            var subj = this._renderTokens(this._getSubject(), model);
            var html = this._renderTokens(this._getBodyHtml(), model);

            if (this.$subjectOut) this.$subjectOut.textContent = subj || '';
            this._writeFrame(html);
            this._lastHtml = html;
        },

        _schedule: function () { clearTimeout(this._t); this._t = setTimeout(this._update.bind(this), 160); },

        _bind: function () {
            var self = this;

            // 右側三欄 + Subject 變更時重繪
            d.addEventListener('input', function (e) {
                var n = e.target.name || e.target.id || '';
                if (n === 'Subject' || n === self.recipientId || n === self.nameId || n === self.jsonId) self._schedule();
            });

            // 觀察 CK 視覺區：內容/樣式/類別變更都觸發（縮放與對齊都會更新）
            function closestByClass(el, cls) { while (el) { if (el.classList?.contains(cls)) return el; el = el.parentNode; } return null; }
            function setupObserver() {
                var ta = d.getElementById(self.editorId);
                var root = ta ? closestByClass(ta, 'ck-editor') : null;
                var target = root ? root.querySelector('.ck-content') : d.querySelector('.ck-content');
                if (!target) return false;

                new MutationObserver(function () { self._schedule(); })
                    .observe(target, {
                        childList: true,
                        subtree: true,
                        characterData: true,
                        attributes: true,
                        attributeFilter: ['style', 'class']
                    });
                return true;
            }

            var tries = 0, t = setInterval(function () {
                if (setupObserver() || ++tries > 30) { clearInterval(t); self._schedule(); }
            }, 200);

            // 輪詢保險（避免極偶發狀況漏觸發）
            setInterval(function () {
                var current = self._getBodyHtml();
                if (current !== self._lastHtml) self._schedule();
            }, 1200);

            // 複製 JSON（不碰編輯器內容）
            var btn = d.getElementById(self.copyBtnId);
            if (btn) btn.addEventListener('click', function () {
                try { navigator.clipboard?.writeText(self.$json?.value || ''); } catch (_) { }
            });
        }
    };

    // 匯出到全域
    w.MailPreview = MailPreview;
})(window, document);
