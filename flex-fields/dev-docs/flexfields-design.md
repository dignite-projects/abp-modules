# FlexFields 模块设计（已落码）

> **状态：** 已落码，自 `10.0.0-rc.5` 起发布。本稿记录设计取舍与否决方案；与代码不一致之处以代码和用户文档
> [FlexFields](../../docs/en/flex-fields/index.md) 为准。

> `Dignite.Abp.DynamicForms`（现居 `dignite-abp` 仓）将拆分独立、改名 **FlexFields**，迁入本仓 `abp-modules`。
> 本稿是多轮设计讨论的整理，**是起点不是终点**：落码后按实际代码回头调整。第 7 节列了仍未决的疑问。

## 1. 命名与定位
- **改名 FlexFields**（灵活字段；命名空间 `Dignite.Abp.FlexFields`）。主角 = 字段。
- 否掉的名：`DynamicForms`（命名了 UI/表单而非领域动作）、`DynamicObjectExtending`（会误示与 ABP `ObjectExtending` 有血缘，实则两模块无关）。命名原则：**命名领域概念，不命名 UI 制品**。
- **定位 = `Volo.Abp.Users` 式共享、非权威内核**（不是 `Volo.Abp.Identity` 那种可运行应用模块）。它是**约束性实现**：只给约束/接口/泛型/机制，**不含领域模型**；每个下游（CMS、将来 Commerce）是自己字段的权威。
- 类比：`IUserData`（接口）↔ `IHasFlexFields` + `IFlexField`；`IdentityUser`（下游具体实体）↔ CMS 的 `Field`/`EntryType`。**内核永不定义"具体那个"。**
- 包型：`.Abstractions` / `.Domain.Shared` / `.Domain` / `.EntityFrameworkCore` / `.MongoDB`。因内核无 app service，属 "core 模块"，不需要 `.Application`/`.HttpApi` 层。Angular `dynamic-form`（→ `flex-fields/angular`）是独立前端轴。

## 2. 概念框架
- 本质 = 运行期、数据定义的 **Adaptive Object-Model / EAV**。EAV 本体只 = 值存储；定义目录 / 规则 / 渲染 / 索引都是围绕它的独立层。
- **但内核只给机制，领域模型全在下游**：没有 `FlexFieldDefinition`/`FlexFieldSet`/`FieldGroup` 等内核聚合；定义、分组、类型绑定、页签、宿主实体全部下游实现（CMS 的 `Field`/`FieldGroup`/`EntryType`/`FieldTabs` **原地不动**）。**不存在"从 CMS 抽取平台"。**
- 唯一的缝 = `IFlexFieldProvider`（下游实现，喂"物化的字段"；现有 `CustomizableObject.GetCustomizeFields()` 已是雏形）。
- **落库边界：内核给"支持"不给"拥有"。** 内核 EF/Mongo 层出实体形状 + `IFlexFieldsDbContext` + `ConfigureFlexFields()` 模型扩展 + Store/Query/Migrator 基类（对标 Users 的 `AbpUsersDbContextModelCreatingExtensions` / `EfCoreUserRepositoryBase`）；**下游拥有 DbContext + 物理表 + 迁移**，内核无具体 DbContext、无落库领域表。运行期载体（UserData 式 `FlexField`，不落表）不在此约束内，需要随时可加。

## 3. 命名映射（已定，原则=命名领域不命名 UI）
| 旧 | 新 |
|---|---|
| `Dignite.Abp.DynamicForms` | `Dignite.Abp.FlexFields` |
| `FormField` | `IFlexField`（运行期形状，**只接口**，下游类型实现） |
| `IFormControl` / `FormControlBase` | `IFieldType` / `FieldTypeBase`（C# 侧无渲染 → 是"字段类型"非"控件"） |
| `FormControlSelector` | `FieldTypeResolver` |
| `FormConfiguration*` | `FieldConfiguration*` |
| `FormControlValidateArgs` | `FieldValidationArgs`（瘦成只装输入） |
| `TextEditFormControl` … | `TextFieldType` / `NumberFieldType` / `DateTimeFieldType` / `SelectFieldType` / `BooleanFieldType` / `TreeFieldType`（去 Blazorise 的 `Edit` 后缀） |

