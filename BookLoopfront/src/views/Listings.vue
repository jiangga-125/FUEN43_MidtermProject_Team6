<!-- src/views/Listings.vue -->
<template>
  <div class="container my-4">
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h3 class="mb-0">二手書市集</h3>
      <div class="text-muted small">
        搜尋：<span class="fw-bold">{{ q || '-' }}</span>
      </div>
    </div>

    <div class="mb-3">
      <ProductTabs @tab-change="onTabChange" />
    </div>

    <div v-if="loading" class="text-center py-5">載入中...</div>

    <div v-else>
      <div v-if="items.length === 0" class="alert alert-light text-center">
        沒有找到商品（可能後端尚無資料或篩選條件）
      </div>

      <div class="row g-3">
        <div
          v-for="item in items"
          :key="item.listingId ?? item.raw?.id ?? item.raw?.listingId"
          class="col-12 col-sm-6 col-md-4 col-lg-3"
        >
          <ListingCard :listing="item" />
        </div>
      </div>

      <nav v-if="totalPages > 1" class="mt-4">
        <ul class="pagination justify-content-center">
          <li :class="['page-item', { disabled: page <= 1 }]" @click="goToPage(page - 1)">
            <a class="page-link">‹</a>
          </li>
          <li
            v-for="p in pageButtons"
            :key="p"
            :class="['page-item', { active: p === page }]"
            @click="goToPage(p)"
          >
            <a class="page-link">{{ p }}</a>
          </li>
          <li :class="['page-item', { disabled: page >= totalPages }]" @click="goToPage(page + 1)">
            <a class="page-link">›</a>
          </li>
        </ul>
      </nav>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ListingCard from '@/components/ListingCard.vue'
import ProductTabs from '@/components/ProductTabs.vue'
import ListingsApi from '@/api/Listings'

export default defineComponent({
  name: 'ListingsView',
  components: { ListingCard, ProductTabs },
  setup() {
    const route = useRoute()
    const router = useRouter()

    const q = ref<string>((route.query.q as string) || '')
    const page = ref<number>(parseInt((route.query.page as string) || '1'))
    const pageSize = ref<number>(12)

    const items = ref<any[]>([])
    const total = ref<number>(0)
    const loading = ref<boolean>(false)

    const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

    const pageButtons = computed(() => {
      const p = page.value
      const start = Math.max(1, p - 2)
      const end = Math.min(totalPages.value, p + 2)
      const arr: number[] = []
      for (let i = start; i <= end; i++) arr.push(i)
      return arr
    })

    let debounceTimer: any = null

    async function fetchList() {
      loading.value = true
      try {
        const params: any = { page: page.value, pageSize: pageSize.value }
        if (route.query.q) params.q = String(route.query.q)
        if (route.query.categoryId) params.categoryId = route.query.categoryId
        console.log('[Listings] fetchList params:', params)
        const resp = await ListingsApi.list(params)
        console.log('[Listings] api resp:', resp)
        items.value = resp.items || []
        total.value = resp.total || 0
      } catch (err) {
        console.error('[Listings] fetchList error:', err)
        items.value = []
        total.value = 0
      } finally {
        loading.value = false
      }
    }

    function goToPage(p: number) {
      if (p < 1 || p > totalPages.value) return
      page.value = p
      const query: any = {}
      if (route.query.q) query.q = String(route.query.q)
      if (route.query.categoryId) query.categoryId = String(route.query.categoryId)
      query.page = String(p)
      router.replace({ path: route.path, query })
      fetchList()
    }

    // 當 route.query { q, page, categoryId } 變動時重新抓資料
    watch(
      () => [route.query.q, route.query.page, route.query.categoryId],
      () => {
        q.value = String(route.query.q || '')
        page.value = parseInt(String(route.query.page || '1')) || 1
        clearTimeout(debounceTimer)
        debounceTimer = setTimeout(() => fetchList(), 150)
      },
      { immediate: true },
    )

    function onTabChange(tab: 'new' | 'hot' | 'used') {
      if (tab === 'used') {
        router.push({ path: '/listings', query: { page: '1' } })
        return
      }
      // new / hot 可以在這處理不同的資料來源（目前示範用 console）
      console.log('[Listings] tab-change', tab)
      // 例如：向不同 API 抓 data 或 local filter
    }

    return {
      q,
      page,
      pageSize,
      items,
      total,
      loading,
      totalPages,
      pageButtons,
      goToPage,
      onTabChange,
    }
  },
})
</script>

<style scoped>
/* 依需要微調 */
</style>
