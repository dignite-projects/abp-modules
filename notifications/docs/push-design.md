# 设备推送（Push）渠道设计（草案 · 待评审）

> 为 `Dignite.Abp.Notifications` 增加 iOS / Android 设备推送渠道。第一个真实消费者是
> `campus/react-native`（Expo SDK 57 + EAS）。本稿只是设计，不含代码改动；第 14 节列出待决问题。

## 0. 结论先行

- **推送是这套框架最典型的渠道之一，不是硬塞进来的。** 发送侧和邮件通知器的结构一一对应。**Core、
  `NotificationDeliveryRequestedEto`、分发器一行都不改。**
- 真正新增的设计只有一处：**设备 token 的登记与生命周期**。它放进 NotificationCenter（新聚合
  `PushDevice` 加注册接口），通过一个桥接包接到通知器上，和 `Emailing.Identity` 是同一个模式。
- 渠道只有一个，名叫 `"Push"`。Expo、FCM、APNs 是渠道下**可替换的投递提供方**，按设备记录上的提供方类型分发。
  写通知定义的人只需要写 `UseChannels("Push")`。
- 第一期只实现 **Expo Push 提供方**（campus 用的就是它）。FCM / APNs 直连等有真实需求时再加，
  不改动其他部分。
- 可选的会话集成：装上 `NotificationCenter.Push.Identity`，推送就跟随 ABP 登录会话的生命周期，
  登出、被吊销或会话过期后不再推送。

## 1. 目标与非目标

**目标**
1. 已有的通知定义加上 `UseChannels("Push")` 后，推送就能到达用户的手机（iOS / Android）。
2. 设备 token 有完整的生命周期：注册、换绑、登出解绑、失效清理。
3. 每台设备按用户在 App 里选的语言生成推送文案。
4. 点开推送后，App 能定位到收件箱里对应的那一条（`notificationId`），并标记已读。
5. 两种运行模式都可用：只装 Core 的无状态转发（由应用自己实现设备存储），以及完整的 Notification Center 模式。

**非目标（见第 13 节）**：送达保证、重试、送达回执追踪、Web Push、国内厂商通道、FCM topic、
按设备退订（#36）、多个 App 共用一个后端。

## 2. 与模块不变量的对照

| 不变量（`notifications-invariants`） | 本设计如何遵守 |
|---|---|
| §1 ETO 是扁平 POCO，鉴别符稳定 | ETO **不改**。推送的 data 字段只放 id 和名称，**不转发 `DataJson`**（受体积上限约束，也出于隐私考虑） |
| §2 DI 生命周期 | 通知器、设备存储、Manager 一律 `ITransientDependency`。设备存储背后是仓储，不能做成单例 |
| §3 通知器是插件，只依赖 `Abstractions` | `Notifications.Push` 只引用 `Abstractions`。提供方包引用 `Push`，NotificationCenter 集成放在单独的桥接包里，和 `Emailing.Identity` 一致 |
| §4 best-effort、单收件人、可取消 | 不重试，不保存投递状态，**不追踪 Expo 回执**（见 §5.3）。`CancellationToken` 一路透传给 HTTP 调用 |
| §5 两种模式都能工作 | 只装 Core 时由应用自己实现 `IPushDeviceStore`。没有实现时默认用 Null 存储，记一条警告后跳过 |
| §7 投递前重新检查权限和功能开关 | 不受影响。资格检查在 ETO 发出之前就完成了 |
| §8 日志里不出现收件人 | 日志里既不写 UserId，也不写完整 token |
| 北极星：best-effort 的站内通知 | 不引入回执轮询、投递状态表或清理 worker。失效 token 的清理全部"在用到时顺手做"，见 §6.2 |

## 3. 总体结构