**注：持久化的 `FormControlName` 值（如 `"TextEdit"`）、`FormConfiguration` 键字符串属"数据"**，改名归存储迁移那批，不随类名动（或保留为不透明 key）。

**落码后更新（2026-08-17）：** 上述"不透明 key"的搁置到此结束——`ControlName`/`FieldConfigurationDictionary` 键前缀已改为与类名一致（`TextEdit`→`Text`、`NumericEdit`→`Number`、`DateEdit`→`DateTime`、`Switch`→`Boolean`、`TreeView`→`Tree`；`Select` 本就一致），Angular 侧同步。这是一次刻意接受的破坏性变更：任何已按旧 key 落库的字段数据在升级后需要下游自行做数据迁移，内核本身无 DbContext、也不提供该迁移。细节见 [`flex-fields/CLAUDE.md`](../CLAUDE.md) 的"Registration keys and configuration keys are persisted data"一节。

## 4. 值存储与查询（Y 方案 · 已定）
- **权威存储** = 宿主上一个"便宜读的值袋子"：自建 `IHasFlexFields` + 专属 `FlexFields` 字典/列，**不用 ABP `ExtraProperties`**。理由 = 与公共 ExtraProperties 大袋子**隔离**（防撞名）+ **平台自持**；`IHasExtraProperties`/`ExtraPropertyDictionary` 属 `Volo.Abp.Data`（非 ObjectExtending），故这是隔离/管道取舍非血缘。代价：自补 EF JSON 转换器 / Mongo 内嵌 / 自己的 `FlexibleEntityDto` / AutoMapper（照抄 ABP）。
- **派生查询索引** = 类型化 `FlexFieldIndex` 表（Salesforce `MT_Data` + `MT_Indexes` 思路）。已定：①同库多宿主**共表**（`EntityType` 判别键）+ ②写时**同 UoW** 同步。整条读走袋子；按字段查走索引 → EntityId → 过滤宿主。
- **provider 可换**：EF = 类型化 pivot 表；Mongo = 内嵌值 + 原生路径索引（写时近乎零同步）。别把关系型表焊进抽象层。
- **两类回填**：`Searchable` 翻开（值类型不变）→ 仅 `RebuildAsync`（Mongo ≈ createIndex 近免费；EF 真回填）；控件改类型（值类型变）→ 也是 `RebuildAsync`，因为 `GetSearchableValues` 每次都从袋子里的原始值重新解释，落码后发现不需要单独的值迁移器。
- **但袋子 key 变了是第三类，`RebuildAsync` 治不了**（落码后补）：字段定义改名 / 删除时，key 本身变了，重推导只会忠实地推导出"什么都没有"，还会把原有索引行删掉。这一类走 `IFlexFieldValueMigrator<TEntity>`，只重写袋子、不碰索引 —— 纯改名对索引无害（索引行以 `FieldId` + 值为键）。内核不感知改名（`IFlexField.Name` 只读、实体归下游），由下游改完名后自己调，每个宿主类型调一次。
- **注意这一类与索引不同，`provider 可换` 不适用**：`IFlexFieldIndexManager` 要两套实现，是因为派生状态的**种类**就不同（EF 透视表 vs Mongo 原生路径索引）；而"把 key 在袋子里挪个位置"跟谁存无关，分页走 `IFlexFieldProvider`、落盘走 ABP 仓储，两头都已经是 provider 中立的。所以 `FlexFieldValueMigrator<TEntity>` 是 `.Domain` 里的**唯一一套实现**（开放泛型注册，对照 `FlexFieldValidator<TEntity>`），`.EntityFrameworkCore` / `.MongoDB` 都不出对应类型，下游也不需要写子类。

## 5. 总接口清单（按层，落码后校准 —— 与代码一致）

对照 ABP 自己的 `Volo.Abp.Users`：`IUserData`（DDD-free 载体）在 `.Abstractions`，`IUser : IAggregateRoot<Guid>`（Entity 契约）在 `.Domain`，两者靠 `.Domain` 里的 `ToAbpUserData()` 单向桥接、互不继承。FlexFields 按同一套收敛。

