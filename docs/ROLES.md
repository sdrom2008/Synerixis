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
| **能** | 全量 merchant-web 菜单；绑店 / AI 设置 / 团队 / 审计 / 计费；收件箱审发草稿（默认全店）；`assign` 会话；读写 `SellerConfig`；**充值/购买订阅**（`PayController` 按 Seller.Id） |
| **不能** | `/api/admin/*`（非 Admin Role）；`/api/support` 需 Staff（`GetCurrentAgent`）；坐席「认领」API（请用 assign） |
| **前端** | `FULL_MENU_ROLES`；路由全开 |

### 4.2 Agent（普通坐席 · seat）

| | |
|--|--|
| **范围** | JWT：`shopId` = 所属店。**merchant 收件箱默认 `assignment=mine`（「我的」）**，可手动切「全部分配」；**`/api/support/tickets` 强制仅 `AssignedAgentId == 自己`** |
| **能** | 收件箱 / 概览 / 上手指南；读本店 sessions（默认己分配）/ drafts / alerts / usage；`claim`；审发草稿（同店 `GetMerchantShopId`）；读快捷回复列表；Support 接管/回复/解决**已分配给自己**的票 |
| **不能** | 绑店 / AI 设置 / 团队 / 审计写读（`CanManageShopOwnerResources`）；`assign` 他人；改 Global 快捷回复；进 admin-console（登录页拒非 Admin）；**充值/购买订阅**（`/api/pay/*` 403） |
| **前端** | 仅 `inbox` / `overview` / `onboarding` |

### 4.3 Supervisor（客服主管 · shop）

| | |
|--|--|
| **范围** | 本店（JWT `shopId`） |
| **能** | 与 Seller 类似的店铺管理：绑店、AI 配置、团队 CRUD、审计、快捷回复写、`assign`、Support 看**全店**票；**充值/购买订阅**（`/api/pay/*` 经 `shopId` / `Agents.ShopId` 解析所属商户） |
| **不能** | 平台 `/api/admin/*`；将成员设为 `Admin`（商家团队 API 禁止）；被强制收件箱 `assignment=mine`（主管默认全店） |
| **前端** | 全菜单（与 Seller 同） |

### 4.4 Admin（平台运营 · PLATFORM only）

| | |
|--|--|
| **范围** | **platform only**：`[Authorize(Roles = "Admin")]` → `/api/admin/*`。**不是**商家店铺角色；跨店支持请走 **进入商户后台**（support token），勿把 Admin 当店长 |
| **能** | admin-console 全页；商家列表；**`POST /api/admin/merchants/{id}/enter`** 签发短时 support JWT（`userType=Seller` + `support=true`/`impersonation=true`，scoped 到目标 `shopId`）→ 打开 merchant-web 全店收件箱；审计仅 `admin.enter_merchant`（Admin 可见，商家 audit **过滤 `admin.*`**） |
| **不能** | 经商家团队 API 创建/升为 Admin；support token **不可**团队/绑店/计费支付；商家侧 **无**「系统账号进入店铺」toast/日志；挂靠 `ShopId` 的裸 Admin JWT **不会**自动跨店 |
| **前端** | admin-console：登录校验 Admin；商家行「进入商户后台」；merchant-web 静默消费 `?supportToken=`，身份标「平台支持」 |

---

## 5. API 授权速查

| 控制器 | Authorize | 范围要点 |
|--------|-----------|----------|
| `AdminController` | `Roles = "Admin"` | 全站；含 `POST merchants/{id}/enter`（support token） |
| `AgentController` | `Roles = "Supervisor,Admin"` | 操作者本店 `currentAgent.ShopId` |
| `MerchantController` | `[Authorize]` | 多数 `GetMerchantShopId`；业主资源 `GetShopOwner*`；assign 禁纯 Agent |
| `SellerController` | `[Authorize]` | profile/config/team 业主或主管；商品等路径偏 Seller |
| `SupportController` | `[Authorize]` | Staff；列表按角色 seat/shop；`messages` 强制同店 ShopId |
| `ReportsController` | `[Authorize]` | 按角色过滤店；**Admin 被 dashboard Forbid** |
| `ChatController` | `[Authorize]` | 注释写 Seller；实现未强制 `IsSeller` |
| `PayController` | `[Authorize]` | **Seller + Supervisor** 可创建/查询；Supervisor 经 shopId/`Agents.ShopId`；Agent / support → 403 |
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
| merchant Agent「只能进收件箱」 | UI ✓；默认 **`assignment=mine`**，可手动切全店；与 Support seat 对齐 |
| ChatController「Seller 身份」 | 仅 `[Authorize]`，未验 `userType` |

