<!-- src/components/ListingCard.vue -->
<template>
  <div class="card listing-card h-100">
      <router-link :to="detailUrl" class="card-img-top-link">
      <img :src="coverSrc" class="card-img-top" alt="cover" />
    </router-link>

    <div class="card-body d-flex flex-column">
      <router-link :to="detailUrl" class="text-decoration-none">
        <h6 class="card-title text-dark">{{ listing.title }}</h6>
      </router-link>

      <div class="mt-auto d-flex justify-content-between align-items-center">
        <div class="btn-group">
          <button class="btn btn-sm btn-primary" @click="addToCart">加入購物車</button>
          <button class="btn btn-sm btn-outline-secondary" @click="toggleFavorite">收藏</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import type { Listing } from '@/api/Listings'
import Listings from '@/views/Listings.vue';

const props = defineProps<{ listing: Listing }>()
const placeholder = '../assets/ad.png' // 再改路徑(佔位圖)
const coverSrc = computed(() => (props.listing as any).coverUrl || (props.listing as any).cover?.url || placeholder)
const router = useRouter()

const detailUrl = computed(() => ({
  name: 'ListingDetail',
  params: { id: props.listing.listingId },
})) // 如果沒有 detail route，可改為 `/listings/${id}`

function addToCart() {
  // TODO: 呼叫 cart service 或 emit 事件。暫時示範 toast
  // emit 或使用全域 store (Pinia/Vuex) 更好
  alert(`加入購物車：${props.listing.title}`)
}

function toggleFavorite() {
  alert(`已加入收藏：${props.listing.title}`)
}
</script>

<style scoped>
.listing-card {
  border-radius: 10px;
  overflow: hidden;
}
.card-img-top {
  width: 100%;
  height: 180px;
  object-fit: cover;
  background: #f6f6f6;
}
.card-img-top-link {
  display: block;
}
.card {
  min-height: 270px;
}
</style>
