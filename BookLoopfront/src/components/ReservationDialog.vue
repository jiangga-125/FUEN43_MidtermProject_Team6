<script setup lang="ts">
import { computed, ref, watch } from 'vue'

export interface MemberOption { id: number; name: string }

const props = defineProps<{
  open: boolean
  bookTitle: string
  members: MemberOption[]
  defaultDate: string   // YYYY-MM-DD
  defaultTime: string   // HH:mm 或 HH:mm:ss
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'confirm', payload: { memberId: number; date: string; time: string }): void
}>()

const memberId = ref<number | null>(null)
const date = ref('')
const time = ref('')
// ---- 日期工具（用本地時間避免時區誤差）----
// ★ ADDED
function toYMD(d: Date){
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${dd}`
}
// ★ ADDED
function addDays(d: Date, n: number){
  const copy = new Date(d.getFullYear(), d.getMonth(), d.getDate())
  copy.setDate(copy.getDate() + n)
  return copy
}
// ★ ADDED
const todayLocal = new Date()
// ★ ADDED
const minDate = toYMD(addDays(todayLocal, 1)) // 明天
// ★ ADDED
const maxDate = toYMD(addDays(todayLocal, 3)) // 大後天（今天+3）
// ★ ADDED
function clampDate(s: string){
  if (!s) return minDate
  if (s < minDate) return minDate
  if (s > maxDate) return maxDate
  return s
}
watch(() => props.open, (v) => {
  if (v) {
    memberId.value = props.members[0]?.id ?? null
    date.value = clampDate(props.defaultDate)
    time.value = props.defaultTime.slice(0,5) // 視 input[type=time] 取 HH:mm
  }
}, { immediate: true })

function normalizeTime(t: string){
  return t.length === 5 ? `${t}:00` : t.slice(0,8)
}

function submit(){
  if (!memberId.value || !date.value || !time.value) return
  emit('confirm', { memberId: memberId.value, date: date.value, time: normalizeTime(time.value) })
}

const visible = computed(() => props.open)
</script>

<template>
  <div v-if="visible" class="modal fade show d-block" style="background:rgba(0,0,0,.5)">
    <div class="modal-dialog">
      <div class="modal-content">
        <div class="modal-header bg-success text-white">
          <h5 class="modal-title">預借確認</h5>
          <button type="button" class="btn-close btn-close-white" @click="$emit('close')"></button>
        </div>

        <div class="modal-body">
          <div class="mb-3">
            <label class="form-label">書名</label>
            <input class="form-control" :value="bookTitle" disabled>
          </div>

           <div class="mb-3">
            <label class="form-label">選擇預約者</label>
            <select class="form-select" v-model.number="memberId">
              <!-- ★ CHANGED：placeholder value 改為 null，避免選到 undefined -->
              <option :value="null" disabled>-- 請選擇 --</option>
              <!-- ★ CHANGED：只顯示人名，不再顯示（ID） -->
              <option v-for="m in members" :key="m.id" :value="m.id">{{ m.name }}</option>
            </select>
          </div>

          <div class="mb-3">
            <label class="form-label">選擇取書日</label>
            <!-- ★ ADDED：限制只能選 明天~大後天 -->
            <input
              type="date"
              class="form-control"
              v-model="date"
              :min="minDate"
              :max="maxDate"
            >
          </div>

          <div class="mb-1">
            <label class="form-label">最晚取書時間</label>
            <input type="time" class="form-control" v-model="time" readonly>
          </div>
        </div>

        <div class="modal-footer">
          <button class="btn btn-primary" @click="submit">借書</button>  
          <button class="btn btn-danger" @click="$emit('close')">取消</button>          
        </div>
      </div>
    </div>
  </div>
</template>
