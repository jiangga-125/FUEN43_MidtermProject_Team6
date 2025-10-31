<template>
  <div v-if="listing" class="card listing-card h-100">
    <router-link v-if="hasId" :to="detailUrl" class="card-img-top-link">
      <img
        :src="listing.image || listing.coverUrl || placeholder"
        class="card-img-top"
        alt="cover"
      />
    </router-link>
    <div v-else class="card-img-top-link">
      <img
        :src="listing.image || listing.coverUrl || placeholder"
        class="card-img-top"
        alt="cover"
      />
    </div>

    <div class="card-body d-flex flex-column">
      <div>
        <router-link v-if="hasId" :to="detailUrl" class="text-decoration-none">
          <h6 class="card-title text-dark">{{ listing.title }}</h6>
        </router-link>
        <h6 v-else class="card-title text-dark">{{ listing.title }}</h6>
      </div>

      <p class="mb-2 text-muted small" v-if="listing.category">分類：{{ listing.category.name }}</p>

      <div class="mt-auto d-flex justify-content-between align-items-center">
        <div class="text-success fw-semibold">借閱</div>

        <div class="btn-group">
          <button class="btn btn-sm btn-outline-primary" type="button" @click.prevent="borrowNow">
            借閱/申請
          </button>
          <button
            class="btn btn-sm btn-outline-secondary"
            type="button"
            @click.prevent="toggleFavorite"
          >
            收藏
          </button>
        </div>
      </div>
    </div>
  </div>

  <div v-else class="card listing-card placeholder h-100">
    <div class="card-img-top skeleton" />
    <div class="card-body">
      <div class="skeleton-line" style="width: 60%"></div>
      <div class="skeleton-line" style="width: 40%"></div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
const props = defineProps({ listing: Object })
const placeholder = '/img/placeholder.png'

const hasId = computed(() => !!(props.listing && (props.listing.id || props.listing.listingId)))
const detailUrl = computed(() => {
  const id = props.listing?.id ?? props.listing?.listingId
  return id ? { name: 'ListingDetail', params: { id } } : { path: '#' }
})

const router = useRouter()
function borrowNow() {
  const id = props.listing?.id ?? props.listing?.listingId
  if (id) router.push({ name: 'ListingDetail', params: { id }, query: { action: 'borrow' } })
  else alert('此項目可借閱，請至詳情頁申請借閱')
}
function toggleFavorite() {
  alert('已加入收藏')
}
</script>