- **`.Domain.Shared`**：仅 `FlexFieldConsts`（列长度等）。刻意不含 `FlexFieldValueType`/`FlexFieldDictionary`——那些是 Abstractions 自足所需的类型词汇，`.Abstractions` 与 `.Domain.Shared` 互不引用，只由 `.Domain` 同时依赖两者。
- **`.Abstractions`**（无 DDD 依赖，**不含任何 Entity 契约、不含任何持久化形状**）：
  - 数据载体：`IFlexFieldData` / `FlexFieldData`（对照 `IUserData`/`UserData`）
  - 字段生命周期 Eto：`FlexFieldRenamedEto` / `FlexFieldDeletedEto`（带 `[EventName]` + `IMultiTenant`，对照 `UserEto`；依据即硬规则 1）。**内核既不发布也不订阅**——发布方必须是下游（`IFlexField.Name` 只读、实体归下游），订阅方也只能是下游（迁移器按宿主类型泛型，内核无宿主类型注册表）。只为"字段定义与宿主分属不同模块"这一种场景存在；同一下游内直接调 `IFlexFieldValueMigrator` 更简单也更安全（见下条）
  - 值袋：`IHasFlexFields`、`FlexFieldDictionary`
  - 运行期载体：`FlexFieldValue`（`Field` 属性类型是 `IFlexFieldData`，不是任何 Entity 接口）
  - 字段类型：`IFieldType`（`GetSearchableValues` 返回 `IEnumerable<object>`，provider 中立）、`FieldTypeBase`、内置类型（`TextFieldType`…）、`FieldTypeResolver`
  - 查询词汇：`FlexFieldValueType`、`FlexFieldQueryOperator`、`FlexFieldQueryCondition`
  - `FieldConfigurationDictionary`、`FieldConfigurationBase`、`FieldValidationArgs`
- **`.Domain`**（依赖 `.Abstractions` + `.Domain.Shared` + `Volo.Abp.Ddd.Domain`）：
  - Entity 契约：`IFlexField : IAggregateRoot<Guid>`——下游的字段定义实体（如 CMS 的 `Field`）实现它
  - 桥接：`FlexFieldExtensions.ToFlexFieldData()`，`IFlexField -> FlexFieldData` 单向
  - 仓储：`IFlexFieldRepository<TField>`（字段定义轴，内核自己从不调用，纯为下游便利）
  - 宿主实体轴的缝：`IFlexFieldProvider<TEntity>`（内核唯一的信息入口）、`IFlexFieldValidator<TEntity>`、`IFlexFieldIndexManager<TEntity>`、`IFlexFieldQueryExecutor<TEntity>`、`IFlexFieldValueMigrator<TEntity>`（改名/删字段时重写袋子 key）
  - **provider 中立的默认实现**（开放泛型注册在 `AbpFlexFieldsDomainModule`，下游零代码）：`FlexFieldValidator<TEntity>`、`FlexFieldValueMigrator<TEntity>`。其余几个缝按 provider 分实现或由下游实现
  - **内核无具体 `FlexField` 实体**（下游类型实现 `IFlexField`）
- **`.EntityFrameworkCore`**（支持不拥有；依赖 `.Domain`）：
  - `FlexFieldIndexValue`——**relational-only**，五个类型化槽位（String/Number/DateTime/Boolean/Guid）+ `Create(FlexFieldValueType, object)` 工厂。只属于这一层：它是关系型透视表的行值形状，`.MongoDb` 不会有对应类型
  - `IFlexFieldIndex`、`FlexFieldIndexBase<TEntity>`（下游每宿主类型一张索引表）
  - `FlexFieldsDbContextModelCreatingExtensions`：`ConfigureFlexFieldsProperty<TEntity>()` / `ConfigureFlexField<TField>()` / `ConfigureFlexFieldIndex<TIndex>()`
  - `EfCoreFlexFieldIndexManagerBase<TDbContext,TEntity,TIndex>`：类型化在这一层发生——从 `IFieldType.GetSearchableValues` 拿到原始值后，配上 `IndexValueType` 转成 `FlexFieldIndexValue`
  - `EfCoreFlexFieldRepositoryBase<TDbContext,TField>`：`IFlexFieldRepository<TField>` 的 EF 实现
  - **无** `IFlexFieldValueMigrator` 的 EF 实现——它 provider 中立，唯一实现在 `.Domain`
  - 无具体 DbContext、无 DbSet
