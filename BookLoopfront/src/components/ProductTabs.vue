<!-- src/components/ProductTabs.vue -->
<template>
  <div class="d-flex gap-2">
    <button
      class="btn btn-sm"
      :class="selected === 'new' ? 'btn-primary' : 'btn-outline-secondary'"
      @click="select('new')"
    >
      新書熱推
    </button>
    <button
      class="btn btn-sm"
      :class="selected === 'hot' ? 'btn-primary' : 'btn-outline-secondary'"
      @click="select('hot')"
    >
      熱門排行
    </button>
    <button
      class="btn btn-sm"
      :class="selected === 'used' ? 'btn-primary' : 'btn-outline-secondary'"
      @click="select('used')"
    >
      二手書 / 借閱
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
const emit = defineEmits<{
  (e: 'tab-change', tab: 'new' | 'hot' | 'used'): void
}>()

const selected = ref<'new' | 'hot' | 'used'>('new')

function select(name: 'new' | 'hot' | 'used') {
  selected.value = name
  emit('tab-change', name)
}
</script>

<style scoped>
.btn {
  border-radius: 20px;
  padding: 6px 12px;
}
</style>
