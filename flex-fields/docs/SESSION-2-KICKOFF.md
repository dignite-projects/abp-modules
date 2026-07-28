# FlexFields 迁移 · Session 2 开工简报（存储机制 Y · 仅 EF）

> 前置：Session 1 已完成（三层 net10 绿）、loc-key 清理已完成。
> **先读 `flexfields-design.md` 的 §4（值存储 Y）、§5（总接口清单）、§6（接口签名）。** 本文件讲 Session 2 干什么、怎么干、别碰什么。

## 0. 这一 session 是有真实逻辑的一步（不再是 rename）
建 Y 方案的**内核机制**：宿主上的"自建值袋子"当权威存储 + 类型化 `FlexFieldIndex` 当派生查询索引。**只做 EF 一条 provider**；Mongo 留 Session 3（设计已定"EF 先行，Mongo 随后"）。

## 1. Session 2 范围
**做（本仓 `flex-fields/`）：**
- `.Domain.Shared` 补：`FlexFieldQueryOperator`、`QueryingByField`（带 `Operator` + `ValueType`）、`FlexFieldConsts`（若缺）。
- `.Abstractions` 补：`IHasFlexFields` + `FlexFieldDictionary`、`FlexFieldEntityRef`、`IFlexFieldProvider`、`IFlexFieldIndexStore`、`IFlexFieldQueryExecutor`、`IFlexFieldValueMigrator`、`IFlexFieldIndexManager`。（`FlexFieldIndexEntry` Session 1 已占位——核对它字段是否符合设计 §6。）
- `.Domain` 补：`FieldTypeBase.Project` 默认单值实现 + `SelectFieldType`/`TreeFieldType` override 成多值；`FlexFieldIndexManager`（编排：保存后 `IFlexFieldProvider.GetFlexFieldsAsync` → 取 `Searchable` 的 → `IFieldType.Project` → `IFlexFieldIndexStore.SynchronizeAsync`，同一 UoW）；`FlexFieldIndexRebuildJob : AsyncBackgroundJob<…>`（provider 无关，只调 `IFlexFieldIndexStore.RebuildAsync`）。
- **自建值袋子**：`IHasFlexFields`（**自有，不继承 `IHasExtraProperties`**）+ `FlexFieldDictionary`；EF 侧一个把 `FlexFields` 字典映射成 JSON 列的 value converter / ModelBuilder 扩展；`FlexibleEntityDto` 基类 + AutoMapper 辅助（对照 ABP 的 ExtraProperties 那套照抄形状）。
- **新建 `flex-fields/src/Dignite.Abp.FlexFields.EntityFrameworkCore`**（支持不拥有）：`FlexFieldIndex` 实体 + `IFlexFieldsDbContext`（`DbSet<FlexFieldIndex>`）+ `FlexFieldsDbContextModelCreatingExtensions.ConfigureFlexFields()` + `EfCoreFlexFieldIndexStore`（绑 `IFlexFieldsDbContext`）+ `EfCoreFlexFieldQueryExecutor` + `EfCoreFlexFieldValueMigrator` + 模块类。**无具体 DbContext。**
- 测试：索引 upsert / 按字段查（多条件 Id 交集）/ 各类型 Project / reindex 回填，EF 侧跑绿。

**不做（明确排除）：**
- ❌ **`.MongoDB`——留 Session 3。**
- ❌ 任何 CMS 改动、Angular、外挂 field-type。
- ❌ 跨字段校验、关系/引用字段（`FlexFieldIndexEntry.GuidValue` 已预留，本轮不实现语义）。
- ❌ 不建"内核自己的 DbContext / 落库领域表"——内核只出 `ConfigureFlexFields()` + 基类,物理表 + 迁移归下游。

