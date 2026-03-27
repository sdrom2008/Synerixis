import zhCN from '@/locales/zh-CN.js';
import enUS from '@/locales/en-US.js';

const locales = {
  'zh-CN': zhCN,
  'en-US': enUS
};

// 默认语言：中文
let currentLang = 'zh-CN';

export function setLanguage(lang) {
  if (locales[lang]) {
    currentLang = lang;
    uni.setStorageSync('language', lang);
  }
}

export function getLanguage() {
  const saved = uni.getStorageSync('language');
  if (saved && locales[saved]) {
    currentLang = saved;
  }
  return currentLang;
}

export function t(key) {
  const keys = key.split('.');
  let val = locales[currentLang];
  for (const k of keys) {
    if (val && val[k]) {
      val = val[k];
    } else {
      return key; // 找不到就返回 key 本身
    }
  }
  return val;
}