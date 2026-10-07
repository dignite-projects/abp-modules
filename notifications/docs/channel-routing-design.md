# 通知渠道路由设计（定稿 · 待实现）

> 把「一条通知走哪些渠道」的决定权从通知定义里拿出来：业务模块给默认值，接入宿主最后覆盖，Core 不认识任何
> 具体渠道名。本稿是设计，不含代码改动；第 10 节是实现清单，第 11 节是评审过程中已经否决的方案及理由。

## 0. 结论先行

- **删除 `NotificationDefinition.UseChannels(...)`**，渠道不再是定义的属性。
- 新增 **`NotificationRoutingOptions`**（Core），只有两级规则：按通知名的 `ForNotification(...)` 和兜底的
  `Default`。**没有组级规则。**
- 业务模块在自己的 `ConfigureServices` 里为**自己的**通知写 `ForNotification`；宿主模块最后加载，再
  `Configure<NotificationRoutingOptions>` 一次就覆盖——这是纯粹的 ABP Options 语义，后写覆盖先写。
- 分发器通过新接口 **`INotificationChannelResolver`** 取渠道，**每条通知解析一次**，不按收件人。默认实现只读
  Options；需要动态逻辑（按租户、按严重程度）的宿主替换它。
- Core 和 Abstractions **不包含任何渠道名常量**。渠道名是开放集合，各渠道包保留自己的
  `XxxNotifier.ChannelName`；业务模块想要强类型就引用渠道包，不想耦合就写字符串。
- 分发管线其余部分（按批扇出、ETO、`NotificationDeliveryRequestedHandler`）**不改**。

## 1. 要解决的问题

现状：渠道在定义通知时写死，`orders.AddNotification(...).UseChannels(SignalRNotifier.ChannelName)`。

1. **该做决定的人不对。** 可复用模块（`Dignite.Vault`、`Dignite.Site`）不知道宿主装了哪些渠道。模块写了
   `UseChannels("Push")` 而宿主没装 Push，分发器照样给每个用户发一条事件，handler 一句 Debug 日志就丢掉。
2. **编译期耦合。** 渠道名常量住在各渠道包里（`EmailNotifier.ChannelName` 在 `Notifications.Emailing`）。
   业务模块想用常量就得引用渠道包，把 `Volo.Abp.Emailing` 一起带进来；否则只能写魔法字符串。
3. **宿主覆盖今天「能用但不成体系」。** 宿主的 `NotificationDefinitionProvider` 最后执行，
   `context.GetOrNull("X")?.UseChannels(...)` 能改，但表达不了「清掉渠道、只进收件箱」（`UseChannels()`
   空参直接抛），没有全局兜底，靠的是隐式执行顺序。

## 2. 决策链与本设计的覆盖范围

「谁决定渠道」是一条链，不是一个开关：

| 层 | 谁 | 本设计 |
|---|---|---|
| 模块 | 模块作者：知道这条通知有多急，不知道宿主装了什么 | `ForNotification` 默认值 |
| 宿主 | 接入方开发者：知道装了什么、默认走什么 | `ForNotification` 覆盖 + `Default` |
| 租户 | SaaS 租户管理员 | **不做**；替换 `INotificationChannelResolver` 即可接入 ABP Settings |
| 用户 | 收件人：「我不要推送」 | **不做**（#36）；留给按批过滤器，见 §7 |

## 3. 与模块不变量的对照

| 不变量 | 本设计如何遵守 |
|---|---|
| 通知器只依赖 `Abstractions` | 不变。路由规则在 Core，通知器不感知 |
| 添加一个通知器包不得让已有通知自动扇出到新渠道 | `Default` 永远不是「所有已注册渠道」；三处都没命中就是只进收件箱 |
| 两种运行模式都能工作 | 无状态模式（`NullNotificationStore`）下解析为空仍然失败，而且提前到启动时 |
| 分发按批、O(收件人) 只做一次存储和扇出 | 解析按通知一次，接口签名没有 `userId` |
| 日志里不出现收件人 | 不变 |
| 拆分部署：一个进程可以不托管某个渠道 | 未托管渠道默认 Warning，不是失败；`RequireHostedChannels` 可选升级 |

## 4. `NotificationRoutingOptions`

