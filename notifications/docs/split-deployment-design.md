# 拆分部署设计：独立的通知服务（定稿 · 待实现）

> 让通知模块能以「发布方在一个进程、收件箱和渠道在另一个进程」的方式部署，第一个消费者是 Dignite.Cloud 的
> NotificationService。本稿只写设计，不含代码；第 12 节是改动清单，第 15 节是评审中否决的方案及理由，
> 第 16 节是实施时必须先核实的事实。所有 `path:line` 引用以 abp-modules `10.0.0-rc.22`、ABP `10.5.0`、
> cloud `main@37cda52` 为准。
>
> 设计原则：**不发明机制，每个部件都对应一条 ABP 既有做法。** 每节末尾标注它照抄的是什么。

## 0. 结论先行

- **Core 拆成两层。** `Dignite.Abp.Notifications` 只剩业务模块需要的东西（定义、路由、`INotificationPublisher`
  契约、负载类型注册、`INotificationStore` 等契约）；分发器、后台作业、投递事件处理器、`NullNotificationStore`
  移到新包 **`Dignite.Abp.Notifications.Distribution`**。业务模块的 PackageReference 和 `DependsOn` 不变。
- **远程发布只发一条事件。** 新包 **`Dignite.Abp.Notifications.Remote`** 实现 `INotificationPublisher`：本地校验定义、
  本地解析渠道、把负载序列化成 JSON，发 **`NotificationPublishRequestedEto`**（放 Abstractions）。
  事件经发布方自己的 outbox 出去，和业务变更同一事务。Distribution 里的处理器收到后交给本地分发器。
- **定义目录照 ABP 动态权限存储做。** 新包 **`Dignite.Abp.Notifications.DefinitionStore`** 及
  `.DefinitionStore.EntityFrameworkCore`：每个发布方启动时把静态定义存进通知服务的库，通知服务动态读取。
  `INotificationDefinitionManager` 改为异步。
- **`Notifications.Identity` 改依赖 `IUserRoleFinder`。** 不再引用 `Volo.Abp.Identity.Domain`；单体由 Identity.Domain
  提供实现，微服务由 `Volo.Abp.Identity.Pro.HttpApi.Client` 提供。
- **推送设备的会话清理不在本稿范围。** `Push.Identity` 保持原样（见 §8）。
- 消灭两个现在就存在的问题：分发作业队列跨服务共享、投递事件广播到每个装了 Core 的服务（§1.2、§1.3）。
- Angular 库、手机端契约、`NotificationDeliveryRequestedEto`、通知器插件边界、两种运行模式：**都不改**。

## 1. 要解决的问题

### 1.1 发布在发布方进程内完成，拆不开

`DefaultNotificationPublisher` 要求本进程有定义（`DefaultNotificationPublisher.cs:62`），去重后不超过
`DirectDistributionUserThreshold` 的显式扇出在线分发，否则入后台作业；按订阅发布（`userIds = null`）一律入作业
（`:76-85`）。`DefaultNotificationDistributor` 在同一个工作单元里：切到通知的租户（`:76`）→ 从 `INotificationStore`
分页取订阅者（`:96`）→ 逐人 `IsAvailableAsync`（`:165-176`）→ 写 `Notification` 和 `UserNotification`（`:183-199`）→
每个「人 × 渠道」发一条 `NotificationDeliveryRequestedEto`（`:207-213`）。

模块今天支持的「拆分部署」只到渠道层：不在本进程的渠道，`NotificationDeliveryRequestedHandler` 直接忽略
（`:70-80`）。收件箱和分发器必须和发布方同进程。

Dignite.Cloud 的现状因此是把整个 NotificationCenter 装进了 VaultService（`CloudVaultServiceModule.cs:116-123`），
靠把 `NotificationCenter` 连接串映射到 VaultService 的库（`:600-609`）让收件箱写入和 outbox 落在同一事务。
第二个发布方（CampusService 托管 `Dignite.Campus.Support`）一出现，这个结构就没法延续。

### 1.2 分发作业的队列跨服务共享

`NotificationDistributionJobArgs` 没有 `[BackgroundJobName]`，RabbitMQ 队列名是类型全名
`AbpBackgroundJobs.Dignite.Abp.Notifications.NotificationDistributionJobArgs`（`[backgroundjobs.rabbitmq]JobQueue.cs:70`）。
ABP 给每个已注册的作业类型都启动消费者（`JobQueueManager.cs:36-39`），cloud 没配队列前缀。只要两个服务都装了
Core，A 服务入队的分发作业就可能在 B 服务执行——用 B 的 store、定义和权限检查器。

### 1.3 投递事件广播

`AbpNotificationsModule` 无条件注册 `NotificationDeliveryRequestedHandler`（`:50-53`）。RabbitMQ 队列名就是
`ClientName`（`RabbitMqDistributedEventBus.cs:70`），不同服务各收一份。每条投递在每个装了 Core 的服务里都多一次
inbox 写入，再被当作「未托管渠道」忽略。

