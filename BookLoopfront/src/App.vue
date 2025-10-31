<script setup lang="ts">
import TopBar from './components/TopBar.vue'
import HeaderBar from './components/HeaderBar.vue'
import NoticeBar from './components/NoticeBar.vue'
import BannerCarousel from './components/BannerCarousel.vue'
import SidebarCategories from './components/SidebarCategories.vue'
import ProductTabs from './components/ProductTabs.vue'
import { computed, ref } from 'vue'

/* 依路由判斷是否首頁 */
import { useRoute, RouterView } from 'vue-router'
const route = useRoute()
const isHome = computed(() => route.path === '/') // 只有首頁為 true

/* 建立可為 null 的分類 id */
const selectedCategoryId = ref<number | null>(null) // null=全部

function onPickCategory(id: number | null) {
  selectedCategoryId.value = id
}

/* ===========================
   【修改】Auth Debug 面板（可開關，方便測試 token 流程）
   - 不會影響你現有頁面布局，預設收合
   - 需要你已經在 src/api/auth.ts 與 src/api/http.ts 實作 loginToken / logout / setAccessToken
   =========================== */
import { loginToken, logout as apiLogout } from './api/auth' // 【修改】確定檔案路徑正確
import http, { setAccessToken } from './api/http' // 【修改】請確認 http.ts 有匯出 setAccessToken

const debugOpen = ref(false)
const dbgAccount = ref('admin@bookstore.local')
const dbgPassword = ref('Admin@12345!')
const dbgMsg = ref('Auth debug panel: 尚未操作')

async function doDebugLogin() {
  try {
    const res = await loginToken(dbgAccount.value, dbgPassword.value)
    dbgMsg.value = `登入成功：access token 取得，expires=${res.expires}`
  } catch (e: any) {
    dbgMsg.value = '登入失敗：' + (e?.response?.data?.message ?? e.message ?? JSON.stringify(e))
  }
}

async function doDebugCallHello() {
  try {
    const r = await http.get('/secure/hello')
    dbgMsg.value = '呼叫成功：' + JSON.stringify(r.data)
  } catch (e: any) {
    dbgMsg.value = '呼叫失敗：' + (e?.response?.data?.message ?? e.message ?? JSON.stringify(e))
  }
}

async function doDebugRefresh() {
  try {
    // 模擬前端 token 過期：清記憶體 token -> interceptor 會呼 /auth/refresh
    setAccessToken(null)
    const r = await http.get('/secure/hello')
    dbgMsg.value = 'refresh 後呼叫成功：' + JSON.stringify(r.data)
  } catch (e: any) {
    dbgMsg.value = 'refresh 失敗：' + (e?.response?.data?.message ?? e.message ?? JSON.stringify(e))
  }
}

async function doDebugLogout() {
  try {
    await apiLogout()
    setAccessToken(null)
    dbgMsg.value = '已登出（前端 token 已清，後端若有 revoke refresh token 也會生效）'
  } catch (e: any) {
    dbgMsg.value = '登出失敗：' + (e?.message ?? JSON.stringify(e))
  }
}
</script>

<template>
  <!-- TopBar（站內共用導覽） -->
  <TopBar />

  <!-- ====== 新增：右上角 Debug 控制（不會擾亂你的原始 layout） ====== -->
  <div class="auth-debug-toggle">
    <button class="debug-btn" @click="debugOpen = !debugOpen">Auth Debug</button>
  </div>

  <div v-show="debugOpen" class="auth-debug-panel">
    <div class="panel-row">
      <label>帳號</label>
      <input v-model="dbgAccount" />
      <label>密碼</label>
      <input v-model="dbgPassword" type="password" />
      <button @click="doDebugLogin">Login (token)</button>
      <button @click="doDebugLogout">Logout</button>
    </div>

    <div class="panel-row">
      <button @click="doDebugCallHello">呼 /secure/hello</button>
      <button @click="doDebugRefresh">模擬過期並觸發 refresh</button>
    </div>

    <div class="panel-msg">{{ dbgMsg }}</div>
  </div>
  <!-- ====== Debug End ====== -->

  <!-- 首頁專屬區塊：只有在 '/' 才會渲染 -->
  <template v-if="isHome">
    <HeaderBar />
    <NoticeBar />
    <BannerCarousel />
    <main class="container layout">
      <!-- 左：分類清單，點擊後更新 selectedCategoryId -->
      <SidebarCategories :selected-id="selectedCategoryId" @select="onPickCategory" />
      <!-- 右：商品區，接收分類 id -->
      <ProductTabs :category-id="selectedCategoryId" />
    </main>
  </template>

  <!-- 非首頁：只顯示各自頁面的內容（乾淨的新頁感） -->
  <template v-else>
    <div class="container page-container">
      <RouterView />
    </div>
  </template>
</template>

<style scoped>
/* 首頁兩欄 */
.layout {
  display: grid;
  grid-template-columns: 260px 1fr;
  gap: 20px;
  margin: 28px auto;
}

/* 非首頁的通用頁面容器（和首頁對齊、位置頂部） */
.page-container {
  max-width: 1200px;
  margin: 28px auto;
  padding: 0 16px;
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: 1fr;
  }
}

/* ====== Debug 面板樣式（可自行改造） ====== */
.auth-debug-toggle {
  position: fixed;
  right: 16px;
  top: 78px; /* 稍微往下避開 TopBar */
  z-index: 1200;
}
.debug-btn {
  background: #111827;
  color: white;
  border-radius: 6px;
  padding: 6px 10px;
  border: none;
  cursor: pointer;
}

.auth-debug-panel {
  position: fixed;
  right: 16px;
  top: 110px;
  width: 420px;
  background: white;
  border: 1px solid #ddd;
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.12);
  padding: 12px;
  z-index: 1200;
  border-radius: 6px;
}
.auth-debug-panel .panel-row {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
  align-items: center;
}
.auth-debug-panel input {
  padding: 6px 8px;
  border: 1px solid #ccc;
  border-radius: 4px;
}
.auth-debug-panel .panel-msg {
  margin-top: 6px;
  font-size: 13px;
  color: #333;
  white-space: pre-wrap;
}
</style>