```
core/src/
  Dignite.Abp.Notifications.Abstractions        (现有，不改)
  Dignite.Abp.Notifications.Push                 新：通知器 + 契约 + 内容链      → Abstractions
  Dignite.Abp.Notifications.Push.Expo            新：Expo 提供方（纯 HttpClient）  → Push

notification-center/src/
  Domain.Shared / Domain / EF / MongoDB /
  Application(.Contracts) / HttpApi(.Client)     改：新增 PushDevice 聚合 + 注册 API
  Dignite.NotificationCenter.Push                新：桥接，用 PushDevice 实现 IPushDeviceStore
                                                     → NotificationCenter.Domain + Notifications.Push
  Dignite.NotificationCenter.Push.Identity       新（可选）：按 ABP 会话判断设备是否有效
                                                     → NotificationCenter.Push + Volo.Abp.Identity.Domain
```

依赖方向都是单向的：NotificationCenter 的分层包**不引用** Push，桥接包同时引用两边。
不装桥接包时，NotificationCenter 仍然可以登记设备，只是没有通知器去用这些数据。
装了 Push 但不装 NotificationCenter 时，应用需要自己提供设备存储。

为什么设备登记放进 NotificationCenter，而不是做成一套独立的分层包：它需要实体、EF/Mongo 映射、
应用服务、控制器、客户端代理，正好就是 NotificationCenter 已有的那几层。单独再建一套分层包，
包的数量会翻倍，却换不来什么。代价是所有 Center 用户都多一张 `PushDevices` 表（见 §12）。

## 4. 通知器：`Dignite.Abp.Notifications.Push`

### 4.1 投递流程

```
PushNotifier.DeliverAsync(request)
 1. 渠道校验（request.Channel == "Push"，和 EmailNotifier 一致）
 2. payload = NotificationPayload.FromRequest(request, serializer)
 3. targets = IPushDeviceStore.GetTargetsAsync(userId)          → 为空则 Debug 日志后返回
 4. 按 CultureName 分组；每种语言切换一次文化，调用内容链生成一次文案   → 结果为 null 的语言跳过
 5. 生成 data（§4.3），组装 PushMessage
 6. 按 Provider 分组，交给对应的 IPushProvider.SendAsync
      找不到对应提供方 → 记警告（只写提供方名）后跳过该组
      某个提供方抛异常 → 记录下来，继续发下一组；全部发完后再抛出，交给 Core 的 handler 记录
 7. 结果为 TokenInvalid 的设备 → IPushDeviceStore.RemoveAsync(provider, token)
```

文化切换的写法和 `EmailNotifier` 完全一样：设置文化后必须在 `finally` 里恢复，非法的文化名回退到默认值。
**建议**把 `EmailNotifier.ResolveCulture` 及其外层的切换代码提取成 `Abstractions` 里的一个小工具类，
Email 和 Push 共用，避免复制一份（这是一次小重构，Email 的行为不变）。

### 4.2 契约

```csharp
// 设备来源：只装 Core 时由应用实现；Center 模式下由桥接包实现
public interface IPushDeviceStore
{
    Task<IReadOnlyList<PushTarget>> GetTargetsAsync(Guid userId, CancellationToken ct = default);
    Task RemoveAsync(string provider, string token, CancellationToken ct = default);
}
public sealed record PushTarget(string Provider, string Token, string? CultureName);
// 默认注册 NullPushDeviceStore：返回空列表，并像 EmailNotifier 那样记一次"没有可用存储"的警告

// 投递提供方：Expo / 以后的 Fcm、Apns
public interface IPushProvider
{
    string Name { get; }   // "Expo"，和设备记录里的 Provider 按 OrdinalIgnoreCase 匹配
    Task<IReadOnlyList<PushSendResult>> SendAsync(
        IReadOnlyList<PushMessage> messages, CancellationToken ct = default);
}
public sealed record PushMessage(
    string Token, string? Title, string Body, IReadOnlyDictionary<string, string> Data);
public sealed record PushSendResult(string Token, PushSendStatus Status, string? Error = null);
public enum PushSendStatus { Succeeded, TokenInvalid, Failed }
```

