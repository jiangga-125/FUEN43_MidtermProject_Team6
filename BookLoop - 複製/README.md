# 📚 BookLoop 專案

## 1️⃣ 檔案放置

- **Controller + View** → 丟到對應的 Area
  - 例：`Areas/Books/Controllers/BooksController.cs`
  - 例：`Areas/Books/Views/Books/Index.cshtml`
- **Models / Services** → 放在專案根目錄的 `Models/`、`Services/` 資料夾

---

## 2️⃣ 資料庫連線設定（User Secrets）=>本機端

1. 在 Visual Studio → 右鍵專案 → **Manage User Secrets**
2. 在 `secrets.json` 填入：

   ```json
   {
      你原本的appsettings.json的字串

   	"ConnectionStrings": {
   		"DefaultConnection": "Server=你的SQL;Database=BookLoop;Trusted_Connection=True;TrustServerCertificate=True"
   	}

   }
   ```

---

## 3️⃣ Git Push

- 建立分支：
  ```
  feature/{模組}-{功能}
  ```
  範例：
  - `feature/Books-CRUD`
  - `feature/Account-Login`

---

Commit message **包含 Jira issue key + Area + 功能描述**

格式：

```
[MP-XX] [Area] 功能描述
```

範例：

```
[MP-12] [Books] 新增書籍 CRUD
[MP-08] [Account] 完成登入功能
[MP-15] [Borrow] 借閱紀錄查詢 API
```

---

1. Push 後開 **PR (Pull Request)**
2. PR 標題格式與 Commit 相同：
   ```
   [MP-XX] [Area] 功能描述
   ```
3. PR 描述需包含：
   - Jira 任務編號（MP-XX）
   - 功能大綱
   - 測試方式（選填）

---

## 6️⃣ 專案啟動方式

1. Clone 專案
2. 用 Visual Studio 開啟 → Ctrl+F5 執行
3. 預設首頁：
   ```
   https://localhost:xxxx/
   ```
4. Area 測試：
   - 書籍模組 → `/Books/Books/Index`
   - 借閱模組 → `/Borrow/Borrow/Index`

---
