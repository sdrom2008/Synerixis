# 角色与权限矩阵（以代码为准）

> 来源：`AgentRole` / JWT claims / `[Authorize]` / `BaseApiController` 范围助手 / merchant-web & admin-console 路由守卫。  
> **勿发明角色**。本文描述的是当前实现，不是目标架构。

---

## 1. 真实角色一览

| JWT `userType` / `role` | 实体 | 登录入口 | 数据锚点 |
|-------------------------|------|----------|----------|
| **Seller** | `Sellers` | `POST /api/auth/phone-login`、微信登录 | `UserId` = `Seller.Id`；JWT 恒带 `shopId`（默认等于 `Seller.Id`） |
| **Agent** | `Agents`，`AgentRole.Agent` | `POST /api/auth/agent-login`；手机号若命中 `Agents.Phone` 也可经 phone-login | `Agents.ShopId` → 所属 `Seller.Id` |
| **Supervisor** | `Agents`，`AgentRole.Supervisor` | 同上 | 同店 `ShopId` |
| **Admin** | `Agents`，`AgentRole.Admin` | 同上；admin-console 登录页要求 `role === Admin` | 仍有 `ShopId`；`/api/admin/*` 为**平台级** |

枚举定义（`Synerixis.Domain/Entities/Agent.cs`）：

```csharp
public enum AgentRole
{
    Agent = 1,        // 普通客服（shop seat）
    Supervisor = 2,   // 客服主管（shop）
    Admin = 3         // PLATFORM 运营（JWT → /api/admin/*）；实体仍挂 ShopId；商家团队禁止创建
}
```

> **语义收口**：`AgentRole.Admin` / JWT `role=Admin` **仅表示平台运营**，不是商家「店长」。merchant-web 团队 UI 只提供 Agent|Supervisor；平台 Admin 账号由 seed / `POST /api/auth/init-agent`（Development）/ Dev 路径创建。同一 JWT 进 merchant-web 时仍按挂靠 `ShopId`，**不会**自动跨店。

`CurrentUser`（`Synerixis.Api/Helpers/CurrentUser.cs`）把四者映射为：

- `IsStaff` = Agent | Supervisor | Admin  
- `CanManageTeam` = Seller | Supervisor | Admin  

**没有**独立的 Merchant / Operator / Viewer 等角色字符串。

---

## 2. JWT claims（`AuthService.GenerateJwt`）

| Claim | Seller | Agent / Supervisor / Admin |
|-------|--------|----------------------------|
| `NameIdentifier` / `userId` / `uid` | `Seller.Id` | `Agent.Id` |
| `userType` + `ClaimTypes.Role` + `role` | `"Seller"` | `"Agent"` / `"Supervisor"` / `"Admin"` |
| `shopId` | 恒有（`shopId ?? userId`） | 有（登录时传入 `agent.ShopId`） |
| `sellerId` | 有 | 无 |

`[Authorize(Roles = "...")]` 依赖 `ClaimTypes.Role`。

---

## 3. 数据范围助手（后端真相源）

| 助手 | 允许 | 返回 | 典型用途 |
|------|------|------|----------|
| `GetMerchantShopId()` | Seller + 任意 Staff | 本店 `Seller.Id` | 收件箱、草稿、告警、用量、认领 |
| `GetShopOwnerSellerId()` | Seller / Supervisor / Admin | 同上 | 绑店、SellerConfig、审计、快捷回复写 |
| `GetTeamManagedShopId()` | Seller / Supervisor / Admin | 同上 | `/api/seller/team*` |
| `GetCurrentSellerId()` | **仅 Seller** | `UserId` | 旧路径；收件箱勿用 |
| `GetCurrentAgent(db)` | Staff | `Agents` 行 | `/api/support/*`、`/api/agents/*` |

约定：**`ChatSession.ShopId` == `Seller.Id` == `Agents.ShopId`**（店铺主键 = 商家 Id）。

会话坐席字段：`AssignedAgentId` / `AssignedAt`；转人工闸：`PendingHumanHandoff`（与 `Status=Pending` 解耦）。

---

## 4. 按角色：能 / 不能 / 范围

### 4.1 Seller（商家 · 店铺业主）