内容链**照搬邮件那一套**，命名一一对应：

| Emailing | Push |
|---|---|
| `INotificationEmailContentProvider` | `INotificationPushContentProvider` |
| `NotificationEmailContentProvider<TData>` | `NotificationPushContentProvider<TData>` |
| `INotificationEmailBuilder` / `DefaultNotificationEmailBuilder` | `INotificationPushBuilder` / `DefaultNotificationPushBuilder` |
| `NotificationEmailBuildContext` | `NotificationPushBuildContext`（Notification、UserId、TenantId、CultureName；**不含设备信息**，因为文案按语言生成，不按设备生成） |
| `NotificationEmail` | `NotificationPushContent(string? Title, string Body)` |
| `NotificationEmailProviderOrders` | `NotificationPushProviderOrders` |
| `MessageNotificationEmailContentProvider` / `LocalizableMessage…` | 同名的 Push 版本，作为内置兜底：Title 为 null（系统会显示 App 名称），Body 用消息正文 |
| `NotificationEmailOptions.DefaultCulture` | `NotificationPushOptions.DefaultCulture` |

### 4.3 data 字段

data 是固定的四个键，值都是字符串，省略 null 值：

| 键 | 来源 | App 用途 |
|---|---|---|
| `notificationId` | `request.NotificationId` | 调用 `POST /api/notification-center/notifications/{id}/mark-as-read` |
| `notificationName` | `request.NotificationName` | 决定跳转到哪个页面 |
| `entityTypeName` | `request.EntityTypeName` | 跳转到具体实体 |
| `entityId` | `request.EntityId` | 同上 |

不放 `DataJson`，原因有三：推送载荷上限 4KB；内容会经过 Apple、Google、Expo 的服务器；
App 需要完整数据时可以调收件箱 API 获取。v1 中内容提供方**不能**往 data 里加自定义键，等有需求再开放。

### 4.4 模块

```csharp
[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class AbpNotificationsPushModule : AbpModule { }
```

## 5. Expo 提供方：`Dignite.Abp.Notifications.Push.Expo`

### 5.1 行为

- 请求：`POST https://exp.host/--/api/v2/push/send`，JSON 数组，**每批最多 100 条**，超出时分批发送。
  通过 `IHttpClientFactory` 的命名客户端发送，不引入 Expo 的 SDK（官方也没有 .NET SDK）。
- 鉴权：配置了 `AccessToken` 时带上 `Authorization: Bearer …`。**生产环境必须开启 Expo 的
  "Enhanced Security for Push Notifications" 并配置这个令牌**（理由见 §9）。
- 字段映射：`to` ← Token，`title`，`body`，`data`，`sound`（默认 `"default"`），
  `priority`（默认 `"high"`），`channelId`（Android 通知渠道，可选）。
- 结果映射（响应 `data` 数组和请求顺序一一对应）：
  - `status: "ok"` → `Succeeded`
  - `details.error == "DeviceNotRegistered"` → `TokenInvalid`
  - 其他错误（`MessageTooBig`、`MessageRateExceeded`、`InvalidCredentials` 等）→ `Failed`，并记录错误码
- 整个请求失败时（HTTP 4xx/5xx，或返回请求级 `errors`）：这一批全部记为 `Failed`，记一条警告。**不重试。**

### 5.2 配置

```csharp
public class ExpoPushOptions
{
    public string BaseAddress { get; set; } = "https://exp.host";
    public string? AccessToken { get; set; }         // 生产环境必配
    public string? Sound { get; set; } = "default";
    public string Priority { get; set; } = "high";
    public string? AndroidChannelId { get; set; }
}
```

### 5.3 刻意不做：回执（receipt）轮询

Expo 的结果分两步：发送时拿到回执单（ticket），约 15 分钟后可以用回执单 id 查询 APNs/FCM 的最终结果，
结果只保留约 24 小时。完整实现需要一个延迟执行的后台任务和一张"待查回执"的表。
这正是 subtraction 删掉的那类"投递平台"基础设施，**v1 不做**。

