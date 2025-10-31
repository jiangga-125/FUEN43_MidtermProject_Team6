<!-- src/components/ListingCard.vue -->
<template>
  <div class="card listing-card h-100">
    <router-link :to="detailUrl" class="card-img-top-link">
      <img :src="listing.coverUrl || placeholder" class="card-img-top" alt="cover" />
    </router-link>

    <div class="card-body d-flex flex-column">
      <router-link :to="detailUrl" class="text-decoration-none">
        <h6 class="card-title text-dark">{{ listing.title }}</h6>
      </router-link>

      <p class="mb-2 text-muted small" v-if="listing.category">分類：{{ listing.category.name }}</p>

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

const props = defineProps<{ listing: Listing }>()
const placeholder = '/img/placeholder.png'
const router = useRouter()

const detailUrl = computed(() => ({ name: 'ListingDetail', params: { id: props.listing.listingId } }))

function addToCart() {
  alert(`加入購物車：${props.listing.title}`)
}
function toggleFavorite() {
  alert(`已加入收藏：${props.listing.title}`)
}
</script>

<style scoped>
.card { border-radius: 10px; }
.card-img-top { height:180px; object-fit:cover; background:#f6f6f6; }
</style>
