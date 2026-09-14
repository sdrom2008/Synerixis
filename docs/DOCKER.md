# Synerixis — Docker Compose 本地一键

> 与 [`DEV_SETUP_BOX.md`](./DEV_SETUP_BOX.md) 互补：本文件描述 **compose 路径**；Box 无 Docker 权限时，文件齐全即可，改用 dotnet + npm。

## 端口（默认）

| 服务 | 宿主机 | 容器内 | 说明 |
|------|--------|--------|------|
| MySQL / MariaDB | `3306` | `3306` | 库 `synerixis` / 用户见 `.env` |
| API | `5000` | `8080` | `/health`、`/health/ready`、`/api/*` |
| Redis（可选） | `6379` | `6379` | `docker compose --profile redis` |
| merchant-web（可选） | `8080` | `80` | nginx 静态 + 反代 `/api`；` --profile web` |

## 快速启动

```bash
cd /workspace/Synerixis   # 或你的仓库根
cp .env.example .env
# 按需填 Shopee / Llm Key；本地验收可不填

docker compose up -d --build
# 可选 Redis + 商家静态站：
# docker compose --profile redis --profile web up -d --build
```

等待 MySQL healthy 后，API 启动时会 `EnsureCreated` + **SchemaPatcher**（开发环境自动补列/表，无需手跑 EF migrate）。

验证：

```bash
curl -sS http://localhost:5000/health
curl -sS http://localhost:5000/health/ready
./scripts/smoke.sh
# 开发测号：
curl -sS -X POST http://localhost:5000/api/auth/init-agent
BASE_URL=http://localhost:5000 TOKEN=<jwt> ./scripts/smoke.sh
```

商家桌面：

- 未启 `web` profile：本机 `cd merchant-web && npm run dev`（默认 `http://localhost:5174`，代理到 `5000`）
- 已启 `web`：浏览器打开 `http://localhost:8080`

Admin：`cd admin-console && npm run dev`（默认代理 `5000`）。

## 配置键对照

`.env.example` 已覆盖：

- **Database**：`MYSQL_*` → `ConnectionStrings:MySqlConnection` / `Database:ConnectionString`
- **Shopee**：`SHOPEE_*` → `Shopee:*`
- **Ai**：`LLM_*` / `AI_PRICE_*` → `Llm:*` / `Ai:*`
- **Redis**：`REDIS_CONNECTION` + `WEBHOOK_RATE_LIMIT_STORE=Redis`
- **Frontends**：`MERCHANT_WEB_BASE_URL` → `Frontends:MerchantWebBaseUrl`

多站点 Partners 数组仍建议在 `appsettings.Development.json` 或挂载配置中写完整 JSON（compose 环境变量以单组 key 为主）。

## 无 Docker 时

见 [`DEV_SETUP_BOX.md`](./DEV_SETUP_BOX.md) 与 [`PRODUCT_STATUS.md`](./PRODUCT_STATUS.md)「本地验收最短路径」：MariaDB/MySQL + `dotnet run` + `merchant-web` / `admin-console` npm。

## 注意

- 勿把真实 Partner Key / LLM Key 提交进仓库。
- aspnet 镜像内已装 `curl` 供 healthcheck；若本机构建失败可去掉 `api.healthcheck`。
- 本阶段不做支付生产、APNs、假物流轨迹。
