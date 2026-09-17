# 通用模型接入层（OpenAI-compatible）

Synerixis 只对接 **OpenAI Chat Completions 兼容** 接口（云端 + 本地），不引入多厂商 SDK。

## 能力

| 能力 | 说明 |
|------|------|
| Admin 配置 / 切换 | `admin-console` → **系统设置 → LLM Provider** |
| 云端 | DashScope 兼容模式、OpenAI 等（`baseUrl` + `apiKey` + `model`） |
| 本地 | Ollama `http://127.0.0.1:11434/v1`、LM Studio `http://127.0.0.1:1234/v1`（Key 可空） |
| 商家统一入口 | 起草 / 意图等走 `LlmRuntime` / `ILlmClient`（Semantic Kernel OpenAI connector） |
| 降级 | 无可用 Key（且非本地 endpoint）或调用失败 → **规则草稿**，不 500 |

## 优先级

1. **商家** `SellerConfig.LlmApiKey`（仅覆盖 Key；BaseUrl/Model 仍用全局）
2. **Admin Active Provider**（`system_settings`：`LlmProvider.*`）
3. **appsettings / 环境变量**（`Llm:BaseUrl`、`Llm:ApiKey`、`Llm:Model` / `LLM_API_KEY` / `LLM_BASE_URL` 等）

停用 Admin Provider（Active=false）即回退到第 3 层。

## Admin API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/admin/llm-provider` | 当前配置（**apiKey 掩码**）+ presets |
| PUT | `/api/admin/llm-provider` | 保存；`active=true` 可同时激活；留空 apiKey 不改；`clearApiKey` 清除 |
| POST | `/api/admin/llm-provider/activate` | 仅切换 Active（需已存 baseUrl+model） |

需 JWT `Role=Admin`。审计：`admin.llm_provider.update`。

## 预设 BaseUrl

| 预设 | BaseUrl | 典型 Model |
|------|---------|------------|
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| DashScope 兼容 | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-plus` |
| Ollama | `http://127.0.0.1:11434/v1` | 本机已 pull 的模型名 |
| LM Studio | `http://127.0.0.1:1234/v1` | LM Studio 加载的模型 id |

## 本地快速切换

1. 启动 Ollama / LM Studio，确认 OpenAI 兼容端口可访问  
2. Admin 登录 → 系统设置 → 点预设 **Ollama** 或 **LM Studio**  
3. 填 Model → **保存并激活**（本地可不填 Key）  
4. 商家侧注入/入站应走真实 Chat；关掉 Ollama 后调用失败 → 现有 catch 降级规则草稿  

云端：选 DashScope/OpenAI → 填 Key → 保存并激活。

## 相关代码

- `LlmRuntime` / `LlmKeyResolver`（Infrastructure/AI）
- `ISystemSettingsService.GetLlmProviderAsync` + `LlmProviderSettingKeys`
- 入站：`InboundAiReplyService`（`UseSellerKey` + `IsConfigured`）

演示清单见 [`DEMO.md`](./DEMO.md)。
