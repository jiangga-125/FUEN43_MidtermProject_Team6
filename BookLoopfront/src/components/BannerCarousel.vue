<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { getBanners } from '@/api/catalog'

// 🖼️ 圖片路徑修正
function fixUrl(url: string) {
  if (!url) return '/placeholder.png'
  if (url.startsWith('http') || url.includes('/images/ads/')) return url
  return `https://localhost:7176/images/ads/${url}`
}

const items = ref<Array<{ id: number; imageUrl: string; link?: string }>>([])
const i = ref(0) // 當前圖片索引
let timer: number | null = null

async function load() {
  try {
    const ads = await getBanners()
    // ✅ 只抓 position = 'Carousel' 的廣告
    items.value = ads.filter((a: any) => a.position === 'Carousel')
  } catch {
    // 後端尚未提供時，用暫存資料
    items.value = [
      { id: 1, imageUrl: '1.jpg' },
      { id: 2, imageUrl: '2.jpg' },
    ]
  }
}



// ⏯️ 自動播放控制
function play() {
  stop()
  timer = window.setInterval(() => {
    next()
  }, 4000)
}
function stop() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}
function prev() {
  i.value = (i.value - 1 + items.value.length) % items.value.length
}
function next() {
  i.value = (i.value + 1) % items.value.length
}
function goTo(index: number) {
  i.value = index
  play() // 點擊後重啟播放
}

onMounted(async () => {
  await load()
  play()
})
onUnmounted(stop)
</script>

<template>
  <div class="wrap container">
    <div class="viewport">
      <!-- ✨ 使用 Vue 內建 Transition 增加淡入淡出 -->
      <transition name="fade" mode="out-in">
        <img
          v-if="items.length"
          :key="items[i].id"
          :src="fixUrl(items[i].imageUrl)"
          alt="廣告圖片"
        />
      </transition>
    </div>

    <!-- ⬅️➡️ 導覽按鈕 -->
    <button class="nav prev" @click="prev">‹</button>
    <button class="nav next" @click="next">›</button>

    <!-- 🔵 圓點指示器 -->
    <div class="dots">
      <span
        v-for="(item, index) in items"
        :key="item.id"
        class="dot"
        :class="{ active: i === index }"
        @click="goTo(index)"
      ></span>
    </div>
  </div>
</template>

<style scoped>
.wrap {
  position: relative;
  margin: 24px auto;
  width: 90%;
  background: #fff;
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid #e9ecef;
   box-shadow: 0 4px 10px rgba(0, 0, 0, 0.05);
}

.viewport {
  height: 300px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f6f7f9;
  position: relative;
}
.viewport img {
  max-height: 100%;
  max-width: 100%;
  border-radius: 8px;
}

/* 導覽按鈕 */
.nav {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  width: 36px;
  height: 36px;
  border-radius: 999px;
  border: 1px solid #ddd;
  background: #fff;
  cursor: pointer;
  transition: background 0.3s, box-shadow 0.3s;
}
.nav:hover {
  background: #f1f1f1;
  box-shadow: 0 0 4px rgba(0, 0, 0, 0.2);
}
.prev {
  left: 12px;
}
.next {
  right: 12px;
}

/* 🔵 圓點指示器 */
.dots {
  position: absolute;
  bottom: 12px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  gap: 8px;
}
.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background-color: #ccc;
  cursor: pointer;
  transition: background-color 0.3s, transform 0.3s;
}
.dot:hover {
  background-color: #999;
  transform: scale(1.2);
}
.dot.active {
  background-color: #000000;
  transform: scale(1.3);
}

/* ✨ 淡入淡出動畫 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.8s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
