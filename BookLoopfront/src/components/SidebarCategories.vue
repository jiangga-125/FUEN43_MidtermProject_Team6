<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getCategories, type Category } from '@/api/catalog'

/* 接收當前選取分類，點擊後 emit 給父層 */
const props = defineProps<{ selectedId: number | null }>()
const emit = defineEmits<{ (e: 'select', id: number | null): void }>()

const loading = ref(true)
const items = ref<Category[]>([])
const err = ref('')

onMounted(async () => {
  try {
    loading.value = true
    items.value = await getCategories()
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '讀取分類失敗'
  } finally {
    loading.value = false
  }
})

function pick(id: number | null) {
  emit('select', id)
}
</script>

<template>
  <aside class="side">
    <h4>全站分類</h4>

    <div v-if="loading" class="muted">載入中…</div>
    <div v-else-if="err" class="err">{{ err }}</div>
    <ul v-else>
      <li :class="{ active: props.selectedId === null }" @click="pick(null)">全部</li>
      <li
        v-for="c in items"
        :key="c.categoryId"
        :class="{ active: props.selectedId === c.categoryId }"
        @click="pick(c.categoryId)"
      >
        {{ c.name }}
      </li>
    </ul>
  </aside>
</template>

<style scoped>
.side {
  background: #fff;
  border: 1px solid #e9ecef;
  border-radius: 12px;
  padding: 12px;
}
h4 {
  margin: 4px 0 12px;
}
ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 6px;
}
li {
  padding: 8px 10px;
  border-radius: 8px;
  cursor: pointer;
}
li:hover {
  background: #f5f7fa;
}
li.active {
  background: #0d6efd;
  color: #fff;
}
.muted {
  color: #7a7a7a;
}
.err {
  color: #c00;
}
</style>