- **`.MongoDB`**（留待后续 session）：同构但**没有 `FlexFieldIndexValue` 或任何透视表实体**——直接在 `FlexFieldDictionary` 上查询、建索引，写时近乎零同步。
- **下游（CMS）**：`Field`/`FieldGroup`/`EntryType`/`FieldTabs` 留下游；`Field : IFlexField`；`Entry : IHasFlexFields`；实现 `IFlexFieldProvider<Entry>`（内部把 `Field` 经 `ToFlexFieldData()` 转成载体）；DbContext 调三个 `ConfigureFlexField*()` 扩展，拥有物理表 + 迁移。

**两条硬规则：**
1. **`.Abstractions` 不出现任何 Entity 契约。** Entity 相关的（`IFlexField`、`IFlexFieldRepository`）一律归 `.Domain`；Eto 这类纯数据载体则允许待在 `.Abstractions`（依据即 `Volo.Abp.Users.Abstractions` 的 `UserEto : IUserData`）。
2. **`FlexFieldIndexValue` 是 EF 独有的。** 它是关系型透视表的行值形状。`.MongoDb` 不会有对应类型，Mongo 直接在 `FlexFieldDictionary` 上查询和建索引。这正是两个 provider 分开的设计前提。

## 6. 关键类型签名（对照落地代码，非草案）

`.Abstractions`：

```csharp
// —— 运行期字段形状：DDD-free 载体，对照 IUserData ——
public interface IFlexFieldData {
    Guid Id { get; }
    string Name { get; }                          // 唯一名 = 值袋子 key
    string DisplayName { get; }
    string? Description { get; }
    string FieldTypeName { get; }                 // → IFieldType.Name
    FieldConfigurationDictionary Configuration { get; }
}
// FlexFieldData : IFlexFieldData 是唯一实现，全 { get; set; }，对照 UserData

public enum FlexFieldValueType { String, Number, DateTime, Boolean, Guid }

// —— 字段类型：拆解与类型化分家 ——
public interface IFieldType {
    string Name { get; }                          // 注册键，如 "Text"
    string DisplayName { get; }
    FlexFieldValueType? IndexValueType { get; }   // null = 不可索引（RichText/Matrix）
    FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary configuration);
    IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args);   // 返回错误，不改共享 list
    IEnumerable<object> GetSearchableValues(FlexFieldValue field);       // 只拆解，不打类型标签；EF 侧配 IndexValueType 转成 FlexFieldIndexValue
}

// —— 运行期载体：定义 + 用法 + 值，FlexFieldData 拿来实例化不拿来继承 ——
public class FlexFieldValue {
    public IFlexFieldData Field { get; }
    public bool Required { get; }
    public bool Searchable { get; }
    public object? Value { get; }
}

// —— 查询条件自带 ValueType → 查询侧自描述，不回查定义 ——
public class FlexFieldQueryCondition {
    public Guid FieldId { get; set; }
    public FlexFieldQueryOperator Operator { get; set; }
    public string Value { get; set; }             // In 时逗号分隔
    public FlexFieldValueType ValueType { get; set; }
}
// FieldValidationArgs 只装 { FlexFieldValue Field }（将来跨字段校验加 siblings）
```

`.Domain`（依赖 `Volo.Abp.Ddd.Domain`）：

```csharp
// —— 字段定义：Entity 契约，下游自己的实体实现（如 CMS 的 Field）——
public interface IFlexField : IAggregateRoot<Guid> {
    string Name { get; }
    string DisplayName { get; }
    string? Description { get; }
    string FieldTypeName { get; }
    FieldConfigurationDictionary Configuration { get; }
}

// —— 单向桥：IFlexField -> FlexFieldData，对照 AbpUserExtensions.ToAbpUserData() ——
public static class FlexFieldExtensions {
    public static FlexFieldData ToFlexFieldData(this IFlexField field);
}

// —— 下游"宿主实体 → 字段"的缝（GetCustomizeFields 的正式化），内核唯一的信息入口 ——
public interface IFlexFieldProvider<TEntity> where TEntity : IHasFlexFields {
    Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(TEntity entity, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetPagedEntitiesAsync(int skipCount, int maxResultCount, CancellationToken ct = default);
}

// —— 字段仓储：字段定义轴，纯为下游便利，内核自己从不调用 ——
public interface IFlexFieldRepository<TField> : IBasicRepository<TField, Guid> where TField : class, IFlexField {
    Task<TField?> FindByNameAsync(string name, CancellationToken ct = default);
    Task<List<TField>> GetListAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken ct = default);
}
```

