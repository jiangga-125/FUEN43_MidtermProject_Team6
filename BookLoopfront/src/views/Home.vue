<script setup lang="ts">
import AdPopupAndTopBar from '@/components/AdPopupAndTopBar.vue'
import BannerCarousel from '@/components/BannerCarousel.vue'
</script>

<template>
  <!-- 🧡 頂部廣告區 -->
  <div class="ad-top-wrapper">
    <AdPopupAndTopBar />
  </div>

  <!-- 💙 輪播廣告區 -->
  <div class="ad-carousel-wrapper">
    <BannerCarousel />
  </div>

  <!-- 📘 首頁內容 -->
  <div class="home-page p-6">
    <h2>簿錄書城（前台）首頁</h2>
    <p>前台首頁內容</p>
  </div>
</template>

<style scoped>
/* 🧡 頂部廣告容器 */
.ad-top-wrapper {
  width: 100%;
  height: 240px;
  max-width: 1200px;
  height: 180px;
  margin: 20px auto 10px;
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.08);
  display: flex;
  justify-content: center;
  align-items: center;
}

/* 💙 輪播廣告容器 */
.ad-carousel-wrapper {
  width: 100%;
  max-width: 1200px;
  height: 300px;
  margin: 0 auto 30px;
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.08);
  display: flex;
  justify-content: center;
  align-items: center;
}

/* 讓廣告圖片自動裁切填滿，不變形 */
.ad-top-wrapper img,
.ad-carousel-wrapper img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

/* 📘 首頁內容 */
.home-page {
  display: flex;
  flex-direction: column;
  align-items: center;
}

/* 📱 RWD 響應式設定 */
@media (max-width: 992px) {
  .ad-top-wrapper {
    height: 140px; /* 平板：高度稍微縮小 */
  }
  .ad-carousel-wrapper {
    height: 220px;
  }
}

@media (max-width: 576px) {
  .ad-top-wrapper {
    height: 100px; /* 手機：縮小到橫幅比例 */
    border-radius: 8px;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.05);
  }
  .ad-carousel-wrapper {
    height: 160px;
    border-radius: 8px;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.05);
  }
}
</style>