不做的代价：卸载了 App 的设备，在发送阶段通常仍然返回 `ok`，只有查回执时才能看到 `DeviceNotRegistered`，
所以这类失效 token 不会被及时清理。v1 用以下办法兜底：
1. 发送阶段返回的 `DeviceNotRegistered` 照常清理；
2. 每个用户最多登记 N 台设备，超出时淘汰最久没有再上报的那台（§6.2），所以记录数不会无限增长；
3. 装了会话集成包后，会话过期即视为设备失效（§8），campus 适用这一条。

## 6. 设备登记：NotificationCenter

### 6.1 `PushDevice` 聚合

按本模块的约定：继承 `BasicAggregateRoot<Guid>`，显式实现 `IMultiTenant`，setter 为 `protected`。

| 属性 | 类型 | 说明 |
|---|---|---|
| `TenantId` | `Guid?` | |
| `UserId` | `Guid` | |
| `Provider` | `string`(32) | `"Expo"` / 以后的 `"Fcm"`、`"Apns"` |
| `Token` | `string`(1024) | 原始 token，不建索引 |
| `TokenKey` | `string` | `Provider + "\n" + Token` 的 SHA-256，**全局唯一索引**。沿用订阅表 `ScopeKey` 那种与提供方无关的哈希键做法，避开 SQL Server 900 字节的索引长度上限 |
| `CultureName` | `string?`(16) | 注册时取请求的 `CurrentUICulture`。campus 的 App 已经在每个请求上带了 `Accept-Language`，**不需要单独的参数** |
| `SessionId` | `string?`(128) | 注册时取 `CurrentUser.FindSessionId()`（`Volo.Abp.Security` 自带，不依赖 Identity） |
| `CreationTime` | `DateTime` | |
| `LastSeenTime` | `DateTime` | 每次注册都会刷新 |

行为方法：`Rebind(userId, tenantId, cultureName, sessionId, now)`（换绑或刷新，都走这一个方法）。

### 6.2 `PushDeviceManager`（领域服务，`ITransientDependency`）

内部直接使用通用仓储 `IRepository<PushDevice, Guid>`，**不新增自定义仓储接口**，也**不放进 Core 的
`INotificationStore`**（推送设备不是 Core 的概念，`NullNotificationStore` 也不该实现它）。

| 方法 | 规则 |
|---|---|
| `RegisterAsync(userId, provider, token, culture, sessionId)` | 按 `TokenKey` **关闭租户过滤**后查找：已存在就 `Rebind` 到当前用户和租户，这样同一台手机换账号、换租户时 token 会跟着走；不存在就新建。之后检查该用户的设备数，超过 `MaxDevicesPerUser`（默认 10）时删除 `LastSeenTime` 最早的那台 |
| `UnregisterAsync(userId, provider, token)` | 只有设备属于**当前用户**时才删除，否则静默返回，不泄露"这个 token 是否存在" |
| `GetListAsync(userId)` | 只返回当前租户下该用户的设备 |
| `RemoveByTokenAsync(provider, token)` | 供推送失效时清理使用 |

关闭租户过滤是本模块"租户之间不混用"原则的一个**有意为之、范围很小的例外**：token 代表的是物理设备，
不属于任何租户，只有在全局唯一的前提下，"换租户登录后旧租户不再推送"才成立。
如果宿主采用每个租户一个数据库，跨库无法保证唯一，只能依靠登出解绑和失效清理，这一点要写进文档。
campus 没有开启多租户，不受影响。

### 6.3 持久化

- EF Core：`INotificationCenterDbContext` 新增 `DbSet<PushDevice> PushDevices`。
  `ConfigureNotificationCenter()` 中映射表 `{DbTablePrefix}PushDevices`，建两个索引：
  `TokenKey`（唯一）和 `(TenantId, UserId)`。