### 1.4 定义只在进程内

定义由 provider 在进程内构建成 Lazy 快照（`NotificationDefinitionManager.cs:32,119-134`）。收件箱按定义算分组和
显示名；找不到定义的通知归到 "Other" 组、显示名为 null（`UserNotificationAppService.cs:57-95, 161-172`），
订阅设置页则完全列不出来。通知服务不托管任何业务模块，没有定义就没有可用的 UI。

### 1.5 三个 `.Identity` 包直接读 Identity 库

`Notifications.Identity` 用 `IIdentityUserRepository` + `IUserClaimsPrincipalFactory<IdentityUser>`
（`IdentityNotificationPermissionChecker.cs:20-46`），`Emailing.Identity` 用 `IIdentityUserRepository`，
`Push.Identity` 用 `IIdentitySessionRepository`，都依赖 `AbpIdentityDomainModule`。cloud 里只有 AuthServer 和
IdentityService 映射了 Identity 库。不装 `Notifications.Identity` 不报错，静默退化成 `AlwaysGranted`。

## 2. 目标拓扑

```
发布方服务（VaultService、将来的 CampusService）
  业务模块 ──► INotificationPublisher（Remote）
                │ 校验定义存在 · 解析渠道 · 序列化负载
                ▼
      NotificationPublishRequestedEto ──► 本服务 outbox ──► RabbitMQ
  启动时：DefinitionStore 的 StaticSaver 把定义写进通知服务的库

通知服务（NotificationService）
  inbox ──► NotificationPublishRequestedHandler ──► 本地分发器（Distribution）
                                                      │ 订阅者 · 资格过滤 · 收件箱 · 投递事件
  通知器：SignalR（Expo / Email 按需）◄──────────────┘
  REST /api/notification-center · hub /signalr-hubs/notifications
  DefinitionStore 的 DynamicStore：读所有发布方写入的定义
```

职责边界：发布方只知道「发生了什么、通知谁、走什么渠道」。通知服务拥有收件箱、订阅、推送设备、全部渠道、定义目录。

**不变的东西**：单体宿主（私有化 vault、campus）继续用 Distribution 做进程内分发，行为与今天相同；
`@dignite/ng.notification-center` 的 REST 和 hub 基址来自同一个 `apiName`（`user-notification.service.ts:11`、
`notification-bell.component.ts:257-261`），整体指向网关即可；react-native 的 `/api/notification-center/push-devices/*`
路径不变。

照抄：ABP Chat (Pro) 的「API 进程发分布式事件，SignalR 宿主消费后推给用户」；本模块自己的
`NotificationDeliveryRequestedEto`。

## 3. 与模块不变量的对照

| 不变量 | 本设计如何遵守 |
|---|---|
| 模块身份是「尽力而为的应用内通知」，不是至少一次投递平台 | 没有新的状态机、租约、重试、遥测。跨进程只多一条事件，可靠性完全交给 ABP 的 outbox/inbox（§4 明确允许这样做） |
| §1 没有任何线路契约携带活的 `NotificationData` | `NotificationPublishRequestedEto.DataJson` 是预序列化字符串；`NotificationInfo` 改为携带 `DataJson`，序列化前移到发布边界 |
| §3 通知器只依赖 Abstractions | 不变。通知器不感知 Remote / Distribution 的区别 |
| §4 投递事件单收件人、尽力而为 | `NotificationDeliveryRequestedEto` 不改。`NotificationPublishRequestedEto` 携带收件人数组，但它是分发**之前**的服务端事件，和 `NotificationDistributionJobArgs` 携带 `Guid[]` 是同一层，不到达通知器或客户端 |
| §5 两种运行模式都能工作 | 无状态模式 = Core + Distribution（含 `NullNotificationStore`）+ 通知器；完整模式再加 NotificationCenter。Distribution 不假设 Center 存在 |
| §7 定义要求在投递时再次生效 | 资格过滤仍在分发器里做，只是分发器在通知服务。定义来自 DynamicStore，权限/功能名来自其它服务时靠 ABP 动态存储（§7） |
| §7 在通知记录的租户里评估 | ETO 实现 `IMultiTenant`，处理器在该租户下运行（`EventBusBase.cs:282`），`NotificationInfo.TenantId` 显式赋值 |
| §8 收件人工作有界 | 处理器沿用发布方的阈值逻辑：小扇出在线，大扇出入**本地**作业。作业只在通知服务注册 |
| 不记录收件人 ID | 不变 |

## 4. 包的重新划分