内嵌决定（相对早期草案的调整，均已落码）：① `Project` 拆成 `GetSearchableValues`（只拆解）+ EF 侧的 `FlexFieldIndexValue.Create`（配类型），不再合一；② `IFlexField` 落到 `.Domain` 并继承 `IAggregateRoot<Guid>`，`.Abstractions` 改用 DDD-free 的 `IFlexFieldData`；③ `IFlexFieldProvider` 收泛型宿主实体 `TEntity`，不是非泛型的 `IHasFlexFields`；④ `QueryingByField` 定稿为 `FlexFieldQueryCondition`；⑤ 新增 `IFlexFieldRepository<TField>`，草案阶段未预见。

## 7. 待定 / 落码后校准
- 用户对本设计仍存个别疑问，**约定落码后按实际代码回调**。
- 未细化：多宿主的 provider 分派；唯一约束（对应 `MT_Unique_Indexes`）；富页签布局（完全归下游 UI）。
  - 改名的**唯一性校验**仍只有 `IFlexFieldRepository.NameExistsAsync` 这一个建议性方法，内核不强制、不建索引（`ConfigureFlexField<TField>()` 刻意不加唯一索引），归下游。袋子侧的**改完之后**已由 `IFlexFieldValueMigrator<TEntity>` 兜住。
  - 多宿主分派的具体体现：`IFlexFieldValueMigrator<TEntity>` 和 `IFlexFieldIndexManager<TEntity>` 一样是每宿主类型一个，一次改名下游要按挂载列表逐个调用 —— 那份列表内核不掌握。**这也正是内核不出事件 handler 的原因**：`FlexFieldRenamedEto` 的 handler 拿不到该 resolve 哪个 `IFlexFieldValueMigrator<TEntity>`。真要让"一次改名自动覆盖所有宿主"，缺的是**宿主类型注册表**（`FlexFieldsOptions.HostEntityTypes` 之类）而不是 ETO；有了它一个非泛型的扇出入口就够，连事件总线都不需要。
  - 走事件总线的两个代价（已写进 Eto 的 XML 注释）：① `PublishAsync` 默认 `onUnitOfWorkComplete: true`，改名提交到 handler 执行之间有窗口，期间任何 `SynchronizeAsync` 都会删掉该字段的索引行 —— 把确定的时序退化成竞态；② handler 的异常被事件总线收集、不回到发布方，`RenameField` 那个"目标 key 已占用就抛"的护栏降级成一行日志。
  - 若将来要做在线安全的改名，正确姿势是**三步幂等**：先把值复制到新 key → 再翻 `Name` → 最后删旧 key。全过程总有一个 key 与当前 `Name` 匹配，`SynchronizeAsync` 任何时刻插进来都不丢数据。
- 离"成熟 FlexFields"仍缺：联动/依赖字段、跨字段校验（`FieldValidationArgs` 已留 siblings 口）、关系/引用字段（索引表 `GuidValue` 已预留）、字段级权限。

## 8. 实施顺序
`rename（A 组，编译器兜底，先做拿干净基座） → 存储机制（Y） → 外挂 field-type（CkEditor/FileExplorer）与 Angular → CMS 切换（另一仓大工程）`。内核无 DDD 可设计（下游 DDD = CMS 现有模型，只改成引用内核契约）。

## 9. 复合字段类型（Matrix / Table，2026-09 从 Dignite.Site 迁入）

