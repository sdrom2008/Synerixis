<template>
  <AppShell active="ai">
    <view class="sx-page sx-page-wide">
      <view class="sx-page-header">
        <view>
          <view class="sx-page-title">AI 设置</view>
          <view class="sx-page-sub">语气、自动回复与营业时段</view>
        </view>
      </view>

      <view class="sx-card section">
        <view class="section-label">回复语气</view>
        <view class="segmented">
          <view
            v-for="(item, index) in toneOptions"
            :key="item.value"
            class="segment"
            :class="{ active: toneIndex === index }"
            @tap="selectTone(index)"
          >
            {{ item.label }}
          </view>
        </view>
        <text class="hint">{{ toneOptions[toneIndex].desc }}</text>
      </view>

      <view class="sx-card section">
        <view class="row-between">
          <view>
            <view class="section-label">自动回复</view>
            <text class="hint">关闭后仅记录进线，不自动发送回复</text>
          </view>
          <switch
            :checked="form.autoReplyEnabled"
            color="#2563EB"
            @change="onAutoReply"
          />
        </view>
      </view>

      <view class="sx-card section">
        <view class="section-label">营业时段</view>
        <text class="hint">已持久化到商家配置；营业时段外降级留言模板逻辑仍待 Webhook 接入</text>
        <view class="hours-row">
          <picker mode="time" :value="form.businessStart" @change="onStart">
            <view class="time-box">{{ form.businessStart }}</view>
          </picker>
          <text class="dash">至</text>
          <picker mode="time" :value="form.businessEnd" @change="onEnd">
            <view class="time-box">{{ form.businessEnd }}</view>
          </picker>
        </view>
      </view>

      <view class="sx-card section">
        <view class="section-label">偏好语言</view>
        <view class="segmented">
          <view
            v-for="(item, index) in langOptions"
            :key="item.value"
            class="segment"
            :class="{ active: langIndex === index }"
            @tap="selectLang(index)"
          >
            {{ item.label }}
          </view>
        </view>
      </view>

      <button class="sx-btn sx-btn-primary save" :loading="saving" @tap="save">
        保存设置
      </button>
    </view>
  </AppShell>
</template>

<script>
import AppShell from '@/components/merchant/AppShell.vue';
import { getSellerProfile, updateSellerConfig } from '@/api/merchant.js';

const LOCAL_KEY = 'sx_ai_settings_local';

export default {
  components: { AppShell },
  data() {
    return {
      form: {
        defaultReplyTone: 'professional',
        preferredLanguage: 'zh',
        autoReplyEnabled: true,
        businessStart: '09:00',
        businessEnd: '22:00'
      },
      toneOptions: [
        { label: '专业', value: 'professional', desc: '正式、严谨，适合跨境售后与物流说明' },
        { label: '亲切', value: 'friendly', desc: '温暖友好，提升买家体验' },
        { label: '简洁', value: 'concise', desc: '短句优先，适合高频咨询' }
      ],
      toneIndex: 0,
      langOptions: [
        { label: '中文优先', value: 'zh' },
        { label: '英文优先', value: 'en' },
        { label: '双语', value: 'bilingual' }
      ],
      langIndex: 0,
      saving: false
    };
  },
  onShow() {
    this.load();
  },
  methods: {
    selectTone(i) {
      this.toneIndex = i;
      this.form.defaultReplyTone = this.toneOptions[i].value;
    },
    selectLang(i) {
      this.langIndex = i;
      this.form.preferredLanguage = this.langOptions[i].value;
    },
    onAutoReply(e) {
      this.form.autoReplyEnabled = !!e.detail.value;
    },
    onStart(e) {
      this.form.businessStart = e.detail.value;
    },
    onEnd(e) {
      this.form.businessEnd = e.detail.value;
    },
    async load() {
      const local = uni.getStorageSync(LOCAL_KEY);
      if (local && typeof local === 'object') {
        this.form = { ...this.form, ...local };
      }
      try {
        const profile = await getSellerProfile();
        const cfg = profile?.config || profile?.Config || {};
        if (cfg.defaultReplyTone || cfg.DefaultReplyTone) {
          this.form.defaultReplyTone = cfg.defaultReplyTone || cfg.DefaultReplyTone;
        }
        if (cfg.preferredLanguage || cfg.PreferredLanguage) {
          this.form.preferredLanguage = cfg.preferredLanguage || cfg.PreferredLanguage;
        }
        const auto =
          cfg.enableAutoReply ?? cfg.EnableAutoReply;
        if (typeof auto === 'boolean') {
          this.form.autoReplyEnabled = auto;
        }
        const start = cfg.businessHoursStart || cfg.BusinessHoursStart;
        const end = cfg.businessHoursEnd || cfg.BusinessHoursEnd;
        if (start) this.form.businessStart = start;
        if (end) this.form.businessEnd = end;
      } catch (e) {
        /* keep local fallback */
      }
      this.toneIndex = Math.max(
        0,
        this.toneOptions.findIndex((t) => t.value === this.form.defaultReplyTone)
      );
      this.langIndex = Math.max(
        0,
        this.langOptions.findIndex((t) => t.value === this.form.preferredLanguage)
      );
      if (this.toneIndex < 0) this.toneIndex = 0;
      if (this.langIndex < 0) this.langIndex = 0;
    },
    async save() {
      this.saving = true;
      const payload = {
        defaultReplyTone: this.form.defaultReplyTone,
        preferredLanguage: this.form.preferredLanguage,
        enableAutoReply: this.form.autoReplyEnabled,
        businessHoursStart: this.form.businessStart,
        businessHoursEnd: this.form.businessEnd
      };
      uni.setStorageSync(LOCAL_KEY, {
        autoReplyEnabled: this.form.autoReplyEnabled,
        businessStart: this.form.businessStart,
        businessEnd: this.form.businessEnd,
        defaultReplyTone: this.form.defaultReplyTone,
        preferredLanguage: this.form.preferredLanguage
      });
      try {
        await updateSellerConfig(payload);
        uni.showToast({ title: '已保存', icon: 'success' });
      } catch (e) {
        uni.showToast({ title: '已存本地，云端同步失败', icon: 'none' });
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.section {
  margin-bottom: 24rpx;
}

.section-label {
  font-size: 30rpx;
  font-weight: 650;
  color: #0F172A;
  margin-bottom: 16rpx;
}

.hint {
  display: block;
  font-size: 24rpx;
  color: #64748B;
  line-height: 1.5;
  margin-top: 12rpx;
}

.segmented {
  display: flex;
  background: #F1F5F9;
  border-radius: 12rpx;
  padding: 6rpx;
  gap: 6rpx;
}

.segment {
  flex: 1;
  text-align: center;
  padding: 18rpx 8rpx;
  border-radius: 10rpx;
  font-size: 26rpx;
  color: #64748B;
}

.segment.active {
  background: #fff;
  color: #2563EB;
  font-weight: 650;
  box-shadow: 0 2rpx 8rpx rgba(15, 23, 42, 0.06);
}

.row-between {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24rpx;
}

.hours-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  margin-top: 20rpx;
}

.time-box {
  min-width: 160rpx;
  padding: 16rpx 24rpx;
  background: #F8FAFC;
  border: 1rpx solid #E2E8F0;
  border-radius: 12rpx;
  text-align: center;
  color: #0F172A;
  font-size: 28rpx;
}

.dash {
  color: #94A3B8;
}

.save {
  width: 100%;
  margin-top: 16rpx;
}
</style>
