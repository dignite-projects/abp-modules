# abp-modules 迁移方案 / 执行清单

把 `abp-file-storing`、`abp-notifications`(以及日后的 `flex-fields`)合并进本仓库 `dignite-projects/abp-modules`,做成 ABP 式 mini-monorepo。

## 背景与决定

- **库合一仓,app 分仓**:可复用库(file-storing / notifications / flex-fields)并进 `abp-modules` 单仓;可运行应用(如 `sites`)各自独立仓。规则一句话:**是库就并进来,是 app 就分出去。**
- **锁步统一版本**:全仓一个 `<Version>`、一个 `v*` tag、一条发布流水线。MAJOR = 所targeting的 ABP 大版本(≥10)。接受"空转 bump"(改一个模块,另一个也跟着发一个内容无变化的新版本号)。
- 每个模块本身已是 `core/` + 功能组 + `host/` + `angular/` 的结构,与 ABP 的 `framework/`+`modules/<name>/` 同构,整体平移即可。

## 三条全程不能碰的不变量

1. **PackageId 与根命名空间不变** —— 所有包名(`Dignite.Abp.FileStoring`、`Dignite.Abp.Notifications` …)原样保留。搬进子目录**不改 PackageId**(它跟 AssemblyName 走,不跟文件夹走),对 NuGet 消费者完全透明。
2. **`AssemblyVersion` 死钉 `1.0.0.0`** —— 两仓现在都是这个值,合并后进根 props 保持不变。notifications 的 `NotificationData` 反序列化依赖 AssemblyQualifiedName 的稳定,详见其 `.claude/rules/framework/common/notifications-invariants.md` §1。
3. **统一版本 MAJOR ≥ 10** —— 继续压制 legacy 老包(`Dignite.Abp.Notifications*` 已发到 3.8.2),从 10.x 起步永久高于它。

## 已知需要调和的点(迁移中具体处理)

- **中央包版本冲突**:`Microsoft.Extensions.FileProviders.Embedded` —— file-storing 用 `10.0.9`,notifications 用 `10.0.7` → 取 **10.0.9**。
- **保留 file-storing 的安全 pin**:`SQLitePCLRaw.*` 全家 `2.1.12` + `CentralPackageTransitivePinningEnabled`(notifications 那份没有)。
- **按包不同的元数据**:`Product` / `PackageTags` / `PackageProjectUrl` / `RepositoryUrl` 两仓不同 → URL 全改成 `.../abp-modules`;Product/Tags 用每个模块组一个薄 `Directory.Build.props` 覆盖保留。
- **根级配置**:`global.json` / `NuGet.Config` 用 file-storing 那份(notifications 没有)。

---

## Phase 0 — 动手前定两个小数
- [x] **统一版本基线**:查两个包在 NuGet 上各自已发布的最高版本,合并首发取一个明确更高的号(否则 NuGet 拒收)。当前都在 `10.0.0-rc.x` 预发布。 → 基线 = `10.0.0-rc.4`
  - 查证(NuGet flat-container index.json,两仓全部 25 个可打包项目逐个查):`Dignite.Abp.FileStoring*` 从未发布;`Dignite.FileExplorer.*` legacy 族最高 `3.8.2`;`Dignite.Abp.Notifications*`/`Dignite.NotificationCenter.*` 已发布最高 `10.0.0-rc.3`(notifications 本地 `Directory.Build.props` 虽已改到 `10.0.0-rc.4`,但从未打 tag、从未推送到 NuGet,CHANGELOG 也还没有 `[10.0.0-rc.4]` 小节)。确认 `10.0.0-rc.4` 未被任何一方占用,可安全作为合仓首发基线。
- [x] **历史保留方式**:`git subtree`(保留两仓提交历史)。

