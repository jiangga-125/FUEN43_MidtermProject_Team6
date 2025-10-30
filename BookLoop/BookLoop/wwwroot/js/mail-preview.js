/* wwwroot/js/mail-preview.js  — 寄出一致版（移除 JSON 變數）
   目標：預覽 = 寄出（用 CKEditor getData() 的原始 HTML，不改結構/尺寸）
*/
(function (w, d) {
    var MailPreview = {
        init: function (opts) {
            this.editorId = opts.editorId || 'editor';
            this.subjectSelector = opts.subjectSelector || '[name="Subject"]';
            this.recipientId = opts.recipientId || 'pv-recipient';
            this.nameId = opts.nameId || 'pv-name';
            this.subjectOutId = opts.subjectOutId || 'pv-subject';
            this.frameId = opts.frameId || 'pv-frame';

            this.$subjectIn = d.querySelector(this.subjectSelector);
            this.$recipient = d.getElementById(this.recipientId);
            this.$name = d.getElementById(this.nameId);
            this.$subjectOut = d.getElementById(this.subjectOutId);
            this.$frame = d.getElementById(this.frameId);

            this._bind();
            this._schedule();
        },

        _getSubject: function () {
            return this.$subjectIn ? this.$subjectIn.value : '';
        },

        _getBodyHtml: function () {
            var raw = (w.editorInstance && typeof w.editorInstance.getData === 'function')
                ? w.editorInstance.getData()
                : (d.getElementById(this.editorId)?.value || '');

            var doc = new DOMParser().parseFromString(raw, 'text/html');
            var root = doc.body;

            // 移除 CK 控制 UI
            root.querySelectorAll('.ck-widget__type-around, .ck-widget__resizer, .ck-size-view, .ck-fake-selection-container')
                .forEach(function (el) { el.remove(); });

            // 移除編輯屬性
            root.querySelectorAll('[contenteditable],[draggable]')
                .forEach(function (el) { el.removeAttribute('contenteditable'); el.removeAttribute('draggable'); });

            return root.innerHTML;
        },

        _renderTokens: function (tpl, dict) {
            return (tpl || '').replace(/\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}/g, function (_, k) {
                return (dict && Object.prototype.hasOwnProperty.call(dict, k) && dict[k] != null) ? String(dict[k]) : '';
            });
        },

        _writeFrame: function (html) {
            if (!this.$frame) return;

            var css = [
                'html,body{ margin:0; padding:12px; background:#fff; overflow-wrap:anywhere; word-break:break-word; }',
                '#pv-root{position:relative; transform-origin: top left;}',
                '#pv-content{padding:12px; box-sizing:border-box;}',
                '.ck-content::after{content:"";display:block;clear:both;}',
                'figure.image > img{display:block; height:auto !important;}',
                'p[style*="text-align:center"] > img{display:inline-block;}',
                'p[style*="text-align:right"]  > img{display:inline-block;}',
                'figure.image figcaption{font-size:.85em;color:#666;text-align:center;margin-top:.25rem;}'
            ].join('');

            var doc = this.$frame.contentDocument || this.$frame.contentWindow.document;

            try {
                doc.open();
                doc.write('<!doctype html><html><head><meta charset="utf-8"><style>' + css + '</style></head><body>');
                doc.write('<div id="pv-root"><div id="pv-content" class="ck-content">');
                doc.write(html || '');
                doc.write('</div></div></body></html>');
                doc.close();
            } catch (e) {
                try { doc.body.innerHTML = '<div id="pv-root"><div id="pv-content" class="ck-content">' + (html || '') + '</div></div>'; } catch (_) { }
            }

            var frame = this.$frame;
            var idoc = frame.contentDocument || frame.contentWindow.document;
            var root = idoc.getElementById('pv-root');
            var cont = idoc.getElementById('pv-content');
            if (!root || !cont) return;

            var fit = () => {
                var naturalW = cont.scrollWidth || cont.getBoundingClientRect().width || 1;
                var naturalH = cont.scrollHeight || cont.getBoundingClientRect().height || 1;
                var viewportW = frame.clientWidth || frame.getBoundingClientRect().width || naturalW;

                var scale = Math.min(1, viewportW / naturalW); // 只縮小不放大
                root.style.transform = 'scale(' + scale + ')';
                root.style.width = naturalW + 'px';
                root.style.height = naturalH + 'px';

                idoc.body.style.height = (naturalH * scale) + 'px';
                idoc.documentElement.style.height = (naturalH * scale) + 'px';
            };

            fit();
            this.$frame.contentWindow.requestAnimationFrame(fit);
            window.addEventListener('resize', fit);
            Array.from(cont.querySelectorAll('img')).forEach(function (img) {
                if (!img.complete) img.addEventListener('load', fit, { once: true });
            });
        },

        _update: function () {
            var model = {
                Recipient: this.$recipient?.value || '',
                Name: this.$name?.value || ''
            };

            var subj = this._renderTokens(this._getSubject(), model);
            var html = this._renderTokens(this._getBodyHtml(), model);

            if (this.$subjectOut) this.$subjectOut.textContent = subj || '';
            this._writeFrame(html);
            this._lastHtml = html;
        },

        _schedule: function () { clearTimeout(this._t); this._t = setTimeout(this._update.bind(this), 160); },

        _bind: function () {
            var self = this;

            d.addEventListener('input', function (e) {
                var n = e.target.name || e.target.id || '';
                if (n === 'Subject' || n === self.recipientId || n === self.nameId) self._schedule();
            });

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

            setInterval(function () {
                var current = self._getBodyHtml();
                if (current !== self._lastHtml) self._schedule();
            }, 1200);
        }
    };

    w.MailPreview = MailPreview;
})(window, document);