`MatrixFieldType`（多种命名块类型的多态重复器）与 `TableFieldType`（单一列结构的同构表格）原生于 Site 仓的 `Dignite.FlexFields.Site`，现作为**内置类型**迁入 `.Abstractions`，与 Text/Number/DateTime/Select/Boolean/Tree 并列。线格式（注册键 `Matrix`/`Table`、配置键 `Matrix.BlockTypes`/`Table.Columns`、值的 camelCase `{blockTypeName, values}`/`{values}`）**一字未改**，属第 3 节"持久化的键即数据"那条规矩的直接应用——已落库的字段升级后照常可读。

**这两类的本质：配置里嵌套字段定义。** 对第 6 节的 `IFieldType` 来说配置是一只不透明的袋子；复合类型第一次让"一个字段的配置里还有字段"成为需要被提问的事实。由此产生两个共享契约与一个共享形状：

- **`ICompositeFieldType`（内核，非外挂包）**：`GetInlineFields(configuration)`，扁平返回。**放内核的理由**是宿主必须能在不认识 `MatrixFieldType`/`TableFieldType` 具体类型的前提下问出"这个类型是不是复合的、它里面声明了什么"——保存字段定义时的深度校验正是这样一个调用点，而定义保存路径归下游、不归字段类型。放进某个外挂包则相反：宿主要么依赖那个包（内核的"约束不拥有"就破了），要么按类型名硬编码（正是这个接口要消灭的耦合）。第三种复合类型（下游自己加的）只要实现它，深度上限与客户端类型选择器自动覆盖，无需注册表。
- **`INormalizesValue`（内核）**：`Normalize(value)` 返回该类型唯一的规范写入形状。
- **`InlineFieldDefinition`（内核）**：一个内联字段——Matrix 某块类型的子字段，或 Table 的一列。刻意**不是** `FlexFieldData`：它带 `Required`，而第 6 节里"必填"属于字段的**用法**（`FlexFieldValue.Required`）不属于定义；内联字段没有独立的用法记录来承载这个标志，只能落在定义上。

**规范化为什么与校验分家。** `Validate` 回答"这个值可不可接受"，只返回错误、从不返回解析结果；于是一个键大小写不合规范但结构完好的值能顺利通过校验，然后被宿主**原样**写进袋子，对下游每一个按 camelCase 读的读者（尤其是 Angular 控件）都是静默不可读。把二者合并意味着今后每一次 `Validate` 覆写都得兼职做值变换——所以是两个方法、两次调用，宿主在写入前先 `Normalize` 再 `Validate`（见 demo 的 `ProductAppService`）。

**嵌套为什么在写时封顶、为什么是 3。** 复合类型的配置里嵌着完整的字段定义，而那些定义又能指向复合类型——字段定义因此是一棵深度无界的树。走这棵树的每一个读者都没有自带深度护栏：`InlineFieldValidator` 沿 `Validate` 递归，`.Web` 的 `Matrix.cshtml`/`Table.cshtml` 沿 `flex-field-view` 分发递归，Angular 的设计器与值编辑器沿 `ff-flex-field-config`/`ff-flex-field-control` 递归。它们都不会死循环（配置是内嵌数据不是引用，不成环），但都付出与深度成正比的栈与渲染开销，且在深处都不再可用。**在唯一的写入口封一次，胜过给每个读者各加一道护栏**——这与第 4 节"写时同步索引"是同一种取舍：把代价挪到低频的写侧。`MaxDepth = 3` 的含义是顶层字段可以是复合的、它的子字段/列也可以是，再往下一层必须是标量；`Table > Matrix > Table`（内层 Table 还带列）为 4，拒绝。`ExceedsMaxDepth` 自带递归预算，因为它是第一个走未经审核的客户端配置的方法——没有预算它自己就是它要防的那个栈溢出。内核只提供度量，**执行在宿主**（内核没有字段定义保存路径可挂，见第 2 节）；Angular 侧的 `MAX_COMPOSITE_NESTING_DEPTH` 是提前置灰的礼貌镜像，权威始终是服务端这个常量。

**两者都不可索引**（`IndexValueType == null`）：值是复合对象的列表，不是标量也不是标量列表，第 4 节的类型化索引槽位无从落脚——第 6 节 `IndexValueType` 的注释本来就把 Matrix 举为这一情形的典型。因此两者都不出 `Views/Shared/FlexFields/Search/` 分部视图，也不允许被标 `Searchable`。

