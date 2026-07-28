# FlexFields 模块设计（设计定稿 · 待落码校准）

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

## 4. 值存储与查询（Y 方案 · 已定）
- **权威存储** = 宿主上一个"便宜读的值袋子"：自建 `IHasFlexFields` + 专属 `FlexFields` 字典/列，**不用 ABP `ExtraProperties`**。理由 = 与公共 ExtraProperties 大袋子**隔离**（防撞名）+ **平台自持**；`IHasExtraProperties`/`ExtraPropertyDictionary` 属 `Volo.Abp.Data`（非 ObjectExtending），故这是隔离/管道取舍非血缘。代价：自补 EF JSON 转换器 / Mongo 内嵌 / 自己的 `FlexibleEntityDto` / AutoMapper（照抄 ABP）。
- **派生查询索引** = 类型化 `FlexFieldIndex` 表（Salesforce `MT_Data` + `MT_Indexes` 思路）。已定：①同库多宿主**共表**（`EntityType` 判别键）+ ②写时**同 UoW** 同步。整条读走袋子；按字段查走索引 → EntityId → 过滤宿主。
- **provider 可换**：EF = 类型化 pivot 表；Mongo = 内嵌值 + 原生路径索引（写时近乎零同步）。别把关系型表焊进抽象层。
- **两类回填**：`Searchable` 翻开（值类型不变）→ 仅 `RebuildAsync`（Mongo ≈ createIndex 近免费；EF 真回填）；控件改类型（值类型变）→ `IFlexFieldValueMigrator` 真迁移，**两 provider 都躲不掉**。回填 job 编排放 `.Domain`（provider 无关），调 provider 实现。

## 5. 总接口清单（按层，落码后校准 —— 与代码一致）

对照 ABP 自己的 `Volo.Abp.Users`：`IUserData`（DDD-free 载体）在 `.Abstractions`，`IUser : IAggregateRoot<Guid>`（Entity 契约）在 `.Domain`，两者靠 `.Domain` 里的 `ToAbpUserData()` 单向桥接、互不继承。FlexFields 按同一套收敛。

- **`.Domain.Shared`**：仅 `FlexFieldConsts`（列长度等）。刻意不含 `FlexFieldValueType`/`FlexFieldDictionary`——那些是 Abstractions 自足所需的类型词汇，`.Abstractions` 与 `.Domain.Shared` 互不引用，只由 `.Domain` 同时依赖两者。
- **`.Abstractions`**（无 DDD 依赖，**不含任何 Entity 契约、不含任何持久化形状**）：
  - 数据载体：`IFlexFieldData` / `FlexFieldData`（对照 `IUserData`/`UserData`）
  - 值袋：`IHasFlexFields`、`FlexFieldDictionary`
  - 运行期载体：`FlexFieldValue`（`Field` 属性类型是 `IFlexFieldData`，不是任何 Entity 接口）
  - 字段类型：`IFieldType`（`GetSearchableValues` 返回 `IEnumerable<object>`，provider 中立）、`FieldTypeBase`、内置类型（`TextFieldType`…）、`FieldTypeResolver`
  - 查询词汇：`FlexFieldValueType`、`FlexFieldQueryOperator`、`FlexFieldQueryCondition`
  - `FieldConfigurationDictionary`、`FieldConfigurationBase`、`FieldValidationArgs`
- **`.Domain`**（依赖 `.Abstractions` + `.Domain.Shared` + `Volo.Abp.Ddd.Domain`）：
  - Entity 契约：`IFlexField : IAggregateRoot<Guid>`——下游的字段定义实体（如 CMS 的 `Field`）实现它
  - 桥接：`FlexFieldExtensions.ToFlexFieldData()`，`IFlexField -> FlexFieldData` 单向
  - 仓储：`IFlexFieldRepository<TField>`（字段定义轴，内核自己从不调用，纯为下游便利）
  - 宿主实体轴的缝：`IFlexFieldProvider<TEntity>`（内核唯一的信息入口）、`IFlexFieldValidator<TEntity>`、`IFlexFieldIndexManager<TEntity>`、`IFlexFieldQueryExecutor<TEntity>`
  - **内核无具体 `FlexField` 实体**（下游类型实现 `IFlexField`）
- **`.EntityFrameworkCore`**（支持不拥有；依赖 `.Domain`）：
  - `FlexFieldIndexValue`——**relational-only**，五个类型化槽位（String/Number/DateTime/Boolean/Guid）+ `Create(FlexFieldValueType, object)` 工厂。只属于这一层：它是关系型透视表的行值形状，`.MongoDb` 不会有对应类型
  - `IFlexFieldIndex`、`FlexFieldIndexBase<TEntity>`（下游每宿主类型一张索引表）
  - `FlexFieldsDbContextModelCreatingExtensions`：`ConfigureFlexFieldsProperty<TEntity>()` / `ConfigureFlexField<TField>()` / `ConfigureFlexFieldIndex<TIndex>()`
  - `EfCoreFlexFieldIndexManagerBase<TDbContext,TEntity,TIndex>`：类型化在这一层发生——从 `IFieldType.GetSearchableValues` 拿到原始值后，配上 `IndexValueType` 转成 `FlexFieldIndexValue`
  - `EfCoreFlexFieldRepositoryBase<TDbContext,TField>`：`IFlexFieldRepository<TField>` 的 EF 实现
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
- 离"成熟 FlexFields"仍缺：联动/依赖字段、跨字段校验（`FieldValidationArgs` 已留 siblings 口）、关系/引用字段（索引表 `GuidValue` 已预留）、字段级权限。

## 8. 实施顺序
`rename（A 组，编译器兜底，先做拿干净基座） → 存储机制（Y） → 外挂 field-type（CkEditor/FileExplorer）与 Angular → CMS 切换（另一仓大工程）`。内核无 DDD 可设计（下游 DDD = CMS 现有模型，只改成引用内核契约）。
