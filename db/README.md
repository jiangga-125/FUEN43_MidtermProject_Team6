---

## 🗄️ 資料庫 (db/)

本專案不會上傳實體資料庫檔案（.mdf / .bak），只會放 **SQL 腳本**。

- `db/init/` → 初始化腳本（建立資料庫、Tables、基礎 Schema）
- `db/seed/` → 測試資料腳本（假資料、範例帳號）
- `db/migration/` → 升級腳本（版本升級，如新增欄位 / 新 Table）

每個資料夾都有 `.gitkeep`，確保 Git 能追蹤結構。