```csharp
namespace Dignite.Abp.Notifications;

/// Maps notification names to the external channels they are delivered on. Modules add defaults for their own
/// notifications; the host module, configured last, overrides any of them or sets the fallback.
public class NotificationRoutingOptions
{
    /// Channels for notifications without an explicit rule. Null or empty = inbox-only.
    public string[]? Default { get; set; }

    /// Notification name → channels. An empty array is an explicit inbox-only rule and is different from no rule.
    /// Keys use ordinal, case-sensitive comparison (notification names); channel names are compared
    /// ordinal, case-insensitive when resolved, like NotificationNotifierOptions.
    public IDictionary<string, string[]> Notifications { get; }

    /// At least one channel; use InboxOnly for the explicit no-external-channel rule.
    public NotificationRoutingOptions ForNotification(string notificationName, params string[] channels);

    /// Explicit inbox-only rule: overrides Default for this notification. Stored as an empty array.
    public NotificationRoutingOptions InboxOnly(params string[] notificationNames);

    /// Sugar: one rule per name, same channels. Expands into Notifications; introduces no new key space.
    public NotificationRoutingOptions ForNotifications(IEnumerable<string> notificationNames, params string[] channels);

    /// Opt-in: fail startup instead of warning when a rule names a channel this process does not host.
    public bool RequireHostedChannels { get; set; }
}
```

规则：

- `ForNotification` 至少一个渠道，零渠道抛 `ArgumentException`。**显式只进收件箱**用 `InboxOnly("X")`，
  含义是覆盖 `Default`、不发任何外部渠道；内部存空数组。意图只有一种写法，读代码的人不会把零参误认为漏写。
  零参 `ForNotification` 和零渠道 `InboxOnly` 的区别是有意的：前者是错误，后者是规则。
- 渠道名 `Trim()` 后去重（ordinal, ignore-case），空白渠道名抛 `ArgumentException`。
- 同一通知名重复调用：后写覆盖先写（整体替换，不合并）。这就是宿主覆盖模块的方式。
- 不提供 `ForGroup`。原因见 §11.1。

用法：

```csharp
// 业务模块 Dignite.Vault，ConfigureServices：
Configure<NotificationRoutingOptions>(options =>
{
    options.ForNotifications(
        [VaultNotifications.ExtractCompleted, VaultNotifications.ExtractFailed],
        "SignalR", "Push");
});

// 宿主（可选，不写也能跑）：
Configure<NotificationRoutingOptions>(options =>
{
    options.Default = ["SignalR"];                                        // 没被命中的通知
    options.ForNotification(VaultNotifications.ExtractFailed, "SignalR", "Email");  // 覆盖模块默认
    options.InboxOnly("Demo.AuditLog");                                   // 显式只进收件箱，不走 Default
});
```

绑定 appsettings 走标准 Options 绑定（`Configure<NotificationRoutingOptions>(configuration.GetSection(...))`），
字典键是通知名；不单独设计配置节。

## 5. `INotificationChannelResolver`

```csharp
namespace Dignite.Abp.Notifications;

public interface INotificationChannelResolver
{
    /// Resolves the external channels for one notification. Called once per distribution, before recipients are
    /// batched. Null or empty = inbox-only. Runs inside CurrentTenant.Change(notification.TenantId).
    Task<string[]?> ResolveAsync(
        NotificationDefinition definition,
        NotificationInfo notification,
        CancellationToken cancellationToken = default);
}
```

`DefaultNotificationChannelResolver`（`ITransientDependency`，方法 virtual）：

1. `Notifications.TryGetValue(definition.Name)` 命中 → 返回（空数组 → null）。
2. 否则 `Default`（空 → null）。

`DefaultNotificationDistributor.ResolveExternalChannelsOrNull` 改为调用它；该方法本来就是 virtual 钩子，
`DistributeAsync` 内调用点不动。无状态模式的检查保留在分发器里（租户级动态解析无法在启动时校验）：
解析为 null 且 `Store is NullNotificationStore` → 抛 `AbpException`，消息改为指向 `NotificationRoutingOptions`。

签名里有 `notification`（含 `Severity`、`TenantId`、`Data`）是给替换实现用的——默认实现不看它。
签名里**没有** `userId`，见 §7。