| 包 | 内容 | 谁安装 |
|---|---|---|
| `Dignite.Abp.Notifications.Abstractions` | 现有内容 + `NotificationPublishRequestedEto` | 所有人（传递） |
| `Dignite.Abp.Notifications`（Core） | 定义 API（provider、context、manager、definition 类型）、`NotificationRoutingOptions` 和 `INotificationChannelResolver`、`INotificationPublisher` 契约、`NotificationEntityIdentifier`、`NotificationInfo` 等信息记录、`INotificationStore`、`INotificationDistributor`、`INotificationPermissionChecker` 契约、路由名对账的启动校验 | 业务模块（不变） |
| `Dignite.Abp.Notifications.Distribution`（新） | `DefaultNotificationPublisher`、`DefaultNotificationDistributor`、`NotificationDistributionJob`、`NotificationDeliveryRequestedHandler`、`NotificationPublishRequestedHandler`、`NullNotificationStore`、`AlwaysGrantedNotificationPermissionChecker`、随 store 走的 manager、「未托管渠道」和「无状态模式下无渠道」两项启动校验 | 托管收件箱和渠道的进程：单体宿主、通知服务 |
| `Dignite.Abp.Notifications.Remote`（新） | 远程 `INotificationPublisher` | 不托管收件箱的发布方 |
| `Dignite.Abp.Notifications.DefinitionStore`（新） | 定义记录实体、`StaticNotificationDefinitionSaver`、`DynamicNotificationDefinitionStore`、初始化器、Options | 发布方（写）和通知服务（读写） |
| `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore`（新） | DbContext（连接串名 `NotificationCenter`）、仓储 | 同上 |
| `Dignite.NotificationCenter.*`、通知器包、`Emailing.Identity`、`Push.Identity` | 不改 | 不变 |
| `Dignite.Abp.Notifications.Identity` | 改依赖（§7） | 单体宿主、通知服务 |

划分规则：**契约留 Core，实现进 Distribution。** 其它包实现的接口（`INotificationStore`、`INotificationPermissionChecker`）
必须留在 Core，否则 NotificationCenter.Domain 和 Notifications.Identity 要反向依赖 Distribution。

兼容性约束：**业务模块今天引用的任何类型，命名空间和程序集都不动**（`AbpNotificationsModule`、`INotificationPublisher`、
`NotificationDefinitionProvider`、`NotificationRoutingOptions`、`LocalizableMessageNotificationData`……）。这样
`Dignite.Vault.Extract.Application` 和 `Dignite.Campus.Support.Application` 按 rc.22 编译的程序集可以直接跑在 rc.23 上，
cloud 不必等它们各自发版。

一个宿主既没装 Distribution 也没装 Remote 时，`INotificationPublisher` 无实现，第一次解析就失败——这是期望的失败方式，
不加静默兜底。

照抄：`Volo.Abp.BackgroundJobs.Abstractions`（入队契约）与 `Volo.Abp.BackgroundJobs`（执行）的分法；
`Volo.Abp.Authorization`（定义 API）与 `Volo.Abp.PermissionManagement.Domain`（存储）的分法。

## 5. 远程发布

### 5.1 `NotificationPublishRequestedEto`（Abstractions）

```csharp
[EventName("Dignite.Abp.Notifications.NotificationPublishRequested")]
public class NotificationPublishRequestedEto : IMultiTenant
{
    public Guid NotificationId { get; set; }          // 发布方生成，幂等键
    public Guid? TenantId { get; set; }               // null = host
    public string NotificationName { get; set; }
    public string? DataJson { get; set; }             // 预序列化，含 type 判别符
    public NotificationSeverity Severity { get; set; }
    public string? EntityTypeName { get; set; }
    public string? EntityId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid[]? UserIds { get; set; }              // null = 按订阅；空数组 = 不发
    public Guid[]? ExcludedUserIds { get; set; }
    public string[]? Channels { get; set; }           // 发布方已解析；null = 只进收件箱
}
```

平的 POCO，默认 System.Text.Json 可往返，和 `NotificationDeliveryRequestedEto` 同一纪律。字段集合和
`NotificationDistributionJobArgs` 对齐，它们描述的是同一个东西：「一条待分发的通知」。

### 5.2 Remote 发布器

`RemoteNotificationPublisher : INotificationPublisher`，`[Dependency(ReplaceServices = true)]`：

1. 本地 `INotificationDefinitionManager` 找定义，找不到抛——保留 `DefaultNotificationPublisher.cs:62` 的 fail-fast。
   业务模块的 provider 本来就在发布方进程里。
2. 本地 `INotificationChannelResolver` 解析渠道。路由规则是业务模块在自己的 `ConfigureServices` 里写的默认值加宿主覆盖
   （`VaultExtractApplicationModule.cs:39-42`、`CpsSupportApplicationModule.cs:52-59`），只有发布方进程有这些
   Options，所以渠道必须在这里解析。现有「模块默认 + 宿主最后覆盖」的模型一个字不改。
3. `INotificationDataSerializer.Serialize(data)` 得到 `DataJson`。
4. `IDistributedEventBus.PublishAsync(eto)`。有环境工作单元时进 outbox（`DistributedEventBusBase.cs:101-128`），
   和业务变更同一事务；没有时直接发。

去重和阈值判断都不在这里做：一条通知永远是一条事件，大小扇出的区分留给接收方。