## Phase 1 — 建骨架
- [x] 本地 `git init`,并提交一个初始 commit(先把 MIGRATION.md + 骨架提交;`git subtree add` 需要仓库已有 HEAD 才能合并,空仓会失败)
  - 骨架提交内容:`LICENSE`/`.editorconfig`/`.gitattributes`(两仓字节级相同,直接取用)、`global.json`/`NuGet.Config`(取 file-storing 那份)、`.gitignore`(file-storing 版本为底,补 notifications 独有的 `dist/`、`.angular/`)。`Directory.Build.props`/`Directory.Packages.props`/`.slnx` 留给 Phase 3/4,避免这里先写一遍、Phase 3/4 再重写。
- [ ] (可后置,Phase 5 前建好即可)在 `dignite-projects` 下建 GitHub 仓 `abp-modules` —— **建成完全空的,别勾 README/.gitignore/license 自动初始化**(否则会生成初始 commit,和本地历史成两条 unrelated 分支,push 要额外处理);本地骨架就绪后加 origin 再 push
- [ ] 目标布局:
  ```
  abp-modules/
    Directory.Build.props      # 共享:语言/Nullable/包元数据/SourceLink/符号包/唯一 <Version> + <AssemblyVersion>
    Directory.Packages.props   # 合并后的中央包版本
    global.json  NuGet.Config
    Dignite.Abp.Modules.slnx   # 聚合解决方案
    .github/workflows/         # 一条 build+test,一条锁步 release.yml
    file-storing/    { core/  file-explorer/  host/  angular/ }
    notifications/   { core/  notification-center/  host/  angular/ }
    # flex-fields/  日后同法并入
  ```

## Phase 2 — 带历史搬代码
- [x] ```bash
  git remote add fs ../abp-file-storing
  git remote add nt ../abp-notifications
  git subtree add --prefix=file-storing  fs main
  git subtree add --prefix=notifications nt main
  ```

## Phase 3 — 统一构建配置(锁步核心)
- [x] 根 `Directory.Build.props`:两份合一 → 唯一 `<Version>`(Phase 0 的号)+ `<AssemblyVersion>1.0.0.0</AssemblyVersion>`;URL 改 abp-modules
- [x] 每个模块组一个薄 `Directory.Build.props`(显式 import 上层),只覆盖 `Product`/`PackageTags`
  - MSBuild 每个项目只自动 import 最近的一份 `Directory.Build.props` 就不再往上找,所以两个模块的薄文件用 `$([MSBuild]::GetPathOfFileAbove(...))` 显式 import 根文件。两个 host(`file-storing/host/.../Web.Host` 靠 `common.props`、`notifications/host/.../Web.Host` 靠自己目录里更近的一份 `Directory.Build.props`)本来就用各自机制短路了这条自动查找链,不受影响,原样保留。
  - README/icon 打包用的 `<None Include>` 仍留在模块层薄文件里(指向各自现有的 README.md/icon.png),等 Phase 6 根 README 合并完再挪到根、模块层薄文件届时才真正瘦成只剩 Product/PackageTags。
- [x] `Directory.Packages.props`:并集,冲突取高版本(见"已知调和点"),保留安全 pin
  - 合并前用脚本比对过两份文件的公共 PackageId,确认真正冲突只有已知的这一个,没有漏网的。
- [x] `global.json` / `NuGet.Config` / 两个 host 的 props 覆盖照搬
  - `global.json`/`NuGet.Config` 用 file-storing 那份放到根,删掉 file-storing/ 下现在重复的两份。
  - **意外发现并修复**:`notifications/host/.../Web.Host.csproj` 内联固定 `Microsoft.Extensions.FileProviders.Embedded` 到 `10.0.7`(这个 host 用自己的 Directory.Packages.props 退出了中心化包管理,不跟中心版本走),合并后中心版本升到 `10.0.9`,restore 报 `NU1605` 包降级错误(`Dignite.NotificationCenter.Web` 需要 >= 10.0.9)。这一行内联版本号跟着中心决议同步改成 `10.0.9`,host 的其余内容原样未动。
  - 顺带把 Phase 2 用完的本地 `fs`/`nt` remote 清掉了(只是搬代码用的临时 remote,留着会让 SourceLink 在每次 build 里报"远程 URL 无效"的噪音警告)。
  - 顺带发现 `ScheduleWakeup` 会在 `.claude/` 下留一个 `scheduled_tasks.lock`,和 `settings.local.json` 一样是本机运行时状态、不该进仓库,已经在根 `.gitignore` 里单独排除(两条都用 `**/.claude/...` 写,不影响 `file-storing/.claude/rules/`、`notifications/.claude/rules/` 这些真正要保留追踪的规则内容)。
  - **验证**:`file-storing/` 和 `notifications/` 各自的 `.slnx` 分别 `dotnet restore` + `dotnet build -c Release`(均 0 error)+ `dotnet test`(file-storing 34/34 通过,notifications 40/40 通过)全部跑绿。聚合根 `.slnx` 要到 Phase 4 才建,这里先用两个模块各自现有的 `.slnx` 当验证入口。

