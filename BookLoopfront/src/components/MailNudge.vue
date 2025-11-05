<template>
  <transition name="fade">
    <div
      v-if="show && mail"
      class="mail-nudge"
      role="dialog"
      aria-live="polite"
      @mouseenter="expanded = true"
      @mouseleave="expanded = false"
    >
      <!-- 信封小圖示（預設顯示＋抖動） -->
      <button
        class="nudge-fab"
        aria-label="未讀信件"
        type="button"
        @click="expanded = !expanded"
      >
        <span class="nudge-envelope" aria-hidden="true">✉️</span>
      </button>

      <!-- 展開的內容面板（hover 或 expanded=true 時顯示） -->
      <div class="nudge-panel" :class="{ 'is-open': expanded }">
        <div class="nudge-head">
          <span class="nudge-title">您有一封未讀的信件</span>
          <button class="nudge-close" aria-label="關閉" @click="closeX">✕</button>
        </div>

        <div class="nudge-body">
          <div class="nudge-subject" :title="mail.subject">【主旨】{{ mail.subject }}</div>
          <div class="nudge-preview">{{ mail.preview }}</div>
        </div>

        <div class="nudge-actions">
          <button class="btn btn-outline-secondary btn-sm" @click="snooze">稍後提醒</button>
          <button class="btn btn-primary btn-sm" @click="viewNow">查看</button>
        </div>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useMailNudge } from "@/composables/useMailNudge";
import { markSeen, snoozed, setSnooze } from "@/utils/nudgeStore";

const { item, fetchOnce } = useMailNudge();

const show = ref(false);
const mail = ref<any>(null);

// ⬅️ 移到外層：控制展開/收合（預設收合）
const expanded = ref(false);

function refresh() {
  if (snoozed()) { show.value = false; return; }
  fetchOnce().then(() => {
    mail.value = item.value;
    show.value = !!item.value;
    expanded.value = false; // 初次顯示維持收合
  });
}

// 小✕：暫停 8 小時並關閉
function closeX() {
  setSnooze(8 * 60 * 60 * 1000);
  expanded.value = false;
  show.value = false;
}

function viewNow() {
  console.log('[nudge] viewUrl =', mail.value?.viewUrl);
  console.log('[nudge] item =', item.value);
  if (!mail.value?.viewUrl) return;
  markSeen(mail.value.rid);
  const w = window.open(mail.value.viewUrl, "_blank", "noopener,noreferrer");
  if (!w) location.href = mail.value.viewUrl;
  show.value = false;
  expanded.value = false;
}

function snooze() {
  setSnooze(1 * 60 * 60 * 1000); // 暫停提醒 1小時
  show.value = false;
  expanded.value = false;
}

onMounted(refresh);
</script>

<style scoped>
/* 右上角定位（含瀏海安全區與可調整 Header 高度） */
.mail-nudge{
  position: fixed;
  right: 16px;
  top: calc(var(--header-h, 0px) + 150px + env(safe-area-inset-top));
  z-index: 2147483647;
  display: flex;
  flex-direction: column;     /* 垂直展開（面板往下） */
  align-items: flex-end;      /* 右側對齊 */
  gap: 10px;
}

/* 小浮動按鈕（信封圖示） */
.nudge-fab{
  appearance: none;
  border: none;
  background: #0d6efd;
  color: #fff;
  width: 50px;
  height: 50px;
  border-radius: 999px;
  box-shadow: 0 10px 20px rgba(13,110,253,.25);
  cursor: pointer;
  display: grid;
  place-items: center;
}
.nudge-envelope{
  font-size: 20px;
  animation: wobble 1.2s ease-in-out infinite;
}

/* 展開面板（預設隱藏，hover/expanded 才顯示） */
.nudge-panel{
  width: 320px;
  max-width: min(92vw, 360px);
  background: #fff;
  border: 1px solid rgba(0,0,0,.08);
  border-radius: 12px;
  box-shadow: 0 10px 20px rgba(0,0,0,.08), 0 2px 6px rgba(0,0,0,.06);
  overflow: hidden;
  opacity: 0;
  transform: translateY(-6px);
  pointer-events: none;
  transition: opacity .18s ease, transform .18s ease;
}

/* 滑過容器（或 expanded=true）即展開 */
.mail-nudge:hover .nudge-panel,
.nudge-panel.is-open{
  opacity: 1;
  transform: translateY(0);
  pointer-events: auto;
}

/* 面板內容樣式 */
.nudge-head{
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  background: #f8f9fa;
  border-bottom: 1px solid rgba(0,0,0,.06);
}
.nudge-title{ font-weight: 600; font-size: 14px; color: #333; }

.nudge-close{
  appearance: none; border: none; background: transparent;
  font-size: 14px; line-height: 1; color: #6c757d; padding: 4px 6px; cursor: pointer;
}
.nudge-close:hover{ color:#000; }

.nudge-body{ padding: 12px; }

/* 主旨／預覽（保留一次定義，避免重複選擇器衝突） */
.nudge-subject{
  font-weight: 600; font-size: 15px; color:#222;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.nudge-preview{
  margin-top: 6px; font-size: 13px; color:#666;
  display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
}

.nudge-actions{
  display: flex; justify-content: flex-end; gap: .5rem; padding: 10px 12px 12px;
}

/* 進出淡入 */
.fade-enter-active, .fade-leave-active { transition: opacity .18s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* 抖動動畫 */
@keyframes wobble {
  0%   { transform: rotate(0deg)   }
  15%  { transform: rotate(-10deg) }
  30%  { transform: rotate(12deg)  }
  45%  { transform: rotate(-8deg)  }
  60%  { transform: rotate(6deg)   }
  75%  { transform: rotate(-4deg)  }
  100% { transform: rotate(0deg)   }
}

/* 手機微調 */
@media (max-width: 576px){
  .mail-nudge{ right: 12px; top: calc(var(--header-h, 0px) + 8px + env(safe-area-inset-top)); }
  .nudge-panel{ max-width: 92vw; }
}
</style>