**否掉的方案：**
- **`IFieldType` 上加一个 `IsComposite` 布尔**。凡是关心"是不是复合"的调用方，紧接着都要走内联字段（量深度、递归校验、描述结构）；一个裸布尔只会让每个调用方各自按具体类型 switch 一次去拿那些字段——正是该接口要消除的耦合。
- **把两个契约放进外挂包**（`FlexFields.Composite` 之类，对照 `FlexFields.FileExplorer` 的先例）。外挂包的判据是"有没有自己的领域依赖"：FileExplorer 依赖 `Dignite.FileExplorer`，所以必须外挂；Matrix/Table 只依赖内核已有的词汇（`FieldConfigurationDictionary`、`FlexFieldDictionary`、`FieldTypeBase`），没有任何外部依赖可隔离。而契约一旦外挂，上面那条"宿主必须能不认识具体类型就提问"的路径立刻断掉。
- **在 `Validate` 内部顺手把值改写掉**（省掉 `INormalizesValue`）。见上文"分家"一条：会把"返回错误"这个契约悄悄变成"返回错误并且可能改你的值"。

## 10. 配置键声明与写时校验（2026-11，10.7.x）

**问题。** `FieldConfigurationDictionary` 是 `Dictionary<string, object>`，什么键都收：模型把 `Text.Mode` 写成 `Text.Multiline`，被原样存下、没有任何读者认它，字段表现得像从没配过——没有一句报错。更糟的是值的类型也不查：`Select.Options` 写成字符串数组能存进去，而读它的 `GetConfiguration<List<SelectListItem>>` 在第一次读字段时才抛 `JsonException`。内核没有字段定义保存路径（第 2 节），能做的只有两件事：让字段类型**说出**自己认哪些键，再给宿主一个**能调用的检查**。

**定案。**