## Phase 4 — 解决方案 + ABP Studio
- [x] 根建聚合 `.slnx`;各模块 `.slnx` 可保留供聚焦开发
  - 根 `Dignite.Abp.Modules.slnx` = file-storing 和 notifications 两份 `.slnx` 的并集,每个 `Project Path`/`Folder Name` 加上各自模块前缀(`file-storing/…`、`notifications/…`),两个模块自己的 `.slnx` 原样保留未动。
  - 验证:根 `.slnx` `dotnet restore` + `dotnet build -c Release`(0 error)+ `dotnet test`(全部子项目通过,0 失败)跑绿。
- [ ] ABP Studio 文件(`.abpmdl`/`.abpsln`/`.abpstudio`)在 ABP Studio 里重建,别手改 —— **待你操作**,汇总在下面的"需要你操作"清单里,未触碰 `file-storing/Dignite.Abp.FileStoring.abpmdl`/`.abpsln`/`.abpstudio/`(notifications 那边本来就没有这些文件)。

## Phase 5 — CI/CD
- [x] 一条 build+test 工作流(构建聚合方案)—— 根 `.github/workflows/ci.yml`,跑 `Dignite.Abp.Modules.slnx`,两模块的测试项目、NuGet 打包+smoke-test、两个 Angular 库分别 build+pack+smoke-test 全部收进一条工作流。
- [x] 一条 `release.yml`:`v*` tag → pack 全部可发布项目 → NuGet + 两个 angular 库发 npm,版本来自唯一 `<Version>` —— 根 `.github/workflows/release.yml`,在 file-storing 那份(更完整:漏洞门禁 + HTTP E2E)基础上合并 notifications,两个 `npm publish` 都跑。
  - 版本核对脚本合一:根 `.github/scripts/verify-version-lockstep.ps1` 现在检查根 `Directory.Build.props` 的 `<Version>` 和**两个** Angular 库 `package.json` 一致(原来两仓各自的版本各查各的);两仓原来的 `verify-version-lockstep.ps1` 已删除。`smoke-test-nuget-packages.ps1`/`smoke-test-angular-package.mjs`(按包名/导出面硬编码,两份分别保留在各自模块的 `.github/scripts/` 下)、file-storing 独有的 HTTP E2E 脚本原样不动,新工作流按相对路径引用。
  - 顺带把 lockstep 缺口焊上:`file-storing/angular/projects/file-explorer/package.json` 版本原来还停在 `10.0.0-rc.1`(notification-center 那份已经是 `10.0.0-rc.4`,巧合已对齐),这次一并改成 `10.0.0-rc.4`;两个 Angular 库 `package.json` 里的 `homepage`/`repository.url` 也改成 `abp-modules`(和 Phase 3 改 `PackageProjectUrl`/`RepositoryUrl` 是同一件事的 npm 那一半,当时漏了)。
  - 根 `.nvmrc` 取 file-storing 那份(`22.14.0`,notifications 原来是在 workflow 里硬编码 `22.x`,`22.14.0` 满足这个范围)。
  - **本地验证**(GitHub Actions 本身没法在这里跑,但脚本和产物本身都实测了):`dotnet pack Dignite.Abp.Modules.slnx` 产出全部 25 个 nupkg;两仓的 `smoke-test-nuget-packages.ps1` 各自跑通(10/10、15/15 包都能被一个消费者项目还原+编译);`verify-version-lockstep.ps1` 本地确认三处版本号(根 `<Version>` + 两个 Angular 包)对齐,输出 `10.0.0-rc.4`。Angular 侧的 `smoke-test-angular-package.mjs`(需要真的 `npm ci` + `ng build` 两个工作区)没在本地跑,留给 Phase 7 首次真实 CI 跑验证。
  - **发现一个需要你决定的事**:统一漏洞门禁(`dotnet list ... --vulnerable`,file-storing 原来就有,notifications 原来没有)套到合并后的方案上,当场检测到 notifications 这边**4 个项目**有 High 级漏洞——已发布的 `Dignite.Abp.Notifications.Emailing`/`Emailing.Identity` 传递依赖 `Scriban 7.2.1`(GHSA-7jvp-hj45-2f2m);本地 dev-only、从不打包的 `Dignite.NotificationCenter.Web.Host` 另外还有 `MessagePack 3.1.1`、`Microsoft.OpenApi 2.3.0`、`SQLitePCLRaw.lib.e_sqlite3 2.1.11`(和 file-storing 那条已经在 pin 的 CVE 是同一个,只是这个 host 自己退出了中心化包管理没吃到那个 pin)三个 High。这些不是这次迁移引入的,是 notifications 仓一直有、只是之前没有这道门禁在测——我把门禁原样接上了但**没有**自作主张加白名单,所以现在 CI/release 会在这一步真的挡住。需要你定:(a) 升级/替换 Scriban 消掉它,(b) 给 Scriban 加一条写明理由的白名单放行(参考 file-storing release.yml 注释里的模式),还是 (c) 先只在 Host 项目上加白名单、库项目的 Scriban 保留门禁逼着修。见下面"需要你操作的事项"。
