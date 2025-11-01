<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { getBanners } from '@/api/catalog'

const items = ref<Array<{ id: number; imageUrl: string; link?: string }>>([])
const i = ref(0)
let timer: number | null = null

async function load() {
  try {
    items.value = await getBanners()
  } catch {
    // 後端尚未提供時，用暫存資料
    items.value = [
      { id: 1, imageUrl: '/banner1.jpg' },
      { id: 2, imageUrl: '/banner1.jpg' },
    ]
  }
}
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

onMounted(async () => {
  await load()
  play()
})
onUnmounted(stop)
</script>

<template>
  <div class="wrap container">
    <div class="viewport">
      <img v-if="items.length" :src="items[i].imageUrl" alt="" />
    </div>
    <button class="nav prev" @click="prev">‹</button>
    <button class="nav next" @click="next">›</button>
  </div>
</template>

<style scoped>
.wrap {
  position: relative;
  margin: 12px auto;
  background: #fff;
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid #e9ecef;
}
.viewport {
  height: 300px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f6f7f9;
}
.viewport img {
  max-height: 100%;
  max-width: 100%;
}
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
}
.prev {
  left: 12px;
}
.next {
  right: 12px;
}
</style>
