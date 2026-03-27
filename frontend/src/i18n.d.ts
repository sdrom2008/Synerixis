declare module '@/utils/i18n.js' {
  /**
   * 切换并保存当前语言设置
   * @param lang 语言代码，如 'zh-CN' 或 'en-US'
   */
  export function setLanguage(lang: string): void;

  /**
   * 获取当前语言
   * @returns 当前语言代码
   */
  export function getLanguage(): string;

  /**
   * 多语言翻译函数
   * @param key 翻译键，如 'common.login'
   * @returns 翻译后的文本
   */
  export function t(key: string): string;
}