### 5.3 接收处理器（Distribution）

`NotificationPublishRequestedHandler : IDistributedEventHandler<NotificationPublishRequestedEto>`：

1. 由 ETO 构造 `NotificationInfo`（`Id = NotificationId`，`TenantId` 显式赋值，`DataJson` 原样）。
2. 沿用 `DefaultNotificationPublisher` 的阈值逻辑：显式收件人且不超过 `DirectDistributionUserThreshold` → 在线调
   `INotificationDistributor`；否则入本地 `NotificationDistributionJob`。把这段逻辑从发布器里提成一个两边共用的方法。
3. 分发器在 `NotificationInfo.Channels` 已给出时直接使用，不再解析。

幂等：ABP inbox 按 `MessageId` 去重（`DistributedEventBusBase.cs:135-163`），处理器和「标记已处理」在同一个事务型工作
单元里（`InboxProcessor.cs:117-122`），收件箱写入和投递事件的 outbox 又在同一个库，所以一次成功处理是原子的。
`InsertNotificationAsync` 另加按 `Id` 的存在性预检（现在只有 `InsertUserNotificationsAsync` 有，
`NotificationStore.cs:106-121`），作为第二道保险。

### 5.4 负载透传

`NotificationInfo.Data`（活的 `NotificationData`）改为 `NotificationInfo.DataJson`；序列化在发布边界做一次
（本地发布器和远程发布器都是），store 写字符串（`Notification` 实体的构造函数本来就收 `string? data`，
`Notification.cs:34-42`），投递 ETO 复制字符串。通知服务不需要认识任何业务负载类型；读取仍走容错路径。

今天两个业务模块都只发 `LocalizableMessageNotificationData`（`DocumentNotificationPlanner.cs:129`、
`ConsultationNotificationDispatcher.cs:134`），Abstractions 默认注册它，所以 Email/Push 的内容提供者在通知服务里照常命中。
将来出现自定义负载类型：SignalR 原样推送 `DataJson`，不受影响；收件箱 REST 走不变量 §1 的容错读，未注册的判别符返回 `Dignite.Unsupported` 占位（原始 JSON 逐字节保留在 `rawJson`），Email/Push 找不到内容提供者则跳过。所以要让 REST/Angular 和 Email/Push 渲染它，通知服务必须注册其判别符；存储本身是透传的，注册之后历史数据照常可读。

照抄：本模块 `NotificationDeliveryRequestedEto` 的 `DataJson`；ABP `StaticPermissionSaver` 发
`DynamicPermissionDefinitionsChangedEto` 让别的服务做事。

## 6. 定义目录

逐项对照 `Volo.Abp.PermissionManagement.Domain`（`StaticPermissionSaver.cs`、`DynamicPermissionDefinitionStore.cs`、
`DynamicPermissionDefinitionStoreInMemoryCache.cs`、`PermissionDynamicInitializer.cs`），Feature 和 Setting 是同一套结构。

| 部件 | 本设计 | ABP 对应 |
|---|---|---|
| 记录实体 | `NotificationGroupDefinitionRecord`、`NotificationDefinitionRecord`（带 `HasSameData` / `Patch`） | `PermissionGroupDefinitionRecord` / `PermissionDefinitionRecord` |
| 表 | `NotifDefinitionGroups`、`NotifDefinitions`，连接串名 `NotificationCenter` | `AbpPermissionGroups` / `AbpPermissions` |
| 序列化 | `INotificationDefinitionSerializer`；显示名用 ABP 的 `ILocalizableStringSerializer`（`L:VaultExtract,Notification:Ready`） | `IPermissionDefinitionSerializer` |
| 静态保存 | `StaticNotificationDefinitionSaver`：应用级锁（不等待）→ MD5 hash 比缓存 → 公共锁 5 分钟 → 工作单元里插/改/删 → 更新公共 stamp | `StaticPermissionSaver.cs:70-76, 77-88, 90-96, 157-232, 103-108` |
| 删除 | 只删 `DeletedNotifications` / `DeletedNotificationGroups` 里显式列出的；多个服务共写一张表互不干扰 | `:175, 209-216` |
| 动态读取 | `DynamicNotificationDefinitionStore`（瞬态）+ `...InMemoryCache`（单例）：SemaphoreSlim 串行，每 30 秒比一次 stamp，变了全量重读；stamp 不存在时在公共锁下初始化 | `DynamicPermissionDefinitionStore.cs:113-134, 136-170`；缓存单例 |
| 初始化 | 后台启动，Polly 重试 | `PermissionDynamicInitializer.cs:97` |
| 开关 | `SaveStaticNotificationsToDatabase`（默认 true）、`IsDynamicNotificationStoreEnabled`（默认 false）；数据迁移环境下都关 | `PermissionManagementOptions`、`AbpPermissionManagementDomainModule.cs:27-33` |
| 合并 | manager 先静态后动态，静态优先 | `FeatureDefinitionManager.cs:26-48` |

