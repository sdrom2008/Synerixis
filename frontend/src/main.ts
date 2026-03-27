import { createSSRApp } from "vue";
import App from "./App.vue";

// 全局设置 BASE_URL（推荐放这里）
const BASE_URL = 'http://192.168.1.254:7092'  // ← 改成你的后端实际地址，例如 http://192.168.1.100:7092 或线上域名
// 把 BASE_URL 挂到 Vue 原型上（所有页面都能用 this.$BASE_URL）
const app = createSSRApp(App)
app.config.globalProperties.$BASE_URL = BASE_URL

export function createApp() {
  return {
    app,
  };
}
