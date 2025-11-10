<script setup lang="ts">
import TopBar from './components/TopBar.vue'
import HeaderBar from './components/HeaderBar.vue'
import NoticeBar from './components/NoticeBar.vue'
import BannerCarousel from './components/BannerCarousel.vue'
import SidebarCategories from './components/SidebarCategories.vue'
import ProductTabs from './components/ProductTabs.vue'
import { computed, ref } from 'vue'
import MailNudge from "@/components/MailNudge.vue";
import FooterContact from './components/FooterContact.vue'

/* 依路由判斷是否首頁 */
import { useRoute, RouterView } from 'vue-router'
import AdPopupAndTopBar from './components/AdPopupAndTopBar.vue'
import Marquee from './components/Marquee.vue'
const route = useRoute()
const isHome = computed(() => route.path === '/') // 只有首頁為 true

/* 建立可為 null 的分類 id */
const selectedCategoryId = ref<number | null>(null) // null=全部

function onPickCategory(id: number | null) {
  console.log('[Parent] onPickCategory', id)
  selectedCategoryId.value = id
}
</script>

<template>
  <!-- TopBar（站內共用導覽） -->
  <TopBar />


  <!-- 首頁專屬區塊：只有在 '/' 才會渲染 -->
  <template v-if="isHome">
    <HeaderBar />
    <AdPopupAndTopBar />
    <Marquee />
    <BannerCarousel />
    <main class="container layout">
      <!-- 左：分類清單，點擊後更新 selectedCategoryId -->
      <SidebarCategories :selected-id="selectedCategoryId" @select="onPickCategory" />
      <!-- 右：商品區，接收分類 id -->
      <ProductTabs :categoryId="selectedCategoryId" />
    </main>
      <RouterView />
  <MailNudge />
  <FooterContact />
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