---

## 8. P0 / P1 权限问题（记录，不大改）

### P0（已修 · `54a63e9`）

1. **~~任意 Seller 可创建 `AgentRole.Admin`~~ → 已修**：`SellerController.AddTeamMember` / `UpdateTeamMember` 对 `Role=Admin` 一律 `400`（商家侧不可创建/升格平台 Admin）。仅平台种子 / `init-agent` / Dev 可建。  
2. **~~`GetTicketMessages` 跨店 IDOR~~ → 已修**：强制 `session.ShopId == agent.ShopId`，再套 Agent seat 规则。  
3. **~~Admin 语义双轨~~ → 已收口（文档+注释）**：`AgentRole.Admin` = **PLATFORM-only**；JWT 仍 `Agents.Role=Admin` + `[Authorize(Roles="Admin")]`；merchant 不提供该角色；挂靠 `ShopId` 进商家台不自动跨店。

### P1

1. ~~merchant 收件箱 vs Support~~ → **已收口**：Agent 默认 `assignment=mine`；Supervisor/Seller 默认全店；Admin 经 support 进店看全店。  
2. **admin-console 路由守卫不验 Role**：粘贴非 Admin JWT 可进壳子，API 403。  
3. **`ReportsController` 对 Admin Forbid / 与注释不符**；报表入口易混淆。  
4. ~~`PayController` Supervisor NotFound~~ → **已修**：Seller + Supervisor 经 shop 解析；Agent / support 403。  
5. **`SupportController` `debug/init-db` `[AllowAnonymous]`**（生产暴露面）。  
6. **`ChatController` 未强制 Seller**。

> P0 已于代码修复。A（Agent 默认 mine）/ B（Admin enter-merchant support）/ C（Seller+Supervisor 充值）已落地，见下文 §10。

---

## 9. 相关文档

- [`MERCHANT_WEB.md`](./MERCHANT_WEB.md) — 商家工作台登录与路由表  
- [`ADMIN_CONSOLE.md`](./ADMIN_CONSOLE.md) — 平台运营台  
- [`DEMO.md`](./DEMO.md) — 演示账号与 5 分钟路径  
- 代码：`Agent.cs`、`AuthService.cs`、`BaseApiController.cs`、`CurrentUser.cs`、各 `*Controller`、`merchant-web/src/router`、`admin-console/src/router`

---

## 10. A / B / C 产品行为（本轮）

### A) Agent 收件箱默认「我的」

- merchant-web Inbox：`userType=Agent` 时 `assignmentFilter` 默认 `mine`，请求 `GET /api/merchant/sessions?assignment=mine`
- 保留「全部分配 / 未分配 / 我的」手动切换
- Supervisor / Seller / support 默认**不**强制 mine（全店）

### B) Admin = 纯平台运营 + 进入商户后台

- `AgentRole.Admin` = PLATFORM only（非店长）
- admin-console 商家列表：「进入商户后台」→ `POST /api/admin/merchants/{id}/enter`
- 返回短时 support JWT（Seller + `support`/`impersonation`）+ `merchantWebUrl`
- merchant-web：`?supportToken=` 静默入会话；全店 inbox；隐藏团队/计费等写入口；**无**「系统账号进入店铺」提示
- 审计：`admin.enter_merchant` 仅 Admin 审计可见；商家 `GET /api/merchant/audit-logs` 过滤 `admin.*`

### C) Seller + Supervisor 可充值/订阅；Agent 不能

- `/api/pay/create` / `query`：Seller 用自身 Id；Supervisor 用 JWT `shopId` 或 `Agents.ShopId` 解析所属 `Sellers` 行
- Agent → 403；support token → 403
- merchant-web 计费菜单：`canViewBilling` = Seller | Supervisor（非 Agent、非 support）