- MongoDB：新集合，建同样的两个索引。
- 测试放在 `NotificationCenter.TestBase` 的抽象场景里，EF 和 Mongo 各跑一遍。

### 6.4 API

```
POST /api/notification-center/push-devices/register     { "provider": "Expo", "token": "ExponentPushToken[…]" }
POST /api/notification-center/push-devices/unregister   { "provider": "Expo", "token": "ExponentPushToken[…]" }
```

- 应用服务 `IPushDeviceAppService`：类级别只加 `[Authorize]`，始终限定在 `CurrentUser.GetId()` 范围内，
  这是本模块对"管理自己的数据"的一贯做法。
- 显式控制器 `PushDeviceController`，和现有的 `UserNotificationController` 风格一致。
- **token 只放在请求体里，不放进 URL**，避免出现在访问日志里。不用 `DELETE` 带请求体的写法，
  因为不少客户端和代理对它支持不好。动作式路由和现有的 `mark-as-read` 保持一致。
- 输入校验：Provider 必填，最长 32；Token 必填，最长 1024。服务端**不校验**提供方是否已安装，
  因为 Center 不引用 Push，未知的提供方会在发送时记一条警告。
- 生成 `HttpApi.Client` 代理。v1 不提供"列出我的设备"接口，也不改 Angular / MVC UI。

## 7. 桥接包：`Dignite.NotificationCenter.Push`

```csharp
[DependsOn(typeof(NotificationCenterDomainModule), typeof(AbpNotificationsPushModule))]
public class NotificationCenterPushModule : AbpModule { }

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPushDeviceStore))]
public class NotificationCenterPushDeviceStore : IPushDeviceStore, ITransientDependency
{
    // GetTargetsAsync：PushDeviceManager.GetListAsync(userId)
    //   → 对每台设备调用 IsActiveAsync；返回 false 的设备删除并跳过 → 映射为 PushTarget
    // RemoveAsync：PushDeviceManager.RemoveByTokenAsync
    protected virtual Task<bool> IsActiveAsync(PushDevice device, CancellationToken ct)
        => Task.FromResult(true);
}
```

租户：通知器运行时，ABP 事件总线已经切换到了通知所属的租户，这里直接使用当前租户，
不调用 `CurrentTenant.Change`（和 `IEmailNotificationAddressResolver` 的约定一样）。

## 8. 会话集成（可选）：`Dignite.NotificationCenter.Push.Identity`

**装上即启用，不需要开关**，和 `Emailing.Identity` 的做法一致。实现方式是继承桥接包里的存储类，重写 `IsActiveAsync`：

```
device.SessionId == null                         → 有效（注册时没有会话信息，无法判断）
IIdentitySessionRepository.ExistAsync(sessionId) → 存在则有效；不存在则视为失效，删除设备
```

效果：用户登出、被管理员吊销会话、触发并发登录限制、或者 30 天不活跃导致会话被清理之后，
这台设备都不会再收到推送。即使 App 端没能调用 unregister（例如 token 过期后被强制登出），也能兜住。

**前提**：宿主必须真正在维护 `IdentitySession`，也就是启用了 **Identity Pro** 的会话管理。
开源版 ABP 只有实体和仓储，没有写入会话的逻辑，同时也不会签发 `session_id` claim，
所以注册时 `SessionId` 为 null，所有设备都按"有效"处理，不会误删，只是这个包不起作用。
这一点要在 README 里写明。

campus 用的是 Identity Pro 和 Account Pro，属于适用对象。**待验证**：campus 签发给 App 的
access token 里是否带有 `session_id` claim（§14）。

## 9. 安全与隐私

1. **Expo token 本身就能被用来推送。** 没有开启 Enhanced Security 时，任何拿到 token 的人都能给这台设备发推送。
   所以生产环境必须开启并配置 `AccessToken`；token 不能进 URL，也不能写进日志。