- [ ] **【手动】重配 NuGet.org Trusted Publishing**:两个包的策略从旧仓名 + 旧 `release.yml` 改到 `dignite-projects/abp-modules` + 新工作流,否则发布授权失败

## Phase 6 — 文档与规则
- [ ] README/CONTRIBUTING/SECURITY 合并到根
- [ ] **CONTRIBUTING 版本小节重写**:"MINOR/PATCH 各自独立" → "锁步统一,MAJOR=ABP 大版本"
- [ ] 合并两套 `.claude`/`.agents`/`AGENTS.md`;**保留 `notifications-invariants.md`**
- [ ] CHANGELOG 合成根级一份

## Phase 7 — 验证 + 首个统一发布
- [ ] 聚合方案 build + 两模块全测试跑通
- [ ] `dotnet pack` 出全部包,核对 PackageId / 版本 / AssemblyVersion 无变化
- [ ] 打第一个统一 tag,走一遍 release

## Phase 8 — 退役旧仓
- [ ] `abp-file-storing`、`abp-notifications` 归档(README 顶部指向 abp-modules),不删

---

## 需要你操作的事项(汇总)

跑到对应 Phase 时不会自己硬来,停下来等你处理:

- [ ] **Phase 1/5**:在 `dignite-projects` 下建 GitHub 仓 `abp-modules`(建成完全空的,别自动初始化 README/.gitignore/license),本地骨架就绪后加 `origin` 再 push。
- [ ] **Phase 4**:ABP Studio 聚合解决方案(`.abpmdl`/`.abpsln`/`.abpstudio`)在 ABP Studio 里重建。
- [ ] **Phase 5(决定,不是操作)**:notifications 这边 4 个项目的 High 漏洞(`Scriban` 影响已发布的 `Emailing`/`Emailing.Identity`;`Web.Host` 另有 `MessagePack`/`Microsoft.OpenApi`/`SQLitePCLRaw`)怎么处理——修掉、白名单、还是只白名单 Host、库项目逼着修。统一漏洞门禁现在原样接上、没加白名单,选定前 CI/release 会在这步失败。详见 Phase 5 记录。
- [ ] **Phase 5**:NuGet.org Trusted Publishing 策略从两仓旧名 + 旧 `release.yml` 改到 `dignite-projects/abp-modules` + 新工作流,否则统一发布会在签名/授权这步失败。
