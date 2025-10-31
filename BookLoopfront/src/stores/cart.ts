import { defineStore } from 'pinia'

export const useCartStore = defineStore('cart', {
  state: () => ({
    items: [] as Array<{ id: number; title: string; price: number; qty: number }>
  }),
  getters: {
    count: (state) => state.items.reduce((s, it) => s + it.qty, 0),
    total: (state) => state.items.reduce((s, it) => s + it.price * it.qty, 0)
  },
  actions: {
    addToCart(payload: { id: number; title: string; price?: number }) {
      const exists = this.items.find(i => i.id === payload.id)
      if (exists) exists.qty += 1
      else this.items.push({ id: payload.id, title: payload.title, price: payload.price ?? 0, qty: 1 })
    },
    removeFromCart(id: number) {
      this.items = this.items.filter(i => i.id !== id)
    },
    clear() {
      this.items = []
    }
  }
})
