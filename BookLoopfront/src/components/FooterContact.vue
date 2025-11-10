<template>
 
  <div class="footer-contact">
    <div class="container">
    <h5 class="title">聯絡我們</h5>
    
      <ul class="info-list">
        <li>
          <span class="icon">📞</span>
          <a href="tel:034533013">03-453-3013</a>
        </li>

        <li>
          <span class="icon">📧</span>
          <a href="mailto:fuen43.team6@gmail.com">fuen43.team6@gmail.com</a>
        </li>

        <li>
          <span class="icon">📍</span>
          <span>桃園市中壢區新生路二段421號（聖德基督學院）</span>
        </li>

        <li>
          <span class="icon">🕘</span>
          <span>週一至週五 09:00–18:00</span>
        </li>

        <router-link class="more-btn" to="/contact">更多聯絡方式 →</router-link>
      </ul>

      
    </div>
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
.footer-contact {
  background-color: #81C0C0;
  color: white;
  text-align: center;
  padding: 24px 12px;
  margin-top: 40px;
}

.container {
  max-width: 900px;
  margin: 0 auto;
  text-align: left;
}

/* 標題 */
.title {
  margin: 0 0 12px 0;
  font-weight: 700;
  font-size: 16px;
  text-align: center;
}


.info-list {
   list-style: none;
  padding: 0;
  margin: 0 auto 12px;
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 10px 100px; 
  max-width: 800px; 
}

/* 每個項目 */
.info-list li {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  justify-content: flex-start; /* 靠左對齊 */
}

/* icon 固定寬度 */
.icon {
  width: 18px;
  text-align: center;
}

/* 按鈕 */
.more-btn {
  display: inline-block;
  margin-top: 10px;
  padding: 6px 10px;
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
  text-decoration: none;
  border-radius: 6px;
  transition: opacity 0.2s ease;
}

.more-btn:hover {
  opacity: 0.85;
}

/* 🔹 手機版自動變回單欄 */
@media (max-width: 768px) {
  .info-list {
    grid-template-columns: 1fr;
    gap: 8px;
  }
}
</style>