需要跟着改的地方：

- `INotificationDefinitionManager` 的 `Get/GetOrNull/GetAll/GetGroups` 由同步改成异步（`INotificationDefinitionManager.cs:16-28`）。
  ABP 6 对 `IPermissionDefinitionManager` 做过同样的事。调用方在本仓库内：分发器、两个 AppService、MVC/Angular 不受影响。
- `NotificationDefinition.Attributes` 是 `object` 字典（`NotificationDefinition.cs:34`），照 ABP `PermissionDefinition.Properties`
  的处理：只序列化可 JSON 化的值进 `ExtraProperties`。
- 定义记录保存的是 `PermissionName` / `FeatureName`，分发时的资格过滤用它们；事件不携带门槛。

缓存键前缀：cloud 所有服务 Redis `KeyPrefix` 都是 `Cloud:`（`vault appsettings.json:43-44`），hash 按应用名、stamp 公共，
和 ABP 权限的做法一样跨服务共享。

本地化：定义记录里的显示名按名字解析。`LocalizableString.Create(name, resourceName)` 存在
（`[localization.abstractions]LocalizableString.cs:113`），`AbpStringLocalizerFactory` 在 options 里找不到资源时查
`IExternalLocalizationStore`（`AbpStringLocalizerFactory.cs:69-74`）。cloud 每个服务都依赖
`LanguageManagementEntityFrameworkCoreModule` 并映射 LanguageService 库，`SaveToExternalStore` 默认开。
Angular 走 `/api/abp/application-localization`，由 Administration 合并外部资源。`LocalizableMessageNotificationData`
本来就是按名字解析的（`LocalizableMessageNotificationDataExtensions.cs:21-24`）。

MongoDB：NotificationCenter 支持 Mongo，DefinitionStore 应有 `.MongoDB` 实现；cloud 不需要，作为后续项，不阻塞本稿。

照抄：ABP 权限 / 功能 / 设置三套动态定义存储；cloud 里每个服务映射 `AdministrationService` 连接串保存自己的定义
（VaultService 的 `VaultExtract.Enable` 就是这样进 `Cloud_Administration.AbpFeatures` 的）。

## 7. 收件人资格：权限与功能

分发器对每个候选人调 `IsAvailableAsync(name, userId)`：功能用 `IFeatureChecker.IsEnabledAsync`（看环境租户），权限委派给
`INotificationPermissionChecker(userId, permissionName)`（`NotificationDefinitionManager.cs:68-99`）。

`Notifications.Identity` 改成：

1. `IUserRoleFinder.GetRoleNamesAsync(userId)` 取角色（接口在 `Volo.Abp.Identity.Domain.Shared`）。
2. 自己拼 `ClaimsPrincipal`：`AbpClaimTypes.UserId`、每个角色一个 `AbpClaimTypes.Role`、`AbpClaimTypes.TenantId`
   取 `CurrentTenant.Id`（分发器已经切到通知的租户）。
3. `IPermissionChecker.IsGrantedAsync(principal, permissionName)`。

这正是 `PermissionChecker` 读取的三个 claim：U 提供者读 UserId，R 提供者读全部 Role，多租户侧由 `tenantid` 决定
（`[authorization]PermissionChecker.cs`、`AbpMultiTenancyClaimsIdentityExtensions.GetMultiTenancySide`）。
包不再依赖 `Volo.Abp.Identity.Domain`，改依赖 `Identity.Domain.Shared` + `Volo.Abp.Authorization`。

实现来源：单体由 Identity.Domain 的 `IdentityUserRoleFinder` 提供；微服务由 `Volo.Abp.Identity.Pro.HttpApi.Client` 的
`HttpClientUserRoleFinder` 提供，服务端的 `GetRoleNamesAsync` 含组织单元带来的角色。cloud 已有先例：AdministrationService
引用该包（`csproj:62`、`Module:99`），`RemoteServices:AbpIdentity` 直连 IdentityService（`appsettings.json:52-55`），
IdentityService 开了 `ExposeIntegrationServices`（`CloudIdentityServiceModule.cs:397`），客户端代理自动带 `__tenant` 头。

通知服务必须打开 `IsDynamicPermissionStoreEnabled` 和 `IsDynamicFeatureStoreEnabled`，否则别的服务定义的权限名在本进程
静默判 false（`PermissionManagementOptions` 默认 false；cloud 只有 Administration `:434` 和 Identity `:442` 开了）。
授权数据本身走 PermissionManagement 读共享的 Administration 库，VaultService 已这样映射（`CloudVaultServiceModule.cs:578-583`）。

已知限制（和今天的实现相同，不是回退）：每个收件人一次角色查询，集成服务没有批量接口；带 state checker 的权限读的是
环境里的 `ICurrentUser`，在后台分发上下文里一律视为未登录；不过滤 `IsActive`。

`Emailing.Identity` 同理可改用 `IExternalUserLookupServiceProvider`（ABP `Identity.HttpApi.Client` 有 HTTP 实现）。
cloud 现在不用 Email 渠道，列为后续项。

