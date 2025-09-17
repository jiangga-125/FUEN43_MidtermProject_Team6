(function () {
    const form = document.getElementById("createForm");
    const tbody = document.querySelector("#authorsTable tbody");

    function reparseUnobtrusive() {
        // 重新套用 unobtrusive 驗證（動態新增欄位時必須）
        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            const $form = window.jQuery(form);
            $form.removeData("validator");
            $form.removeData("unobtrusiveValidation");
            window.jQuery.validator.unobtrusive.parse($form);
        }
    }

    function currentRows() {
        return Array.from(tbody.querySelectorAll("tr"));
    }

    function renumberRows() {
        const rows = currentRows();
        let checkedIndex = -1;

        // 找出目前被勾選的 radio（舊索引）
        rows.forEach((tr, oldIdx) => {
            const radio = tr.querySelector('input[type="radio"][name="PrimaryIndex"]');
            if (radio && radio.checked) checkedIndex = oldIdx;
        });

        rows.forEach((tr, newIdx) => {
            // 調整 name/index 與 id/for
            const hiddenId = tr.querySelector('input[type="hidden"][name^="Authors["]');
            const nameInput = tr.querySelector('input[type="text"][name^="Authors["]');
            const radio = tr.querySelector('input[type="radio"][name="PrimaryIndex"]');
            const label = tr.querySelector('label[for^="PrimaryIndex_"]');

            if (hiddenId) hiddenId.setAttribute("name", `Authors[${newIdx}].ListingAuthorID`);
            if (nameInput) {
                nameInput.setAttribute("name", `Authors[${newIdx}].AuthorName`);
                nameInput.setAttribute("id", `Authors_${newIdx}__AuthorName`);
                // 對應 MVC 的驗證 span data-valmsg-for
                const valSpan = tr.querySelector('span.field-validation-valid, span.field-validation-error');
                if (valSpan) valSpan.setAttribute("data-valmsg-for", `Authors[${newIdx}].AuthorName`);
            }

            if (radio) {
                const newId = `PrimaryIndex_${newIdx}`;
                radio.setAttribute("id", newId);
                radio.setAttribute("value", newIdx);
                if (label) label.setAttribute("for", newId);
            }
        });

        // 若沒有任何 radio 被選，預設第一列為主作者
        const anyChecked = rows.some(tr => tr.querySelector('input[type="radio"][name="PrimaryIndex"]')?.checked);
        if (!anyChecked && rows.length > 0) {
            const firstRadio = rows[0].querySelector('input[type="radio"][name="PrimaryIndex"]');
            if (firstRadio) firstRadio.checked = true;
        }

        reparseUnobtrusive();
    }

    function addRow(afterTr) {
        const newIndex = currentRows().length;

        const tr = document.createElement("tr");
        tr.innerHTML = `
      <td>
        <input type="hidden" name="Authors[${newIndex}].ListingAuthorID" value="" />
        <input class="form-control" name="Authors[${newIndex}].AuthorName" id="Authors_${newIndex}__AuthorName" />
        <span class="text-danger field-validation-valid" data-valmsg-for="Authors[${newIndex}].AuthorName" data-valmsg-replace="true"></span>
      </td>
      <td class="text-center">
        <input type="radio" id="PrimaryIndex_${newIndex}" class="form-check-input" name="PrimaryIndex" value="${newIndex}" />
        <label class="form-check-label ms-1" for="PrimaryIndex_${newIndex}">主作者</label>
      </td>
      <td class="text-center">
        <button type="button" class="btn btn-outline-primary btn-sm add-btn">新增</button>
        <button type="button" class="btn btn-outline-danger btn-sm delete-btn">刪除</button>
      </td>
    `;

        if (afterTr) {
            afterTr.insertAdjacentElement("afterend", tr);
        } else {
            tbody.appendChild(tr);
        }

        // 新增一列後，如果之前沒有任何選取，就把新列設為主作者（可依需求改）
        const anyChecked = tbody.querySelector('input[type="radio"][name="PrimaryIndex"]:checked');
        if (!anyChecked) {
            const radio = tr.querySelector('input[type="radio"][name="PrimaryIndex"]');
            if (radio) radio.checked = true;
        }

        reparseUnobtrusive();
        renumberRows();
    }

    function deleteRow(tr) {
        const rows = currentRows();
        if (rows.length <= 1) {
            alert("至少需要 1 位作者。");
            return;
        }
        tr.remove();
        renumberRows();
    }

    // 事件委派：新增 / 刪除
    tbody.addEventListener("click", function (e) {
        const target = e.target;
        if (!(target instanceof HTMLElement)) return;

        if (target.classList.contains("add-btn")) {
            const tr = target.closest("tr");
            addRow(tr);
        } else if (target.classList.contains("delete-btn")) {
            const tr = target.closest("tr");
            deleteRow(tr);
        }
    });

    // 初始一次，避免手動修改 DOM 後 index 不一致
    renumberRows();
})();