| | |
|--|--|
| **范围** | **shop**（自己的 `Seller.Id`） |
| **能** | 全量 merchant-web 菜单；绑店 / AI 设置 / 团队 / 审计 / 计费；收件箱审发草稿；`assign` 会话；读写 `SellerConfig`；创建支付订单（`PayController` 按 `NameIdentifier` 查 `Sellers`） |
| **不能** | `/api/admin/*`（非 Admin Role）；`/api/support` 需 Staff（`GetCurrentAgent`）；坐席「认领」API（请用 assign） |
| **前端** | `FULL_MENU_ROLES`；路由全开 |

### 4.2 Agent（普通坐席 · seat）

| | |
|--|--|
| **范围** | JWT：`shopId` = 所属店。**merchant 收件箱默认全店会话**（可选 `assignment=mine`）；**`/api/support/tickets` 强制仅 `AssignedAgentId == 自己`** |
| **能** | 收件箱 / 概览 / 上手指南；读本店 sessions / drafts / alerts / usage；`claim`；审发草稿（同店 `GetMerchantShopId`）；读快捷回复列表；Support 接管/回复/解决**已分配给自己**的票 |
| **不能** | 绑店 / AI 设置 / 团队 / 审计写读（`CanManageShopOwnerResources`）；`assign` 他人；改 Global 快捷回复；进 admin-console（登录页拒非 Admin） |
| **前端** | 仅 `inbox` / `overview` / `onboarding` |

### 4.3 Supervisor（客服主管 · shop）

| | |
|--|--|
| **范围** | 本店（JWT `shopId`） |
| **能** | 与 Seller 类似的店铺管理：绑店、AI 配置、团队 CRUD、审计、快捷回复写、`assign`、Support 看**全店**票 |
| **不能** | 平台 `/api/admin/*`；将成员设为 `Admin`（仅 Seller 或已有 Admin 可，见团队 API）；Pay 创建（按 Seller 表查 Id，Supervisor JWT 会 NotFound） |
| **前端** | 全菜单（与 Seller 同） |

### 4.4 Admin（平台运营 · platform + 挂靠 shop）

| | |
|--|--|
| **范围** | **platform**：`[Authorize(Roles = "Admin")]` → `/api/admin/*`（商家/连接/会话监控/全站用量/审计/运营设置）。**shop**：同一 JWT 进 merchant-web 时仍按挂靠 `ShopId` 走 `GetMerchantShopId` |
| **能** | admin-console 全页；同店商家工作台全菜单；**不可**经商家团队 API 创建/升为 Admin（仅 seed / `init-agent` / Dev 路径） |
| **不能** | 用 Admin JWT 调 `/api/reports/dashboard`（角色分支只认 Seller/Agent/Supervisor → **Forbid**）；注释写「跨店铺」但 **merchant API 不会自动跨店** |
| **前端** | admin-console：登录校验 Admin；路由守卫**只查 token 是否存在** |

---

## 5. API 授权速查

| 控制器 | Authorize | 范围要点 |
|--------|-----------|----------|
| `AdminController` | `Roles = "Admin"` | 全站 |
| `AgentController` | `Roles = "Supervisor,Admin"` | 操作者本店 `currentAgent.ShopId` |
| `MerchantController` | `[Authorize]` | 多数 `GetMerchantShopId`；业主资源 `GetShopOwner*`；assign 禁纯 Agent |
| `SellerController` | `[Authorize]` | profile/config/team 业主或主管；商品等路径偏 Seller |
| `SupportController` | `[Authorize]` | Staff；列表按角色 seat/shop；`messages` 强制同店 ShopId |
| `ReportsController` | `[Authorize]` | 按角色过滤店；**Admin 被 dashboard Forbid** |
| `ChatController` | `[Authorize]` | 注释写 Seller；实现未强制 `IsSeller` |
| `PayController` | `[Authorize]` | 按 JWT Id 查 `Sellers`（坐席无效） |
| `AuthController` | 登录匿名；`init-agent` 仅 Development | — |

---

## 6. 前端路由守卫

**merchant-web**（`router/index.ts` + `stores/auth.ts`）：

