<script setup lang="ts">
import AdPopupAndTopBar from '@/components/AdPopupAndTopBar.vue'
import BannerCarousel from '@/components/BannerCarousel.vue';

</script>

<template>
  <!-- 🧡 頂部橫幅 + 彈出廣告 -->
  <div class="top-banner-area">
    <AdPopupAndTopBar />
  </div>

  <!-- 🩵 首頁主要內容 -->
  <div class="home-page p-6">
    <h2>簿錄書城（前台）首頁</h2>
    <p>前台首頁內容</p>
    <BannerCarousel />
  </div>
</template>



<style scoped>
.home-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-top: 200px;
}

.top-banner-area {
  width: 100%;
  position: relative;
  z-index: 9999;
  margin-bottom: 12px;
   border: 2px solid red; /* 臨時測試用邊框 */
}
</style>
