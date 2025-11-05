<!-- src/components/HeaderBar.vue (覆蓋用) -->
<template>
  <header class="headerbar bg-white py-2 shadow-sm">
    <div class="container d-flex align-items-center gap-3">
      <!-- LOGO -->
      <router-link to="/" class="me-3 text-decoration-none"> </router-link>

      <!-- 搜尋欄：按 Enter、按鈕皆會觸發搜尋 -->
      <div class="flex-grow-1">
        <div class="input-group">
          <input
            v-model="searchText"
            @keyup.enter="submitSearch"
            type="search"
            class="form-control"
            placeholder="請輸入書名、作者、ISBN..."
            aria-label="搜尋書籍"
          />
          <button class="btn btn-primary" @click="submitSearch" type="button">搜尋</button>
        </div>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'

const router = useRouter()
const route = useRoute()

// 初值取自 route.query.q（若你已在 /listings?q=xxx 時返回 header，就會顯示）
const searchText = ref<string>((route.query.q as string) || '')

/**
 * submitSearch: 按下搜尋會導到 /listings?q=xxx
 * 若 searchText 為空，會導到 /listings（不帶 q）
 * 若你想導到其他 path（例如 /used），把 basePath 改成你要的路徑
 */
function submitSearch() {
  const q = (searchText.value || '').trim()
  const basePath = '/listings' // <- 若要導到別的頁，改這裡

  if (!q) {
    router.push({ path: basePath })
    return
  }

  router.push({
    path: basePath,
    query: {
      q,
      page: '1',
    },
  })
}

// 當外部 route 的 query.q 變動時（例如按 browser back/forward），同步 input
watch(
  () => route.query.q,
  (val) => {
    searchText.value = String(val || '')
  },
)
</script>

<style scoped>
.headerbar {
  position: sticky;
  top: 60px;
  z-index: 1050;
}
.input-group .form-control {
  min-width: 420px;
}
@media (max-width: 768px) {
  .input-group .form-control {
    min-width: 150px;
  }
}
</style>
