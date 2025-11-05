<template>
  <div class="marquee">
    <p class="marquee-text">{{ messages[currentIndex] }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

// ✅ 跑馬燈要顯示的寒暄文字
const messages = [
  "🌞 歡迎來到 BookLoop，祝你有美好的一天！🌞",
  "📚 今天也要記得看看書，讓靈魂充實一下～📚",
  "🎉 別忘了領取會員優惠券喔！🎉",
  "💎 感謝你的支持，BookLoop 陪你一起閱讀生活。💎"
]

// ✅ 當前顯示第幾句
const currentIndex = ref(0)
let timer: number

// ✅ 每 18 秒切換一句
onMounted(() => {
  timer = window.setInterval(() => {
    currentIndex.value = (currentIndex.value + 1) % messages.length
  }, 18000)
})

onUnmounted(() => clearInterval(timer))
</script>

<style scoped>
.marquee {
  width: 100%;
  height: 40px; /* ⭐ 加上固定高度，避免容器塌陷 */
  overflow: hidden;
  position: relative; /* ⭐ 讓絕對定位文字可參考這個容器 */
  background-color: 	#81C0C0;
  border: 2px solid 	#81C0C0;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 跑馬燈文字樣式 */
.marquee-text {
  position: absolute;
  white-space: nowrap;
  font-size: 18px;
  font-weight: 600;
  color: #fefefe;
 padding-top: 15px;
  animation: scrollText 10s linear infinite;
  
}

/* 跑馬燈動畫 */
@keyframes scrollText {
  from {
    transform: translateX(100%);
  }
  to {
    transform: translateX(-100%);
  }
}
</style>
