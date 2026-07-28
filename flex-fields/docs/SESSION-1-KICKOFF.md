# FlexFields 迁移 · Session 1 开工简报

> 这是把 `dignite-abp` 的 `Dignite.Abp.DynamicForms` 拆出、改名 **FlexFields**、迁入本仓 `abp-modules` 的第一步执行说明。
> **先读同目录的 `flexfields-design.md`（完整设计）。** 本文件只讲"这一 session 干什么、怎么干、别碰什么"。

## 0. 背景（一句话）
FlexFields = 一个 `Volo.Abp.Users` 式的**约束性内核**：给"给任意宿主对象挂运行期可配置字段"提供约束/接口/机制，领域模型（定义/分组/类型绑定/宿主实体）全归下游。详见设计稿。

## 1. Session 1 范围（只做这些）
**做：** 把源工程 `Dignite.Abp.DynamicForms`（**核心那一个**）按命名映射搬进本仓，拆成 `.Abstractions` / `.Domain.Shared` / `.Domain` 三层，**net10 编译绿**。
**不做（本 session 明确排除）：**
- ❌ 存储机制 Y（`IHasFlexFields` / `FlexFieldIndex` / EFCore·Mongo provider / 索引 / 查询 / reindex）——留后续 session。
- ❌ 外挂 field-type（CkEditor / FileExplorer）、Angular 包——留后续。
- ❌ 任何 CMS 改动——CMS 切换是另一仓的独立大工程，最靠后。
- ❌ 改任何**持久化字符串**（`"TextEdit"` 之类的 `ControlName`、配置键）——那是数据，归存储迁移。

## 2. 目标落点（本仓路径，对齐 file-storing / notifications 的结构）
```
abp-modules/flex-fields/
  docs/                     # 本文件 + flexfields-design.md
  src/
    Dignite.Abp.FlexFields.Abstractions/
    Dignite.Abp.FlexFields.Domain.Shared/
    Dignite.Abp.FlexFields.Domain/
    （EntityFrameworkCore / MongoDB —— 后续 session 建）
  test/
    Dignite.Abp.FlexFields.Tests/
  angular/                  # 后续
```

## 3. 源清单（从 `../dignite-abp/` 拉；去留已定）
| 源（相对 dignite-abp 仓根） | 处置 |
|---|---|
| `framework/src/Dignite.Abp.DynamicForms` | **搬**（Session 1 主体）→ 拆成 Abstractions/Domain.Shared/Domain，按第 5 节改名 |
| `framework/test/Dignite.Abp.DynamicForms.Tests` | **搬** → `flex-fields/test`（改名；剔除依赖 Blazor 的用例） |
| `framework/src/Dignite.Abp.DynamicForms.Components` | ❌ **弃**（Blazor，已定不保留） |
| `framework/src/Dignite.Abp.DynamicForms.Components.BlazoriseUI` | ❌ **弃**（Blazor） |
| `framework/test/Dignite.Abp.DynamicForms.Components.Tests` | ❌ **弃**（Blazor） |
| `modules/ckeditor-component/src/Dignite.Abp.DynamicForms.CkEditor` | **搬**（外挂 field type → RichText）——**后续 session** |
| `modules/file-explorer/src/Dignite.Abp.DynamicForms.FileExplorer` | **搬**（外挂 field type）——**后续 session** |
| `modules/*/src/Dignite.Abp.DynamicForms.Components.*`（CkEditor/FileExplorer 的 Blazor 版） | ❌ **弃** |
| `npm/ng-packs/packages/dynamic-form` | **搬** → `flex-fields/angular`——**后续 session** |
| CMS 里 `.../Dignite/Abp/DynamicForms/{Matrix,Table,Entry}`（field types） | **留 CMS**（下游专属；将来 re-point 到 `IFieldType`） |
| CMS 里 `.../Dignite/Abp/Data/*`（`IHasCustomFields`/`QueryingByField`/`FieldQueryingBase`/`CustomizableObject`） | **后续搬进 FlexFields 存储机制（Y）**，不是 Session 1 |

