// utils/config.js
export const BASE_URL = 'http://localhost:7092';  // 后端API地址
export const getToken = () => uni.getStorageSync('token') || '';