## 2. 参考源（从 `../dignite-abp/` 读，**参考不照抄**）
现有最接近的实现在 CMS 里（`modules/cms/src/Dignite.Cms.Domain(.Shared)/Dignite/Abp/Data/`）：`IHasCustomFields`、`QueryingByField`、`FieldQueryingBase<T>`、`CustomizableObject`、`IFieldQuerying`、各 `*FieldQuerying`、`EfCoreEntryRepository.QueryingByFields`。
**注意它们是"旧世界"**：基于 `ExtraProperties`、查询是内存 `IEnumerable`。Y 要的是：**自建袋子（非 ExtraProperties）+ 类型化索引表 + provider 可换 + DB 下推**。拿它们理解语义,**按 Y 重写**,不要 copy 过来。

## 3. 关键约束（设计已定，别翻案）
- 值袋子**自建 `IHasFlexFields`,不用 ABP `ExtraProperties`**（隔离 + 平台自持）。
- 权威存储 = 袋子；索引 = 派生。整条读走袋子;按字段查走索引 → EntityId → 过滤宿主。
- 索引表**同库多宿主共表**（`EntityType` 判别）+ 写时**同 UoW** 同步。
- `QueryingByField` **自带 `ValueType`** → 查询侧自描述,不回查定义。
- `IFieldType.Project` 是投影的唯一入口（Session 1 已在接口上）。
- reindex/迁移 job 编排在 `.Domain`(provider 无关),EF 只出 `RebuildAsync`/`Migrator` 的**实现**——不需要"EF 能否跑 job"。
- 落库边界:内核给"支持",下游给 DbContext + 表 + 迁移。

## 4. 步骤
1. `.Domain.Shared` → `.Abstractions` → `.Domain` 依次补上第 1 节的类型/接口,先让三层带着新接口**编译绿**（实现可先抛 `NotImplementedException` 占位）。
2. 自建值袋子 + EF value converter（对照 ABP `ExtraProperties` 的 JSON 列映射)。
3. 建 `.EntityFrameworkCore` 工程（**以某个现有模块的 `.EntityFrameworkCore.csproj` 为模板**,net10 + 中央版本）：`FlexFieldIndex` + `IFlexFieldsDbContext` + `ConfigureFlexFields()`。
4. 实现 `EfCoreFlexFieldIndexStore`（Synchronize 同 UoW upsert；Rebuild 分批回填）、`QueryExecutor`（LINQ 打索引表、多条件交集）、`ValueMigrator`。
5. `FlexFieldIndexManager` + `FlexFieldIndexRebuildJob` 串起来。
6. 测试 EF 侧跑绿（见第 1 节测试项）。
7. `dotnet build` + 测试到绿。

## 5. 完成判定
- ✅ `.Domain.Shared`/`.Abstractions`/`.Domain`/`.EntityFrameworkCore` 全部 net10 编译绿、挂进 sln。
- ✅ EF 侧:保存宿主 → 索引同步；按字段查走索引(非内存);reindex 回填 works。测试绿。
- ✅ 内核**无具体 DbContext、无落库领域表**;只出 `ConfigureFlexFields()` + 基类。
- ✅ 值袋子是自建 `IHasFlexFields`,**无 `IHasExtraProperties` 依赖**。
- ✅ 未碰 Mongo / CMS / Angular / 外挂 field-type;旧 DynamicForms 仍原地。
- 产出后跟用户对一次,再开 Session 3（Mongo provider + 双 provider conformance 测试）。

## 6. 几个坑（设计里论证过）
- **EF/Mongo 不对称是固有的**,但本轮只做 EF;设计契约已把不对称关进 provider 层,别把关系型表焊进 `.Abstractions`。
- **类型保真**:EF 显式写 `NumberValue`/`DateTimeValue` 等,别让数字当字符串。
- **多值字段**(Select 多选/Tree)= 多条索引行,`Project` override。
- reindex 两类触发:`Searchable` 翻开(值类型不变)只 `RebuildAsync`;控件改类型(值类型变)走 `ValueMigrator`——本轮把接口和 EF 实现都做上。