2. **换绑语义带来的风险。** 拿到别人 token 的已登录用户，可以把这个 token 注册到自己名下：
   受害者从此收不到推送，攻击者自己的通知会推到受害者的手机上。攻击者**拿不到受害者的通知内容**。
   缓解手段是 token 保密（见第 1 条），风险较低，记录在案。
3. **内容会经过第三方。** 标题和正文要经过 Expo、Apple、Google 的服务器。
   内容提供方应该只写"有一条新的 XX，请打开查看"这一级别的信息，**不写金额、成绩等敏感内容**。
   campus 有工资单、银行流水这类数据，这一条必须写进 campus 的通知定义规范里。
4. 注册接口要求登录，限定在当前用户范围内；每个用户的设备数有上限（§6.2）。

## 10. campus 接入

### 10.1 账号与凭据（一次性）

1. Apple Developer：生成 APNs Auth Key（`.p8`），记下 Key ID 和 Team ID。
2. Firebase：创建项目，加入 Android 应用（包名 `com.dignite.campus`），下载 `google-services.json`，
   生成 FCM v1 服务账号密钥。
3. Expo：执行 `eas init` 关联项目（在 `app.config.js` 的 `extra.eas.projectId` 中生成项目 ID），
   用 `eas credentials` 上传上面两份凭据；开启 Enhanced Security，生成 Access Token 交给后端。
   **这些操作和打包方式无关**，用本地或 Mac 打包同样适用。

### 10.2 后端（`campus/src`）

1. 引用以下包：`Dignite.Abp.Notifications`、NotificationCenter 的 Domain / EF Core /
   Application(.Contracts) / HttpApi、`Notifications.Push`、`Push.Expo`、`NotificationCenter.Push`、
   `NotificationCenter.Push.Identity`；Web 端如果需要铃铛，再加 `Notifications.SignalR` 和 UI 包。
2. 在 `CampusDbContext` 中调用 `ConfigureNotificationCenter()`，然后生成迁移。
3. 配置 `ExpoPushOptions.AccessToken`，从机密配置读取，不能写进仓库。
4. 用 `INotificationDefinitionProvider` 定义 campus 自己的通知，加上 `UseChannels("Push")`。
   在业务代码中通过 `INotificationPublisher` 发布。
5. 为 campus 的每种通知类型编写 `NotificationPushContentProvider<TData>`，按 §9 第 3 条控制敏感度。

### 10.3 App（`campus/react-native`）

| 步骤 | 位置 | 说明 |
|---|---|---|
| 安装依赖 | `package.json` / `app.config.js` | `npx expo install expo-notifications expo-device`，在 plugins 中加入 `expo-notifications`，Android 配置 `googleServicesFile`。确认 `blockedPermissions` 里没有 `POST_NOTIFICATIONS` |
| 申请权限 | 登录成功后 | 选在有上下文的时机申请，不要一启动就弹窗；iOS 只会弹一次。Android 13 及以上需要运行时授权 |
| 注册 | `setAppConfig` 监听器中确认已登录后；切换语言后；`addPushTokenListener` 回调中 | 调用 `getExpoPushTokenAsync({ projectId })` 后执行 `POST …/push-devices/register`。每次启动都注册一次，服务端会刷新 `LastSeenTime` 和语言 |
| 解绑 | `useLogout` 中，**放在 `fetchAndRevokeTokens` 之前** | 这个请求需要有效的 access token，失败直接忽略。`logoutAsync`（强制登出）这条路径也尝试调用，失败时由会话集成兜底 |
| 前台展示 | 启动时 | `setNotificationHandler` 决定 App 在前台时是否显示；Android 用 `setNotificationChannelAsync` 创建默认通知渠道 |
| 点击跳转 | 根导航 | `addNotificationResponseReceivedListener` 处理点击，冷启动时用 `getLastNotificationResponseAsync`。按 `notificationName` / `entityId` 跳转，再调用 mark-as-read |
| Web 端 | — | `Platform.OS === 'web'` 时跳过全部推送逻辑 |

