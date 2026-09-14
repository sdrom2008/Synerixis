# Synerixis — Box 开发环境搭建说明

> **主开发环境仍以用户本机为准**：Windows 11 + Visual Studio 2022 + HBuilderX + 本地/局域网 MySQL。  
> 本文档描述 **Cursor Box（Linux）** 上的辅助环境，用于构建验证、文档撰写与 Admin Console 脚手架开发。

## 已验证状态（Box）

| 组件 | 状态 |
|------|------|
| .NET 9 / `dotnet build -c Release` | 通过（0 Error） |
| MariaDB 11.x（apt） | 可安装；容器内需手动启动 `mysqld`（policy-rc.d 会拦截 service） |
| 库 `synerixis` / 用户 `synerixis` | 本地 only，密码见示例配置（勿用于生产） |
| `frontend`（uni-app） | `npm install` |
| `admin-console` | Vue3 + Element Plus 脚手架 |
| `merchant-web` | Vue3 + Element Plus 商家桌面工作台 |

## 1. 克隆与构建 API

```bash
cd /workspace/Synerixis
dotnet build Synerixis.sln -c Release
```

## 2. MariaDB（本机 localhost）

安装：

```bash
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y mariadb-server mariadb-client
```

Box 上 `systemctl`/`service` 常被 policy 拦截，可手动启动：

```bash
sudo mkdir -p /var/run/mysqld && sudo chown mysql:mysql /var/run/mysqld
sudo mysqld --user=mysql --datadir=/var/lib/mysql \
  --socket=/var/run/mysqld/mysqld.sock \
  --pid-file=/var/run/mysqld/mysqld.pid \
  --bind-address=127.0.0.1 &
sudo mysqladmin --socket=/var/run/mysqld/mysqld.sock ping
```

建库与用户（仅 127.0.0.1 / localhost）：

```sql
CREATE DATABASE IF NOT EXISTS synerixis CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'synerixis'@'localhost' IDENTIFIED BY 'synerixis_dev';
CREATE USER IF NOT EXISTS 'synerixis'@'127.0.0.1' IDENTIFIED BY 'synerixis_dev';
GRANT ALL PRIVILEGES ON synerixis.* TO 'synerixis'@'localhost';
GRANT ALL PRIVILEGES ON synerixis.* TO 'synerixis'@'127.0.0.1';
FLUSH PRIVILEGES;
```

## 3. API 配置

- **提交**：`Synerixis.Api/appsettings.Development.example.json`（无真实生产密钥）
- **本地/Box 使用**：复制为 `appsettings.Development.json`（已在 `.gitignore`，勿提交）

```bash
cp Synerixis.Api/appsettings.Development.example.json Synerixis.Api/appsettings.Development.json
# 按需修改 Jwt:Key、Shopee、Llm 等
```

连接串键名与 `Program.cs` 一致：优先 `ConnectionStrings:MySqlConnection`，其次 `Database:ConnectionString`；均缺失时开发模式降级 SQLite。

运行（示例）：

```bash
cd Synerixis.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

## 4. 商家端 frontend（uni-app）

```bash
cd frontend
npm install
# HBuilderX 打开本目录为主路径；Box 上可用 Vite 做 H5 预览（视项目脚本而定）
```

## 5. 运营后台 admin-console

```bash
cd admin-console
npm install
npm run dev      # http://localhost:3000
npm run build    # 生产构建校验
```

默认代理 `/api` → `http://localhost:5000`（见 `vite.config.ts`）。


## 5b. 商家桌面工作台 merchant-web

独立于 uni-app `frontend/` 的 PC 端商家控制台（收件箱三栏 + draft-first 审发）。

```bash
cd merchant-web
npm install
npm run dev      # http://localhost:5174
npm run build    # 生产构建校验
```

默认代理 `/api` → `http://localhost:5000`（可用 `VITE_API_PROXY_TARGET` / `VITE_API_BASE_URL` 配置）。详见 `merchant-web/README.md`。

## 6. 与 Win11 主环境的关系

| 事项 | Win11 主环境 | Box |
|------|--------------|-----|
| 日常编码 / 调试 | VS2022 | 可选 |
| uni-app 调试 / 发版 | HBuilderX | 辅助 `npm install` / 文档 |
| MySQL | 本机或 `192.168.x` | `127.0.0.1` 本地库 `synerixis` |
| 密钥 / Partner Key | User Secrets / 本地 appsettings | 仅 Development（gitignore） |
| Git | 同一 `origin/main` | 同一仓库 |

**不要**把 `.env.mysql`、真实 Partner Key、支付证书提交进仓库。

## 7. 常见问题

- **mysqld 起不来**：检查 `/var/run/mysqld` 权限与 `bind-address=127.0.0.1`。
- **EF 迁移**：Design-time 读取 `Synerixis.Api` 下 `appsettings.json` / `appsettings.Development.json` 的 `Database:ConnectionString`。
- **无 MySQL 时**：不配连接串则使用 SQLite `dev.db`（仅演示，非 SaaS 目标路径）。

## 8. Shopee 多站点与 Webhook Redis

- **单组 key**：只填 `Shopee:AppKey`/`AppSecret`（或 `PartnerId`/`PartnerKey`）+ `Endpoint`/`ApiBaseUrl`，`Region` 默认 `SG`。
- **多站点**：`Shopee:Partners` 数组，或 `Shopee:TW:Host` / `PartnerId` / `PartnerKey`。绑店时 merchant-web 选站点，或 `GET /api/merchant/bind/shopee?region=TW`。
- **Webhook 限流**：`Webhook:RateLimitStore=Memory`（默认）。多实例设 `Redis` 并填写 `ConnectionStrings:Redis`。未配 Redis 时回退 Memory。

产品完成度见 [`PRODUCT_STATUS.md`](./PRODUCT_STATUS.md)。
