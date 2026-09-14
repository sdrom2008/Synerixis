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

