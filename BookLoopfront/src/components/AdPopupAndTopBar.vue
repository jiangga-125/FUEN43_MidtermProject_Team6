<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getBanners } from '@/api/catalog'  // 這是你從後端抓廣告的 API

const popupAd = ref<any>(null)
const topBannerAd = ref<any>(null)
const sidebarAd = ref<any>(null)

const isPopupVisible = ref(false)
const isTopBannerVisible = ref(false)

// 從後端載入廣告
// 從後端載入廣告
async function loadAds() {
  const ads = await getBanners()
  console.log('📢 後端回傳廣告資料：', ads)

  // 🎯 根據 position 分類
  const popupAds = ads.filter((a: any) => a.position === 'EventPage')
  const topBannerAds = ads.filter((a: any) => a.position === 'HomeBanner')
  const sidebarAds = ads.filter((a: any) => a.position === 'Sidebar')

  // ✅ 從每個類別中「隨機挑一張」出來
  const pickRandom = (arr: any[]) => arr.length ? arr[Math.floor(Math.random() * arr.length)] : null

  popupAd.value = pickRandom(popupAds)
  topBannerAd.value = pickRandom(topBannerAds)
  sidebarAd.value = pickRandom(sidebarAds)

  console.log('🎯 隨機選中的頂部廣告：', topBannerAd.value)
  console.log('🎯 隨機選中的彈出廣告：', popupAd.value)

  if (popupAd.value) isPopupVisible.value = true
  if (topBannerAd.value) isTopBannerVisible.value = true
}



// 關閉彈窗 → 顯示頂部橫幅
function closePopup() {
  isPopupVisible.value = false
  if (topBannerAd.value) isTopBannerVisible.value = true
}

onMounted(loadAds)
</script>

<template>
  <div>
    <!-- 🧡 頂部廣告 -->
    <transition name="slide-down">
      <div v-if="true" class="top-banner">
  <img :src="topBannerAd?.imageUrl" alt="頂部廣告" />
</div>

    </transition>

    <!-- 💥 彈出式廣告 -->
    <transition name="fade">
      <div v-if="isPopupVisible" class="popup-overlay">
        <div class="popup">
          <button class="close-btn" @click="closePopup">關閉廣告 ✕</button>
          <a :href="popupAd?.linkUrl" target="_blank">
           <img :src="popupAd?.imageUrl" alt="彈出式廣告" />
          </a>
        </div>
      </div>
    </transition>

    <!-- 🧭 側邊欄廣告（如果你有Sidebar區域） -->
    <div v-if="sidebarAd" class="sidebar-ad">
      <a :href="sidebarAd.linkUrl" target="_blank">
        <img :src="`/images/ads/${sidebarAd.imageUrl}`" alt="側邊廣告" />
      </a>
    </div>
  </div>
</template>

<style scoped>
.top-banner {
  top: 0;
  left: 0;
  width: 100vw;           /* 🔥 滿版寬度 (視窗寬度) */
  z-index: 9999;
  background: #fff;       /* 底色避免文字壓到 */
  border-bottom: 1px solid #ddd;
  text-align: center;
  overflow: hidden;       /* 避免圖片外溢 */
  box-shadow: 0 2px 6px rgba(0,0,0,0.1);
}

.top-banner img {
  width: 100%;            /* 🔥 讓圖片隨視窗寬度縮放 */
  height: auto;
  display: block;
  object-fit: cover;      /* 若比例不同，可裁切填滿 */
}

.toggle-btn {
  position: absolute;
  right: 16px;
  bottom: 8px;
  background: rgba(0, 0, 0, 0.5);
  color: #fff;
  border: none;
  border-radius: 6px;
  padding: 2px 8px;
  cursor: pointer;
}

</style>