`expo-notifications` 是原生模块，**必须重新打包上架**。建议这一版照常发布，推送放到下一版。

## 11. 测试

| 范围 | 用例 |
|---|---|
| `Notifications.Push` | 渠道校验；没有设备时直接返回；按语言分组且每种语言只生成一次文案；内容为 null 时跳过；data 的四个键；`TokenInvalid` 触发删除；未知提供方记警告后跳过；一个提供方失败不影响另一个且最后抛出；取消；Null 存储的警告 |
| `Push.Expo` | 用假 `HttpMessageHandler` 测试：每批 100 条的分批；Bearer 头；回执单和消息按顺序对应；`DeviceNotRegistered` 映射为 `TokenInvalid`；请求级失败时整批 `Failed` |
| NotificationCenter（TestBase，EF 和 Mongo 各一遍） | 注册新建与刷新；跨用户换绑；跨租户换绑；unregister 只能删自己的设备；超过上限时淘汰最旧的设备；`RemoveByToken` |
| `Push.Identity` | 会话不存在时设备被跳过并删除；`SessionId` 为 null 时视为有效 |
| 不变量回归 | ETO 未改动，现有的序列化往返测试保持通过 |

## 12. 兼容性与发布

- **对 Center 用户是破坏性变更**：`INotificationCenterDbContext` 新增了 `PushDevices`。
  自己实现这个接口的宿主 DbContext 必须补上这个属性，并生成迁移（新增一张表）。目前还在 rc 阶段，可以接受，
  在 CHANGELOG 里写明即可。
- 新增 4 个包，需要同步更新：两个 `.slnx`、`Directory.Packages.props`（只用到 `Microsoft.Extensions.Http`）、
  `Dignite.NotificationCenter.abpmdl` / Installer、README 的包表、`notifications/CLAUDE.md` 的结构表、
  `notifications-invariants` §3 中的插件列表。
- 版本随仓库统一发布（下一个 rc）。

## 13. 明确不做 / 以后再说

| 项目 | 原因 | 什么时候再考虑 |
|---|---|---|
| Expo 回执轮询 | 属于投递平台的基础设施（§5.3） | 失效 token 堆积成为实际问题时 |
| FCM / APNs 直连提供方 | 目前没有消费者 | 出现原生 App，或者需要摆脱 Expo 时 |
| 角标（badge） | 需要再加一个"未读数"接口；App 打开后可以自己调用 `unread-count` 设置角标 | campus 上线后用户有明确诉求时 |
| 内容提供方自定义 data 键 | YAGNI | App 需要额外的路由信息时 |
| 不活跃设备按时间过期 | 设备数上限和会话集成已经兜住；按时间过期会误伤很少打开 App、但依赖推送的用户 | — |
| Web Push（浏览器推送） | 不同的协议（VAPID），网页端已经有 SignalR | 有需求时作为另一个提供方加入 |
| 按设备 / 按渠道退订 | 这是 #36 的范畴 | 跟 #36 一起解决 |
| 多个 App 共用一个后端 | Expo 不允许一次请求混用多个项目的 token；FCM 凭据也是按项目区分的 | 出现第二个 App 时 |
| 国内厂商通道 | 不在本次讨论范围内 | — |

## 14. 待决问题

1. **包名**：`Dignite.Abp.Notifications.Push` / `.Push.Expo` / `Dignite.NotificationCenter.Push` /
   `.Push.Identity` 是否可以？
2. **会话集成是否放进第一期？** 建议放：campus 适用，代码量也很小。
3. **`MaxDevicesPerUser` 默认值**：建议 10，做成 Options 可配。
4. **共享文化工具的提取**（§4.1）：是否接受顺带对 Emailing 做这次小重构？
5. **campus 验证**：OpenIddict 签发给 App 的 access token 是否带 `session_id` claim（决定 §8 能否生效）；
   第一批要推送的业务事件有哪些，各自的敏感度如何（决定 §10.2 第 4、5 步）。
