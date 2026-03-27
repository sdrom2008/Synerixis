import { createSSRApp } from "vue";
import App from "./App.vue";

// 全局设置 BASE_URL（推荐放这里）
import { BASE_URL } from '@/utils/config.js';
// 把 BASE_URL 挂到 Vue 原型上（所有页面都能用 this.$BASE_URL）
const app = createSSRApp(App)
app.config.globalProperties.$BASE_URL = BASE_URL

export function createApp() {
  return {
    app,
  };
}