照抄：ABP `Volo.Abp.PermissionManagement.Domain` 的 `UserPermissionManagementProvider` 就是靠 `IUserRoleFinder` 在没有
Identity 库的进程里算用户的有效权限；微服务模板给 Administration 装 `HttpClientUserRoleFinder` 就是为了这个。

## 8. 推送设备与会话：本稿不覆盖

`Push.Identity` 的职责是「会话结束的设备不再推」，靠 `IIdentitySessionRepository` 在推送时过滤
（`IdentitySessionPushDeviceStore.cs:31-59`）。核实到的事实：

- Identity Pro 没有会话的集成服务，也没有会话的分布式事件；Identity 只给 `IdentityUser` 和 `IdentityRole` 配了自动 ETO
  （`[identity.domain]AbpIdentityDomainModule.cs:39-44`）。
- 用 `AutoEventSelectors.Add<IdentitySession>()` 走 `EntityDeletedEto<EntityEto>` 只覆盖一部分：`RevokeAsync` 和
  `RevokeAllAsync(userId)` 走加载后删除，会发实体事件；**不活跃会话的清理**（`IdentitySessionCleanupService` →
  `DeleteAllAsync(TimeSpan)`）走 `DeleteDirectAsync` 批量 SQL，不发事件（`EfCoreIdentitySessionRepository.cs:92-95`）。
  而「刷新令牌过期后被迫下线」正是这条路。实体事件不能作为方案。
- ABP 对「服务 B 查 Identity 状态」的标准答案是集成服务。ABP 没有会话的，要自己按 ABP 规范在 IdentityService 加一个，
  契约需要单独打包给通知服务引用。

推送渠道只在 Campus 进 cloud 时才需要（Extract 只走 SignalR）。决定：**`Push.Identity` 保持原样服务单体；
会话集成服务的设计并入 Campus 进 cloud 的设计**，和职员查找等其它 Identity 查询需求一起定。本稿的通知服务在加推送渠道前
不装 `Push.Identity`。

## 9. 启动校验与失败方式

| 进程 | 情形 | 结果 |
|---|---|---|
| 任何宿主 | 既没装 Distribution 也没装 Remote | `INotificationPublisher` 无实现，首次解析失败 |
| 任何宿主 | 两个都装 | 启动失败，消息指出二选一 |
| 发布方（Remote） | 路由规则引用未定义的通知名 | 启动失败（Core 现有校验不动） |
| 发布方（Remote） | 规则引用的渠道本进程没有通知器 | 不校验——渠道在别处托管。「未托管渠道」校验随 Distribution 走 |
| 发布方（Remote） | 定义解析为无渠道 | 允许，表示只进收件箱 |
| 发布方（Remote） | 发布时定义不存在 | 抛，和本地发布器一致 |
| 通知服务 | 动态权限 / 功能存储未开 | DefinitionStore 的 `IsDynamicNotificationStoreEnabled` 开着而两者没开时启动警告，消息说明后果（别人的权限名静默判 false） |
| 通知服务 | 收到的 `NotificationName` 在动态目录里也不存在 | 记日志，按无定义分发（只进收件箱、"Other" 组），不丢弃——目录同步与事件到达有时间差 |
| 无状态模式（Distribution + Null store） | 定义解析为无渠道 | 启动失败（现有校验，位置移到 Distribution） |

## 10. Dignite.Cloud 的落地

### 10.1 NotificationService（ABP Studio 已生成模板，未提交）

模板之外要手补的通用项（样本 CampusService 核实）：`UserSharingStrategy = Shared`（模块和测试，CLAUDE.md 约定）、
Helm chart 的 `ConnectionStrings__LanguageService`（campus / vault / site 的 chart 都漏了）、`environment.prod.ts` 和
Helm `angular-configmap.yaml` 的 scope、run profile 的 `execution.order`、Prometheus。

通知专有：

- 包：Core、Distribution、`DefinitionStore` 及 EF、`NotificationCenter.{Domain,Application,HttpApi,EntityFrameworkCore}`、
  `Notifications.SignalR`、`Notifications.Identity`、`Volo.Abp.Identity.Pro.HttpApi.Client`、
  `Microsoft.AspNetCore.SignalR.StackExchangeRedis`。
- 自有 DbContext 调 `ConfigureNotificationCenter()` 和 `ConfigureNotificationDefinitionStore()`，连接串名 `NotificationCenter`
  映射到本服务库，迁移由本服务生成——和 VaultService 接 Extract 的方式相同。
- `IsDynamicPermissionStoreEnabled` / `IsDynamicFeatureStoreEnabled` 打开。
- `RemoteServices:AbpIdentity:BaseUrl` 直连 IdentityService，照 Administration。
- JwtBearer `OnMessageReceived` 从 `/signalr-hubs` 的 query 取 `access_token`，从 VaultService 搬过来
  （`CloudVaultServiceModule.cs:261-281`）。
