<!-- src/components/SidebarCategories.vue -->
<script setup lang="ts">
import { onMounted, ref, watch, computed } from 'vue'
import { getCategories, type Category } from '@/api/catalog'


// 同時接受兩種 prop（舊 API 與 v-model 支援）
const props = defineProps<{
  selectedId?: number | null
  modelValue?: number | null
}>()
// emits: 原本的 select 事件 + v-model update
const emit = defineEmits<{
  (e: 'select', id: number | null): void
  (e: 'update:modelValue', id: number | null): void
}>()

// state
const loading = ref(true)
const items = ref<Category[]>([])
const err = ref('')
const localSelected = ref<number | null>(props.modelValue ?? props.selectedId ?? null)
const effectiveSelected = computed({
  get() {
    // 取 modelValue（v-model）優先，否則取 selectedId
    return props.modelValue ?? props.selectedId ?? localSelected.value
  },
  set(v: number | null) {
    localSelected.value = v
    // 同時 emit 給父元件（兩種方式都 emit）
    emit('select', v)
    emit('update:modelValue', v)
  },
})

watch(
  () => [props.modelValue, props.selectedId],
  ([mv, sid]) => {
    const newVal = mv ?? sid ?? null
    console.log('[Sidebar] props changed -> modelValue:', mv, ' selectedId:', sid, ' => set local->', newVal)
    localSelected.value = newVal
  }
)

onMounted(async () => {
  try {
    loading.value = true
    items.value = await getCategories()
    console.log('[Sidebar] categories loaded (items):', items.value)
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '讀取分類失敗'
    console.error('[Sidebar] load categories err', e)
  } finally {
    loading.value = false
  }
})

// 當 user 點分類時更新 effectiveSelected（會觸發 setter）
function pickItem(item: Category | null) {
  if (item === null) {
    console.log('[Sidebar] pick -> ALL (null)')
    effectiveSelected.value = null
    return
  }
  // 解析 id（容錯）
  const id = (item as any).categoryId ?? (item as any).id ?? (item as any).CategoryID ?? undefined
  console.log('[Sidebar] pick raw item ->', item, ' parsed id ->', id)
  if (id === undefined) {
    console.warn('[Sidebar] parsed id is undefined — 請檢查 API 回傳')
    effectiveSelected.value = null
    return
  }
  effectiveSelected.value = id
}
</script>

<template>
  <aside class="side">
    <h4>全站分類</h4>

    <div v-if="loading" class="muted">載入中…</div>
    <div v-else-if="err" class="err">{{ err }}</div>

    <div v-else class="list-group">
      <button
        type="button"
        class="list-group-item list-group-item-action"
        :class="{ active: effectiveSelected === null }"
        @click.stop="pickItem(null)"
      >
        全部
      </button>

      <button
        v-for="c in items"
        :key="c.categoryId"
        type="button"
        class="list-group-item list-group-item-action"
        :class="{ active: effectiveSelected === (c.categoryId) }"
        @click.stop="pickItem(c)"
      >
        {{ c.name ?? '（無名稱）' }}
      </button>
    </div>
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
.list-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.list-group-item {
  border-radius: 8px;
  padding: 10px 12px;
  text-align: left;
}
/* 微調 hover / active 視覺 */
.list-group-item:hover {
  background: #f5f7fa;
}
.list-group-item.active {
  background: #0d6efd;
  border-color: #0d6efd;
  color: #fff;
}
.muted {
  color: #7a7a7a;
}
.err {
  color: #c00;
}
</style>
