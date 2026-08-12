# 本地用 Clerk 真实 session token 测试鉴权流程

Cataben 的受保护端点（`submit` / `code/execute` / 成就 / `/me` 等）使用 Clerk JWT 鉴权。

> **关键映射**：Clerk 的 JWT `sub` 是字符串（如 `user_2xxx`），**不是**本系统 DB 里的 Guid `User.Id`；
> 二者通过 `User.ExternalId` 关联。控制器经 `ICurrentUserService` 把 `sub` 解析为内部 `User.Id`。
> 因此：无 token → **401**；鉴权通过但该 Clerk 用户未在 DB 建行 → **404**。

## 前置（基础设施已就绪）

```bash
docker compose up -d postgres redis
dotnet run --project Cataben.API          # Development, http://localhost:5277
```

## 第一步：在 Clerk 创建应用并拿到 Issuer

1. 注册 https://dashboard.clerk.com → **New application**（开发实例免费）。
2. 在 **API Keys** 页复制 **Frontend API URL**，形如 `https://abc123-xx.clerk.accounts.dev`。
   - 这就是 JWT 的 `iss`，填给后端的 `Clerk:Issuer`。
   - Clerk 在该地址发布 `/.well-known/openid-configuration`，后端 `JwtBearer` 据此自动发现 JWKS（无需手动配 key）。
3. 同页复制 **Secret key**（`sk_test_...`）—— 仅「脚本签发测试 token」需要；**后端校验 token 只用公钥，不需要它**。
4. **Users → Add user**，建一个测试用户（邮箱 + 密码），记下其 Clerk user id（形如 `user_2xxx`）。

## 第二步：把 Issuer 写入 user-secrets（不进仓库）

`Cataben.API` 已初始化 `UserSecretsId`（Development 下由默认配置自动加载）：

```bash
dotnet user-secrets set "Clerk:Issuer" "https://abc123-xx.clerk.accounts.dev" --project Cataben.API
```

重启 API 使其生效。

## 第三步：让该 Clerk 用户在本系统 DB 中存在（关键，否则 404）

生产中 Clerk 触发 `user.created` webhook 创建用户；本地收不到 webhook，手动 POST 同一端点：

```bash
curl -X POST http://localhost:5277/api/auth/webhook/clerk \
  -H "Content-Type: application/json" \
  -d '{"type":"user.created","data":{"id":"user_2xxx","username":"alice","emailAddresses":[{"emailAddress":"alice@example.com"}]}}'
```

## 第四步：获取真实 session JWT

> 实测：Clerk dev 实例在 `{Frontend API URL}/.well-known/openid-configuration` 发布标准 OIDC discovery（`issuer` = Frontend API URL），JWKS 在 `/.well-known/jwks.json`。后端 `JwtBearer` 的 `Authority` 自动发现，无需手动配 key。session token 默认 **60s 有效**。

**方式 A（密码直换，推荐）**——用账号密码调 Clerk 前端 sign-in API，一步拿到 session JWT（无需 `sk_test_`、无需先登录）：

```bash
FRONTEND_API="https://<your-clerk>.clerk.accounts.dev"
curl -s -X POST "$FRONTEND_API/v1/client/sign_ins" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "strategy=password" \
  --data-urlencode "identifier=zhangsan@mail.com" \
  --data-urlencode "password=<password>" \
  -o /tmp/signin.json
TOKEN=$(grep -oE '"jwt":"[^"]+"' /tmp/signin.json | head -1 | sed 's/"jwt":"//;s/"$//')
echo "$TOKEN"   # eyJ... (RS256, 60s lifetime)
```

**方式 B（Backend API）**——需要 `sk_test_` 且先登录一次拿 session_id：

```bash
SESSION_ID=sess_2xxx
CLERK_SECRET_KEY=sk_test_xxx
TOKEN=$(curl -s -X POST "https://api.clerk.com/v1/sessions/$SESSION_ID/tokens" \
  -H "Authorization: Bearer $CLERK_SECRET_KEY" | jq -r .jwt)
echo "$TOKEN"
```

**方式 B（浏览器）**——在任意加载了 `@clerk/clerk-js` 的页面登录后，控制台执行：

```js
await window.Clerk.session.getToken()   // 复制返回的 JWT
```

> Clerk session token 默认有效期约 60s，过期就重新签发。

## 第五步：用 token 调受保护端点

```bash
# 运行代码（Roslyn）
curl -s http://localhost:5277/api/code/execute \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"Console.WriteLine(\"hi\");","type":"Algorithm"}'

# /me（应返回第三步创建的用户）
curl -s http://localhost:5277/api/user/me -H "Authorization: Bearer $TOKEN"

# 提交挑战（用种子挑战 id）
CHALLENGE_ID=$(curl -s http://localhost:5277/api/challenge | jq -r '.[0].id')
curl -s -X POST "http://localhost:5277/api/submission/submit/$CHALLENGE_ID" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"..."}'

# 成就统计
curl -s http://localhost:5277/api/achievement/user/stats -H "Authorization: Bearer $TOKEN"
```

## 排错

| 现象 | 原因 / 处理 |
|---|---|
| `401` + 日志 `IDX... Unable to obtain configuration` | `Clerk:Issuer` 不是完整 Frontend API URL（需含 `https://`） |
| `401` issuer validation failed | user-secrets 的 Issuer 与 token 的 `iss` 不一致 |
| `404`（鉴权已通过） | 第三步漏了 —— 该 Clerk 用户未在 DB 建行 |
| token 频繁失效 | Clerk session token 默认 60s，重新签发即可 |
| `401` IDX10205 `ValidIssuer: 'https://your-clerk-domain.com'` | user-secrets 的 Issuer 被 appsettings 占位符覆盖。`Program.cs` 已修复（显式配置链尾部加 `AddUserSecrets<Program>`）；若自定义配置链请确保 user-secrets 在 `AddJsonFile` **之后**加载 |
| 解析 JSON 报错 | 示例用 `jq`；未安装可用其他方式提取字段 |