- **声明在 `IFieldType.ConfigurationKeys`**，默认接口成员，返回 `IReadOnlyList<FieldConfigurationKey>?`。**`null` = 没声明 = 不检查**；**空列表 = 声明"不接收任何配置" = 所有键都拒**。两者必须分开：第三方类型是在这个成员出现之前编译的，对它们"没说"绝不能解释成"全部拒绝"。默认接口成员让只实现了 `IFieldType`（没继承 `FieldTypeBase`）的类型既不用改、也不会加载失败。
- **`FieldConfigurationKey`** 带键名、值的种类（`String/Integer/Number/Boolean/DateTime/Guid/Enum/Array/Object`）、可选的 `ValueType`（枚举类型，或数组/对象要反序列化成的 CLR 类型）、默认值、一句描述、`Required`、`EmptyStringIsUnset`（空字符串视为未设置；最初叫 `Nullable`，与 `System.Nullable` 重名且会让人以为 null 才需要它，null 本来就总是"未设置"，发版前改名）。默认值**从配置类读**（`new TextConfiguration().Mode`），不在声明里重抄一遍——配置类的 getter 才是字段真正的默认值。`Shape`（给人和 AI 看的值形状，如 `[{ "text": string, ... }]`）**由 `ValueKind`+`ValueType` 推出来**，不手写——手写的形状迟早和校验器接受的东西漂开。
- **`IFieldConfigurationValidator`** 在 `.Abstractions`（字段类型在那儿，服务要读 `FlexFieldsResource` 本地化）。`Validate` 返回 `ValidationResult` 列表（成员名 `Configuration[key]`，沿用 MVC 对字典项的命名），`Check` 把它们打包成 `AbpValidationException`。选**校验异常而不是 `BusinessException`**：MCP 的错误过滤器把前者归为 `validation` 并把 `ValidationResult` 逐条带出（"改掉 fields 里的参数再试"），后者归为 `conflict`；HTTP 上也是 400 而不是 403。所有问题一次报完，不是一次一个。
- **内核不调用它，宿主调用**，与 `CompositeFieldNesting` 完全同构（第 9 节）：调用点是宿主接收客户端字段定义的 create / update。demo 的 `ProductFieldAppService` 是示范，同时把声明的键放进它的字段类型列表。
- **只管写，不管读。** 校验在读路径上不存在；历史上存下的配置——键没声明、值是别的类型——照旧能读。这条与"持久化的键即数据"（第 3 节）同源。
- **更新要传 `previous`。** 只改显示名的客户端会把存着的配置原样发回来，旧定义里没声明的键、别的类型的值就会让这次无关的改动被拒。所以 `Validate/Check` 带可选的 `previous`（存着的配置）：值和之前一模一样的键、和之前一模一样（名字+类型+配置）的内联字段不再检查，客户端新增或改动的才查；必填键"之前就缺"也不算这次的错。比较用的归一化（`JsonElement`、装箱数字、`List<object>` 都先转成紧凑 JSON）在内核里，宿主不必各写一遍。内联字段按"整条定义是否原样存在"判断，不按名字配对（不同块类型里字段可以重名）；改了一条就整条重查。字段类型变了时不传 `previous`，整份都是新的。
- **复合类型的内联字段不被外层的错误挡住，也不要求外层声明了键。** 最初的写法是外层键有错就不往内联走（怕 `GetInlineFields` 读不出来），结果既违背"所有问题一次报完"，也让没声明键的复合类型整个绕过内联检查。现在照常走：读不出来时，若外层键已经报过就不再重复，否则报一条"配置格式不对"（存下去只是把失败推到第一次读）；`previous` 同样读不出来时是旧账，不算这次的。内联字段的类型名没注册也报（见下"否掉"里被推翻的那条）。
- **接受的集合 = 读取器能读的集合。** `GetConfiguration<T>` 经字符串转换，Angular 的 `Number` 编辑器输入框是 `type="text"`，所以它存下来的 `Number.Min` 是 `"10"`；`DateTime.Min` 是 `"2026-01"`，清空是 `""`。拒掉这些会让内核自己的编辑器存不了盘。反过来读取器**读不了**的才拒：枚举只按整数读（`"MultipleLine"` 会让 `JsonSerializer.Deserialize<TextMode>` 抛），非 `EmptyStringIsUnset` 键的空字符串读取时抛。校验器比读取器略严的地方（字符串键给数字、越界的枚举整数，读取器读得出来）是有意的：读得出来但没有意义的，不放进去。
- **声明复述 `*ConfigurationNames` 常量，不发明名字**，且有反射测试守着两者相等。Angular 编辑器写、服务端没命名的键（`Number.Placeholder`，Angular 一直在写在读，服务端没有对应常量）此时会被校验器拒掉，所以补了常量和 `NumberConfiguration.Placeholder`——这是一个已落库的键，不是新发明的。

**否掉的方案：**

- **在 `FieldConfigurationBase` 里反射配置类的属性来推导键。** 看起来零重复，但键的默认值（`Text.CharLimit` 随 `Mode` 变）、值的种类、`EmptyStringIsUnset` 和描述都不在属性里；而且第三方类型也要能声明，反射推导会把"没声明"变成"声明了一个推导出的集合"。
- **单独的 `IDescribesConfiguration` 能力接口（照 Site 的 `IHasValueShape`）。** 可行，但宿主每次列字段类型都要 `as` 一下；声明本就是字段类型最基本的事实之一，和 `IndexValueType` 同级，放在 `IFieldType` 上、用默认实现兜住兼容更直接。
- **未声明的类型"默认拒绝所有键"。** 会让每个第三方字段类型在升级后无法保存任何配置。
- **校验器在读路径兜底（"读到未知键就抛"）。** 违背"历史数据必须可读"。
- **顶层的未知字段类型名也由校验器报错。** 没做：宿主保存的是一个它自己管理的类型名，是否允许保存一个未注册类型是宿主的决定，留给宿主。
- **内联字段的未注册类型也留给 `InlineFieldValidator`（值校验时才报）。** 最初这样定，后来推翻：宿主看不进配置里面，定义保存时没有任何地方能拦住内联字段里拼错的类型名——和拼错的键是同一类错误，也正是这个功能要抓的。"类型已被移除、只改显示名的旧定义会变严"的顾虑由 `previous` 解决（原样存在的内联字段不再检查）。
