<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
const router = useRouter()

const q = ref('')

function go() {
  const s = q.value.trim()
  if (!s) return
  // 先直接導到 /member 當作 Demo；等你有 /search 頁後改成 /search?q=...
  router.push({ path: '/member', query: { q: s } })
}
</script>

<template>
  <header class="hdr container">
    <div class="brand">簿錄書城</div>
    <div class="search">
      <input v-model="q" placeholder="請輸入書名、作者、ISBN…" @keyup.enter="go" />
      <button @click="go">搜尋</button>
    </div>
    <div class="logo">BOOKLOOP</div>
  </header>
</template>

<style scoped>
.hdr {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px 0;
}
.brand {
  font-weight: 800;
  font-size: 20px;
}
.search {
  flex: 1;
  display: flex;
  gap: 8px;
}
.search input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid #e1e1e1;
  border-radius: 999px;
}
.search button {
  padding: 10px 16px;
  border-radius: 999px;
  border: 0;
  background: #d4a017;
  color: #fff;
  cursor: pointer;
}
.logo {
  padding: 8px 12px;
  border: 1px solid #eee;
  border-radius: 12px;
  background: #fff;
}
</style>
