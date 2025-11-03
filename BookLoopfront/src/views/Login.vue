<template>
  <form @submit.prevent="go" class="card">
    <h3>登入</h3>
    <input v-model="account" placeholder="Email / 帳號" />
    <input v-model="password" placeholder="密碼" type="password" />
    <button :disabled="loading">登入</button>
    <p v-if="err" class="err">{{ err }}</p>
    <p class="hint">沒有帳號？<a href="/register">去註冊</a></p>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { login } from '@/api/auth'

const account = ref('')
const password = ref('')
const loading = ref(false)
const err = ref('')

async function go() {
  loading.value = true
  err.value = ''
  try {
    await login(account.value, password.value)
    const redirect = new URLSearchParams(location.search).get('redirect') ?? '/member'
    location.href = redirect
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '登入失敗'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.card {
  max-width: 360px;
  margin: 40px auto;
  display: grid;
  gap: 8px;
}
input {
  padding: 10px;
  border: 1px solid #ddd;
  border-radius: 8px;
}
button {
  padding: 10px;
  border: 0;
  background: #0d6efd;
  color: #fff;
  border-radius: 8px;
  cursor: pointer;
}
.err {
  color: #c00;
}
.hint {
  font-size: 14px;
  color: #666;
}
</style>
