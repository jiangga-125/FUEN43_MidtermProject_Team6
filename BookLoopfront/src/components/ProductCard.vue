<script setup lang="ts">
import type { Book } from '@/api/catalog'

const props = defineProps<{ book: Book }>()
const emit = defineEmits<{ (e: 'add', b: Book): void; (e: 'like', b: Book): void }>()

function add() {
  emit('add', props.book)
}
function like() {
  emit('like', props.book)
}
</script>

<template>
  <div class="card">
    <div class="cover">
      <img :src="book.coverUrl || '/placeholder.png'" :alt="book.title" />
    </div>
    <div class="info">
      <h5 class="title" :title="book.title">{{ book.title }}</h5>
      <div class="prices">
        <span v-if="book.salePrice != null" class="sale">NT$ {{ Math.round(book.salePrice) }}</span>
        <span
          v-if="book.listPrice && book.salePrice && book.salePrice < book.listPrice"
          class="list"
          >NT$ {{ Math.round(book.listPrice) }}</span
        >
      </div>
      <div class="actions">
        <button @click="add">加入購物車</button>
        <button class="ghost" @click="like">收藏</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.card {
  border: 1px solid #eee;
  border-radius: 12px;
  overflow: hidden;
  background: #fff;
  display: grid;
  grid-template-rows: 180px 1fr;
}
.cover {
  background: #f6f7f9;
  display: flex;
  align-items: center;
  justify-content: center;
}
.cover img {
  max-width: 100%;
  max-height: 100%;
}
.info {
  padding: 10px;
  display: grid;
  gap: 8px;
}
.title {
  font-size: 14px;
  line-height: 1.4;
  height: 40px;
  overflow: hidden;
}
.prices {
  display: flex;
  align-items: center;
  gap: 8px;
}
.sale {
  color: #e63946;
  font-weight: 700;
}
.list {
  color: #999;
  text-decoration: line-through;
}
.actions {
  display: flex;
  gap: 8px;
}
.actions button {
  flex: 1;
  cursor: pointer;
  border-radius: 8px;
  padding: 8px 10px;
  border: 1px solid #0d6efd;
  background: #0d6efd;
  color: #fff;
}
.actions .ghost {
  background: #fff;
  color: #0d6efd;
}
</style>