| 路由 | Seller | Supervisor | Admin | Agent |
|------|:------:|:----------:|:-----:|:-----:|
| `/inbox` `/overview` `/onboarding` | ✓ | ✓ | ✓ | ✓ |
| `/shops` `/ai-settings` `/billing` `/quick-replies` `/team` `/audit` | ✓ | ✓ | ✓ | ✗ → 收件箱 |

**admin-console**：非 public 路由只要求 `sx_admin_token`；角色校验在 **Login.vue**（`role === Admin`），不在 `beforeEach`。

---

## 7. 文档 vs 代码 Gaps

| 说法 / 位置 | 代码实际 |
|-------------|----------|
| `AgentRole.Admin` 注释「跨店铺」 | 实体必有 `ShopId`；跨店能力主要靠 `/api/admin/*`，不是 merchant 多店切换 |
| MERCHANT_WEB「计费 API 仍可仅 Seller」 | `GET /api/merchant/usage*` 用 `GetMerchantShopId` → **Supervisor/Admin/Agent 均可读本店用量** |
| README「Seller/Supervisor/Admin 全菜单」 | 与前端一致；**Pay 支付仍仅 Seller Id** |
| Support 注释「Supervisor 可看全店」 | 列表按店过滤 ✓；`GET tickets/{id}/messages` **已修**：强制 `session.ShopId == agent.ShopId` + Agent seat 规则 |
| Reports「Admin 可看所有店铺」 | `agent-performance` 注释如此；`dashboard` 对 Admin **Forbid** |
| 团队 UI 文案常写 Agent\|Supervisor | **已修**：商家 `POST/PUT /api/seller/team*` 拒绝 `Role=Admin`；仅平台种子/`init-agent` 可建 |
| merchant Agent「只能进收件箱」 | UI ✓；默认 **全店会话列表**（非 seat），与 `/api/support` seat 模型不一致 |
| ChatController「Seller 身份」 | 仅 `[Authorize]`，未验 `userType` |

---

## 8. P0 / P1 权限问题（记录，不大改）

### P0（已修）

1. **~~任意 Seller 可创建 `AgentRole.Admin`~~ → 已修**：`SellerController.AddTeamMember` / `UpdateTeamMember` 对 `Role=Admin` 一律 `400`（商家侧不可创建/升格平台 Admin）。仅平台种子 / `init-agent` / Dev 可建。  
2. **~~`GetTicketMessages` 跨店 IDOR~~ → 已修**：强制 `session.ShopId == agent.ShopId`，再套 Agent seat 规则。  
3. **~~Admin 语义双轨~~ → 已收口（文档+注释）**：`AgentRole.Admin` = **PLATFORM-only**；JWT 仍 `Agents.Role=Admin` + `[Authorize(Roles="Admin")]`；merchant 不提供该角色；挂靠 `ShopId` 进商家台不自动跨店。

### P1

1. **merchant 收件箱 vs Support 坐席范围不一致**：Agent 在 merchant 默认可看/审全店；Support 仅己分配。产品与隐私预期需统一（强制 `assignment=mine` 或文档标明「Agent 可全店人审」）。  
2. **admin-console 路由守卫不验 Role**：粘贴非 Admin JWT 可进壳子，API 403。  
3. **`ReportsController` 对 Admin Forbid / 与注释不符**；报表入口易混淆。  
4. **`PayController` 坐席 JWT 创建支付失败**（按 Seller 表查），但 billing 页对 Supervisor 开放。  
5. **`SupportController` `debug/init-db` `[AllowAnonymous]`**（生产暴露面）。  
6. **`ChatController` 未强制 Seller**。

> P0 已于代码修复（Seller 团队拒 Admin、Support 消息同店校验、Admin 平台语义注释/ROLES 收口）。P1 仍为记录项。

---

## 9. 相关文档

- [`MERCHANT_WEB.md`](./MERCHANT_WEB.md) — 商家工作台登录与路由表  
- [`ADMIN_CONSOLE.md`](./ADMIN_CONSOLE.md) — 平台运营台  
- [`DEMO.md`](./DEMO.md) — 演示账号与 5 分钟路径  
- 代码：`Agent.cs`、`AuthService.cs`、`BaseApiController.cs`、`CurrentUser.cs`、各 `*Controller`、`merchant-web/src/router`、`admin-console/src/router`