- SignalR Redis backplane：现在副本都是 1，但 RabbitMQ 同队列竞争消费意味着副本 > 1 时只有消费到事件的实例能推
  （`RabbitMqDistributedEventBus.cs:86-91`）。Redis 已有，一开始就配。
- 网关：`VaultNotificationCenter`、`VaultNotificationHub` 两条路由（`gateways/web/.../appsettings.json:228-239`）目标集群
  换成 Notification，Helm `webgateway.yaml` 加集群地址。
- Angular：`environment.ts:10` 和 OpenIddict 种子的 Angular / WebPublic / Swagger 客户端加 `NotificationService` scope；
  `notifications.provider.ts` 和 `package.json` 里「served by VaultService」「同 services/vault/common.props 版本」的注释改掉。
  代码不改。

### 10.2 VaultService 退出

删：csproj 的四个 NotificationCenter / SignalR 包（`:61-70`）、`DependsOn` 四项（`:116-123`）、hub 鉴权（`:261-281`）、
`NotificationCenter` 连接串映射（`:605-609`）、`ConfigureNotificationCenter()`（`VaultServiceDbContext.cs:48`）、测试模块
对应项。新增一个 drop 四张 `Notif*` 表的迁移（原迁移 `20261009022215` 同时建了 `Vault*` 表，不能回滚）。

加：Remote、`DefinitionStore.EntityFrameworkCore`，`NotificationCenter` 连接串映射到 NotificationService 的库。
`Dignite.Abp.Notifications`（Core）经 Extract.Application 传递进来，不用显式引用；`NotificationCenterVersion` 属性改名或
移到 NotificationService，注释改成「与 Extract 依赖的 `Dignite.Abp.Notifications` 同版本」。

### 10.3 端到端验收

Vault 上传文档 → 文档就绪 → Angular 铃铛经 NotificationService 响；订阅设置页能列出 "Documents" 组的四条定义，显示名
已本地化；VaultService 的库里没有 `Notif*` 表；RabbitMQ 里 `Cloud_VaultService` 队列不再收到 `NotificationDeliveryRequested`。

## 11. vault-extract 与 campus

- **vault-extract 代码不改**，升依赖版本；`DocumentNotificationDispatcher.cs:52-82` 里「收件箱和发布方共用连接、同一事务」
  的注释改成「远程发布时该事务只含 outbox 写入」；`docs/en/egress/operator-notifications.md`、`deployment.md`、
  `CHANGELOG.md` 补「拆分部署：宿主装 Remote 而不是 NotificationCenter」。测试项目若依赖本地发布器，加 Distribution。
- **campus.support 不改**。campus 单体宿主加 Distribution，`Push.Identity` 等照旧，行为不变。
- Campus 进 cloud 是另一个设计：`Campus_Mobile` 客户端和 `Campus` scope、职员查找改走集成服务、会话集成服务（§8）、
  public 网关给手机端的路由。本稿不堵它的路。

## 12. 删除与修改清单（abp-modules/notifications）

**新增**

- Abstractions：`NotificationPublishRequestedEto`。
- `Dignite.Abp.Notifications.Distribution`：从 Core 迁入 §4 列出的实现；`NotificationPublishRequestedHandler`；
  `[BackgroundJobName("Dignite.Abp.Notifications.Distribute")]`。
- `Dignite.Abp.Notifications.Remote`：`RemoteNotificationPublisher`、模块类、与 Distribution 互斥的启动校验。
- `Dignite.Abp.Notifications.DefinitionStore` + `.EntityFrameworkCore`：§6 的全部部件。
- `Dignite.NotificationCenter.Installer` 的 `.abpmdl` 补新包（如果 Studio 安装需要）。

**修改**

- Core：`NotificationInfo.Data` → `DataJson`；`INotificationDefinitionManager` 异步化及其调用方；
  `NotificationDefinitionStartupService` 只留路由名对账；`AbpNotificationsModule` 不再注册 handler 和作业。
- Distribution 内：发布器的「在线 / 入队」判断提成共用方法；分发器接受预解析的 `Channels`；
  `NotificationStore.InsertNotificationAsync` 加 Id 预检（在 NotificationCenter.Domain）。
- `Notifications.Identity`：§7。csproj 去掉 `Volo.Abp.Identity.Domain`。
- README：Install 两段改成三种组合（单体 / 发布方 / 通知服务）；新增 "Split deployment" 一节；Packages 表加四个包；
  Architecture 图加 `NotificationPublishRequestedEto`。
- CLAUDE.md（notifications）：Structure 表加新包；"Two operation modes" 加第三种「远程发布」。
- `notifications-conventions` skill：补「契约留 Core，实现进 Distribution」和 `DataJson` 规则。
- `host/`：装 Distribution。

**不改**

- `NotificationDeliveryRequestedEto`、所有通知器、`NotificationCenter.Web`、Angular 库、`Push.Identity`、`Emailing.Identity`。