## 4. 命名映射（改名就照这张，逐一改）
| 旧 | 新 |
|---|---|
| 命名空间 `Dignite.Abp.DynamicForms` | `Dignite.Abp.FlexFields` |
| `FormField` | `IFlexField`（**改成只接口**；具体载体本 session 不建，见设计稿"不给具体类"） |
| `IFormControl` / `FormControlBase` | `IFieldType` / `FieldTypeBase` |
| `FormControlSelector` / `IFormControlSelector` | `FieldTypeResolver` / `IFieldTypeResolver` |
| `FormConfigurationBase` / `FormConfigurationDictionary` | `FieldConfigurationBase` / `FieldConfigurationDictionary` |
| `FormControlValidateArgs` | `FieldValidationArgs`（瘦成只装 `IFlexField Field`；`Validate` 改为**返回** `IReadOnlyList<ValidationResult>`） |
| `TextEditFormControl` / `NumericEditFormControl` / `DateEditFormControl` / `SelectFormControl` / `SwitchFormControl` / `TreeViewFormControl` | `TextFieldType` / `NumberFieldType` / `DateTimeFieldType` / `SelectFieldType` / `BooleanFieldType` / `TreeFieldType` |
| 各 `*Configuration` / `*ConfigurationNames` / `*Mode` | 同规则（去 `Edit`、Form→Field） |
| `AbpDynamicFormsModule` / `AbpDynamicFormsResource` | `FlexFieldsAbstractionsModule`（分层各一个 Module 类）/ `FlexFieldsResource` |

分层归属：接口 + `IFlexField` + `FieldConfigurationBase` + `FieldValidationArgs` → `.Abstractions`；枚举/常量/`*Dictionary`/`*ConfigurationNames`/`FlexFieldsResource` → `.Domain.Shared`；`FieldTypeBase` + 具体 `*FieldType` + `FieldTypeResolver` → `.Domain`。`IFieldType.Project(...)` 是本轮**新增**的方法（见设计稿签名），本 session 可先给 `FieldTypeBase` 一个默认单值实现占位（`FlexFieldIndexEntry` 先放 `.Abstractions`，即使存储机制还没落）。

## 5. 步骤
1. 建目录树（第 2 节），**以 `../abp-modules/notifications/core/src/Dignite.Abp.Notifications.Abstractions/*.csproj` 为模板**造三个 csproj（拿 net10 TFM + 中央包版本，别自己写版本号）。
2. 把核心源码 copy 过来，**全局套用第 4 节命名映射**，按分层归属拆到三层。
3. 三个 `Module` 类的依赖链：`Domain → Domain.Shared`、`Domain → Abstractions`、`Abstractions → Domain.Shared`。
4. 加进解决方案（照现有模块怎么挂 sln 就怎么挂）。
5. `dotnet build` 到**绿**。有编译错就地修，别改设计。
6. 端口测试项目，跑通非 Blazor 用例。

## 6. 已定的决定 —— 不要重新讨论（这窗已吵过很多轮）
- 名字就是 **FlexFields**；`DynamicForms`/`DynamicObjectExtending` 已否。
- 定位 = **Users 式约束内核，零领域模型**；不做成可运行/自带库的应用模块。
- `IFormControl` → **`IFieldType`**（C# 侧不渲染，它是"类型"不是"控件"）。
- **弃掉所有 Blazor `.Components*`**；去掉 Blazorise 的 `Edit` 后缀。
- `IFlexField` **只接口**，内核不给具体 `FlexField` 载体（要 UserData 式的可后补）。
- 值存储用**自建袋子**不用 `ExtraProperties`——但那是**存储机制 session**的事，Session 1 不碰。
- 若真发现设计有问题：**记下来、继续**，别原地推翻重设计；留到与用户对账。

## 7. 完成判定（Definition of Done）
- ✅ `flex-fields/src` 三层 net10 编译绿、挂进 sln。
- ✅ 命名映射全套用，无 `Form*`/`DynamicForms` 残留（持久化字符串除外）。
- ✅ 无任何 Blazor 依赖残留。
- ✅ 未触碰 CMS、未触碰 dignite-abp 里的旧 `DynamicForms`（旧的原地留着，别删）。
- 产出后跟用户对一次，再决定下一 session（大概率是存储机制 Y）。
