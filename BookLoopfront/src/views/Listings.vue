<!-- src/components/ListingCard.vue -->
<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useCartStore } from '@/stores/cart' // 引入購物車 store
import type { Listing } from '@/api/Listings'
import type { Book } from '@/api/catalog'

const props = defineProps<{ listing: Listing }>()
const placeholder = '/img/placeholder.png'
const router = useRouter()
const cartStore = useCartStore() // 取得購物車 store

const detailUrl = computed(() => ({
  name: 'ListingDetail',
  params: { id: props.listing.listingId },
}))

function addToCart() {
  // 將 Listing 轉為 Book 型別
  const book: Book = {
    bookId: props.listing.listingId,
    title: props.listing.title,
    salePrice: 0, // 如果 Listing 沒有價格，先給 0
    coverUrl: props.listing.coverUrl || '',
  }

  cartStore.addItem(book, 1)  // 加入購物車
  alert(`已加入購物車：${book.title}`)
}

function toggleFavorite() {
  alert(`已加入收藏：${props.listing.title}`)
}
</script>