## 6. 启动校验

放进现有的 `NotificationDefinitionStartupService`（它已经在 `StartingAsync` 里物化定义表），紧跟
`Registration.Validate()` 之后：

| 情况 | 处理 |
|---|---|
| `Notifications` 里的通知名不在定义表 | **启动失败**，消息列出所有未知名字。模块作者拼错自己的通知名也在这里被抓住 |
| 规则或 `Default` 里的渠道名不在本进程 `NotificationNotifierOptions.Notifiers` | 默认 **Warning**（逐渠道一条，列出引用它的通知名）；`RequireHostedChannels = true` 时启动失败 |
| `Store is NullNotificationStore` 且某定义按默认解析器解析为空 | **启动失败**，列出通知名 |

第三条只对默认解析器成立：`INotificationChannelResolver` 被替换时跳过，运行时检查兜底。实现上通过
`resolver is DefaultNotificationChannelResolver` 判断即可，不引入新接口。

运行时：`NotificationDeliveryRequestedHandler` 里「channel 未被本进程托管」那条日志**保持 Debug**。它在拆分部署里
每个收件人 × 每个渠道触发一次，升到 Warning 会刷屏；配置错误由上面的启动校验一次性报告，运行时不再重复。

## 7. 为用户层（#36）预留的位置

用户选渠道（「我不要推送」）**不进 resolver**。它天然按收件人，塞进 resolver 会把 O(通知) 的解析变成
O(收件人) 的解析。将来的形态是分发循环里的按批过滤：

```csharp
// 不在本次范围内，只约定位置
Task<IReadOnlyDictionary<Guid, string[]>> FilterAsync(
    NotificationInfo notification, string[] channels, IReadOnlyList<Guid> userIds, CancellationToken ct);
```

插在 `ProcessCandidateBatchAsync` 写完收件箱、发 ETO 之前。实现它的前提是订阅实体加渠道字段
（`NotificationSubscription` 今天没有），到时再做。本设计只要求：分发器里「解析渠道」和「逐人发 ETO」之间
保持一个清晰的批级断点，不要把两者揉成一个循环。

## 8. 删除与修改清单

**删除**

- `NotificationDefinition.UseChannels(...)`、`GetChannelsOrNull()`，以及 `Attributes` 注释里对渠道路由的提及
  （`Attributes` 本身保留，仍是自由扩展袋）。
- `NotificationChannels` 静态类（`AttributeName`、`IsAllowed`）。已确认 `IsAllowed` 只有测试在用；
  通知器按 `request.Channel == ChannelName` 校验，不走它。
- `NotificationChannelRouting_Tests` 里针对 `UseChannels` 的用例。

**修改**

- `DefaultNotificationDistributor`：注入 `INotificationChannelResolver`，`ResolveExternalChannelsOrNull` 改为
  异步调用它；错误消息改指向 `NotificationRoutingOptions`。
- `NotificationDefinitionStartupService`：加 §6 的校验。
- `NotificationDeliveryRequestedHandler`：不改（日志级别保持 Debug，见 §6）。
- `AbpNotificationsModule`：`AddValidatedOptions<NotificationRoutingOptions>` 做结构校验（空白渠道名）；
  名字对账放在 StartupService，因为定义表只在那里可达。
- 测试：`TestNotificationDefinitionProvider`、`DefaultNotificationDistributorTests`、
  `DefaultNotificationPublisherTests`、NotificationCenter `TestBase` 的 provider，把 `UseChannels` 换成
  测试模块里的 `Configure<NotificationRoutingOptions>`。
- `host/.../DemoNotificationDefinitionProvider` → 渠道配置移到 host 模块的 `ConfigureServices`。
- `README.md`：§「Register the notification definition」和 §「Use UseChannels only for external channels」改写；
  新增「Routing」小节说明两级规则、模块默认 + 宿主覆盖、显式只进收件箱、启动校验三档。
- `docs/push-design.md`：三处 `UseChannels("Push")` 改为 Options 写法（第 13、22、322 行附近）。
- `CLAUDE.md`（notifications）：「Adding a feature」里 `UseChannels(...)` 一句改为 Options。

**新增**