## 13. 测试要点

| 项 | 断言 |
|---|---|
| ETO 往返 | `NotificationPublishRequestedEto` 用默认 STJ 往返，`DataJson` 内含判别符而非 CLR 名（照 ETO 现有测试） |
| Remote 发布器 | 定义不存在抛；渠道由本地 resolver 解析并写入 ETO；有工作单元时事件进 outbox 而非直发；一条通知恰好一条事件，与收件人数量无关 |
| 接收处理器 | 租户来自 ETO；小扇出在线、大扇出入作业；`Channels` 非空时 resolver 不被调用（`Received(0)`）；同一 `NotificationId` 处理两次只有一行 `Notification` |
| 负载透传 | 通知服务未注册的判别符：存储逐字节保留；收件箱 REST 返回 `Dignite.Unsupported` 且 `rawJson` 与原文一致；SignalR 原样推送；Email 跳过并记 Debug |
| 包边界 | 只装 Core + Remote 的宿主：`JobQueueManager` 没有分发作业的消费者；没有 `NotificationDeliveryRequested` 的处理器 |
| 互斥 | Distribution + Remote 同装 → 启动失败 |
| DefinitionStore | 两个应用名各自保存，第二个不删第一个的记录；`DeletedNotifications` 删对应记录；hash 相同不写库；动态 store 在 stamp 变化后重读；静态优先于动态 |
| 本地化 | 按名字的显示名经外部存储解析（测试里用内存 `IExternalLocalizationStore`） |
| Identity | 拼出的 principal 含 UserId、全部 Role、TenantId；租户上下文正确；`IUserRoleFinder` 用 NSubstitute |
| 无状态模式 | Distribution + Null store 行为与今天完全一致（现有测试不改） |

## 14. 实现顺序

1. **abp-modules/notifications**：拆包 → `DataJson` → Remote + ETO + 处理器 → DefinitionStore → `Notifications.Identity` →
   文档。一个 PR 一个主题，最后发 `10.0.0-rc.23`。
2. **cloud**：NotificationService 模板补齐并提交（可与第 1 步并行，不依赖上游）→ 升到 rc.23 → 接线 → VaultService 退出 →
   §10.3 验收 → PR。
3. **vault-extract、campus**：升版本、改注释和文档、单体宿主加 Distribution，各自发版。

步骤 2 不等步骤 3：§4 的兼容性约束保证 rc.22 编译的 Extract 程序集能跑在 rc.23 上。

## 15. 评审中否决的方案

| 方案 | 否决理由 |
|---|---|
| 定义目录用启动时发事件同步 | 自造一套需要处理顺序、整体替换、首次到达的机制；ABP 对权限 / 功能 / 设置用的都是共享表 + 静态 Saver + 动态 Store，cloud 已在用 |
| 不拆包，用开关让 Core 在发布方不注册作业和处理器 | ABP 按约定自动注册 `IBackgroundJob<>`，反向摘除要改 Options 字典，是绕路；拆包让依赖方向说清楚谁托管什么 |
| 远程发布走 HTTP 集成服务 | 同步、不原子、cloud 没有服务间 client_credentials 的先例；分布式事件 + outbox 是模板已有套路，Chat (Pro) 也这样做 |
| 远程 `INotificationStore`（发布方分发，store 走 HTTP） | 每个收件人一次 HTTP，事务性全无，作业队列共享问题原样保留 |
| 通知服务映射 Identity 库 | cloud 里只有 AuthServer 和 IdentityService 碰 Identity 库，不为一个包打破边界 |
| 事件携带权限 / 功能门槛，通知服务不存定义 | 收件箱和订阅页仍需定义；既然目录必须有，门槛从目录取，事件保持最小 |
| 渠道由通知服务解析 | 模块默认路由写在业务模块的 `ConfigureServices` 里，通知服务没有；改成把路由存进目录会把渠道重新变成定义的属性，与 channel-routing 设计相悖 |
| 自定义 `IdentitySessionEto` 做强类型映射 | 用户核实 ABP 没有这个类型，不自造；且实体事件覆盖不了批量清理（§8） |
| `EntityDeletedEto<EntityEto>` 处理会话结束 | 批量清理不发事件（§8） |

## 16. 实施时要先核实的事实

- LanguageService 库里 `VaultExtract` 资源是否真的被写入（机制核实了，数据没有；开发库未运行）。
- `HttpClientUserRoleFinder` 调集成服务是否需要令牌：Administration 的先例只配了 BaseUrl。
- Shared 用户策略下，经集成服务按租户查角色能否查到。
- cloud 的 access token 是否带 `session_id`（代码推断应有；Campus 阶段才用到）。
- `NotificationDistributionJobArgs` 经 RabbitMQ 作业序列化时 `DataJson` 的处理（改字段后重新验证）。
- ABP Studio 安装路径：`Dignite.NotificationCenter.Installer` 的 `.abpmdl` 是否要列新包。
