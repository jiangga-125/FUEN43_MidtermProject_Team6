// src/stores/test.ts
import { defineStore } from 'pinia'

export const useTestStore = defineStore('test', {
  state: () => ({ count: 0 }),
  actions: {
    inc() {
      this.count++
    },
  },
})