- `NotificationRoutingOptions`、`INotificationChannelResolver`、`DefaultNotificationChannelResolver`（Core）。
- 测试：规则覆盖顺序、`InboxOnly` vs 无规则、零参 `ForNotification` 抛、`Default` 兜底、启动校验三档、无状态模式提前失败、
  `RequireHostedChannels`。

## 9. 测试要点

| 项 | 断言 |
|---|---|
| 覆盖顺序 | 两次 `ForNotification` 同名，后者整体替换前者（不合并） |
| 显式只进收件箱 | `InboxOnly("X")` → 解析为 null，即使 `Default` 非空；`ForNotification("X")` 零参抛 `ArgumentException` |
| 覆盖方向 | `ForNotification` 后接 `InboxOnly` 同名 → 空；反过来 → 渠道。两者是同一张表的两种写法 |
| 兜底 | 无规则 → `Default`；`Default` 为空 → null |
| 大小写 | 渠道名 `"signalr"` 和 `"SignalR"` 去重为一个；通知名区分大小写 |
| 启动：未知通知名 | `StartingAsync` 抛，消息含全部未知名 |
| 启动：未托管渠道 | 默认只 Warning；`RequireHostedChannels` 时抛 |
| 启动：无状态模式 | `NullNotificationStore` + 解析为空 → 抛；替换 resolver 后不抛 |
| 分发器 | 解析只调用一次（NSubstitute `Received(1)`），与收件人数量无关 |

## 10. 实现顺序

1. Core：Options + resolver + 分发器接线 + 删除 `UseChannels`（一个 PR，编译通过为止，含测试迁移）。
2. 启动校验 + handler 日志级别。
3. host、README、push-design.md、CLAUDE.md。

兼容性：模块没有外部用户（见记忆 `notification-center-no-users`），直接删，不留 `[Obsolete]`，不写升级指南。

## 11. 评审中否决的方案

### 11.1 组级规则 `ForGroup`

曾经在方案里，被拿掉。两个原因：

- **组会从「展示分类」变成「路由作用域」。** `NotificationGroupDefinition` 的定位是定义期元数据，不持久化，
  重新分组免费。组一旦是路由键，重新分组就改变通知走哪个渠道；宿主用 `GetGroupOrNull` 往模块的组里追加
  通知，会悄悄继承模块的渠道。
- **「具体优先」和「宿主最后写」是两套规则，会打架。** 有了通知级 > 组级的优先序，模块一旦写了通知级规则，
  宿主的组级规则就压不住，只能靠「模块只写组级」的约定。去掉组级，只剩一个具体层，覆盖就是纯粹的
  后写覆盖先写，不需要约定。

代价是宿主想整体改掉一个模块的一批通知得逐条写。实际场景里最常见的「这个宿主没装 Push」不需要任何配置
（Warning + 运行时丢弃）；真要在拆分部署里主动关掉某个渠道，正确的杠杆是宿主级的 `DisableChannel("Push")`
——系统级事实，和组无关。有真实需求再加，接口位置不冲突。

### 11.2 保留 `UseChannels` 作为模块默认值

和「业务模块里 `Configure<NotificationRoutingOptions>`」语义相同，只是多了一种写法和一张「定义属性 vs
Options」的优先级表。两者只留一个，留 Options，因为它让模块默认和宿主覆盖走同一机制。

### 11.3 渠道名常量下沉到 Abstractions

`NotificationChannelNames.SignalR / Email / Push` 放进 Abstractions 意味着 Abstractions 认识三个具体渠道，
而渠道是开放集合，第三方渠道包进不了这个列表。否决。常量留在各渠道包，业务模块自己选引用还是写字符串。

### 11.4 直接用 ABP Settings 存路由

好处是免费得到 appsettings `Settings:` 节、Setting Management UI、租户层。但值是字符串、宿主代码侧没有强类型
入口、Core 多一个 `Volo.Abp.Settings` 依赖，而当前没有租户级需求。保留为 `INotificationChannelResolver`
的一种替换实现，不做默认。

### 11.5 发布时传渠道（旧 ABP Zero 的 `targetNotifiers`）

这是「代码决定」，正是要离开的方向。将来若有单次覆盖的需要，`PublishAsync` 加一个可选参数即可，
不影响本设计。
