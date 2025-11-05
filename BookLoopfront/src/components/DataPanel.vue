<template>
<div class="panel">
<div v-if="state.loading" class="loading">
<div class="spinner" aria-hidden="true"></div>
<div>載入中…</div>
</div>
<div v-else-if="state.error" class="alert error">載入失敗：{{ state.error }}</div>
<div v-else-if="!state.data || state.data.length === 0" class="alert info">{{ emptyText }}</div>
<div v-else>
<slot />
</div>
</div>
</template>


<script setup>
defineProps({
state: { type: Object, required: true },
emptyText: { type: String, default: '目前沒有資料。' }
})
</script>


<style scoped>
.panel { border: 1px solid #eee; border-radius: .5rem; padding: 1rem; }
.loading { display: grid; place-items: center; gap: .5rem; padding: 2rem 0; }
.spinner { width: 28px; height: 28px; border: 3px solid #ddd; border-top-color: #222; border-radius: 50%; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg) } }
.alert { padding: .75rem 1rem; border-radius: .5rem; }
.alert.info { background: #f1f5f9; }
.alert.error { background: #fde8e8; color: #b91c1c; }
</style>