# Infinite Canvas 视觉改造：开源参考调研

本调研只采用项目 GitHub 仓库、仓库许可证和官方文档作为一手来源。重点是提炼适用于 Infinite Canvas 现有 React 画布的视觉与交互原则，并区分“借鉴设计”与“引入运行时依赖”。许可证说明用于工程筛查，不替代法律意见；上线前仍应以所选版本随包许可证为准。

## 结论

- **优先直接沿用现有依赖**：项目已依赖 `lucide-react`、`radix-ui`、`shadcn`。Lucide 适合保持工具栏与节点类型图标的一致；Radix 可供弹层控件使用；shadcn 的价值主要在可编辑源码和样式基线。当前已有 `web/src/components/ui/select.tsx` 使用 Radix Select，且全局样式导入 `shadcn/tailwind.css`。不要仅为了视觉改造再引入一套完整组件库。
- **优先作为交互参考**：Excalidraw 的画布优先、轻量工具操作、箭头绑定与开放格式；React Flow 的节点、边、连接句柄和状态反馈，适合映射到当前节点式工作流。
- **不要直接把 tldraw SDK 用于生产**：其仓库当前 `LICENSE.md` 明确禁止在生产环境使用，除非适用试用许可或另有商业协议。可参考公开文档中的扩展点与交互模型，但不要将 SDK 或其受限代码当作可自由采用的 MIT 组件。
- **自行实现并匹配当前主题**：项目现有画布主题与 Ant Design 已定义主视觉。外部项目适合作为交互范例，不宜照搬品牌、截图或整套视觉皮肤。

## 来源与可借鉴内容

### Excalidraw

- 仓库：[excalidraw/excalidraw](https://github.com/excalidraw/excalidraw)
- 一手来源：[README](https://github.com/excalidraw/excalidraw/blob/master/README.md)、[LICENSE](https://github.com/excalidraw/excalidraw/blob/master/LICENSE)、[官方开发文档](https://docs.excalidraw.com/)、[组件 Props](https://docs.excalidraw.com/docs/@excalidraw/excalidraw/api/props)
- 许可证：MIT；复制或分发软件时需保留版权与许可声明。
- 可借鉴：无限画布、画布优先的层级、深浅主题、形状库、箭头绑定与箭头标签、撤销/重做、缩放和平移、PNG/SVG/剪贴板导出，以及本地优先自动保存的产品行为。轻量工具入口让画布保持主角，常用动作应易达，低频选项可收纳。
- 直接引入建议：**暂不建议作为主画布引擎**。当前应用已有节点、连接、媒体生成及插件节点的领域模型；引入整套白板编辑器会重复承担交互与数据模型。若未来需要独立的手绘标注/白板子功能，再针对组件包 API、样式隔离及数据互操作做小型验证。

### tldraw

- 仓库：[tldraw/tldraw](https://github.com/tldraw/tldraw)
- 一手来源：[README](https://github.com/tldraw/tldraw/blob/main/README.md)、[LICENSE.md](https://github.com/tldraw/tldraw/blob/main/LICENSE.md)、[Shapes](https://tldraw.dev/docs/shapes)、[Tools](https://tldraw.dev/docs/tools)、[UI components](https://tldraw.dev/sdk-features/ui-components)
- 许可证：**tldraw License（非 MIT）**。允许开发环境使用、修改和随其他应用捆绑；明确禁止在 Production Environment 使用，除非获得试用许可或商业协议。还要求不绕过许可密钥机制、保留许可/版权通知等。
- 可借鉴：把形状、工具、绑定关系与 UI 扩展点拆开；自定义工具与形状；吸附、边缘滚动、富文本、嵌入内容和快捷工具栏等交互模型。特别适合用来检查画布的“创建—选择—操作—连接”各状态是否完整。
- 直接引入建议：**生产依赖不建议**，除非先取得并核实适用许可。官方公开能力可作为设计研究参考；不要按 MIT 许可处理，也不要复制其 SDK 实现代码。

### xyflow / React Flow

- 仓库：[xyflow/xyflow](https://github.com/xyflow/xyflow)
- 一手来源：[README](https://github.com/xyflow/xyflow/blob/main/README.md)、[LICENSE](https://github.com/xyflow/xyflow/blob/main/LICENSE)、[Learn 文档](https://reactflow.dev/learn)、[交互概念](https://reactflow.dev/learn/concepts/adding-interactivity)、[视口概念](https://reactflow.dev/learn/concepts/the-viewport)
- 许可证：MIT。README 将 React Flow 核心库标为 MIT；Pro 示例与服务是独立付费产品，不应与核心库许可证混淆。
- 可借鉴：节点卡片和可见连接句柄、连线建立时的有效/无效目标反馈、边标签、选择与拖动状态、缩放和平移控制，以及可控节点/边状态。对 Infinite Canvas 而言，重点是让连接关系和节点状态清晰可扫读，而不是复刻默认节点外观。
- 直接引入建议：**暂不建议替换当前画布实现**。现有项目已有自定义 `InfiniteCanvas`、节点几何、主题及节点注册机制；React Flow 可用于局部原型验证或行为对照。若确有重用需求，应单独评估自定义节点、媒体节点、缩放表现与现有数据模型的映射成本。

### Lucide

- 仓库：[lucide-icons/lucide](https://github.com/lucide-icons/lucide)
- 一手来源：[README](https://github.com/lucide-icons/lucide/blob/main/README.md)、[LICENSE](https://github.com/lucide-icons/lucide/blob/main/LICENSE)、[图标目录](https://lucide.dev/icons/)、[React 用法](https://lucide.dev/guide/react)
- 许可证：ISC；仓库许可证对部分源自 Feather 的图标另附 MIT 版权/许可文本。分发时保留适用的开源声明，并检查所用包版本的许可证文件。
- 可借鉴：统一的描边、尺寸、线宽与语义命名，有助于让选择、移动、连接、媒体类型和编辑动作形成稳定的图标语言。图标应帮助识别高频命令；纯图标按钮提供可访问名称与悬停提示。
- 直接引入建议：**建议直接使用现有 `lucide-react`**。项目已在画布工具栏使用它，无需重复安装或另建图标体系。避免把普通图标当作品牌标志。

### Radix Primitives

- 仓库：[radix-ui/primitives](https://github.com/radix-ui/primitives)
- 一手来源：[README](https://github.com/radix-ui/primitives/blob/main/README.md)、[LICENSE](https://github.com/radix-ui/primitives/blob/main/LICENSE)、[Introduction](https://www.radix-ui.com/primitives/docs/overview/introduction)、[Accessibility](https://www.radix-ui.com/primitives/docs/overview/accessibility)、[Components](https://www.radix-ui.com/primitives/docs/components/overview)
- 许可证：MIT。
- 可借鉴：无样式、可组合的交互基元强调键盘操作、焦点管理、ARIA 与可定制性。画布周边菜单、Popover、Tooltip、Dialog、Tabs、Toggle 等控件可把行为交给成熟原语，外观继续由当前画布主题控制。
- 直接引入建议：**复用项目现有 `radix-ui` 包，仅按具体交互增量采用**。不要同时引入另一份重复 primitives；确保新的画布控件有键盘路径、焦点返回和合适的无障碍名称。

### shadcn/ui

- 仓库：[shadcn-ui/ui](https://github.com/shadcn-ui/ui)
- 一手来源：[README](https://github.com/shadcn-ui/ui/blob/main/README.md)、[LICENSE.md](https://github.com/shadcn-ui/ui/blob/main/LICENSE.md)、[官方文档](https://ui.shadcn.com/docs)、[组件文档](https://ui.shadcn.com/docs/components)
- 许可证：MIT。
- 可借鉴：官方 README 强调 Open Code：组件源码分发到应用中，可以由项目直接修改；同时强调组合式接口和可作为自有组件库的起点。对于视觉改造，价值在于选择少量控件源码并按当前主题调整，而非强制接受固定皮肤。
- 直接引入建议：**使用已有 shadcn 配置与代码，不再重复添加脚手架/依赖**。当前项目有 `shadcn` 依赖、Tailwind 导入和 UI 组件目录；实际组件采用前先检查是否与 Ant Design 重复。随项目分发时遵守组件文件自带的许可/署名要求。

## 对 Infinite Canvas 的改造启发

1. **维持画布为视觉主区**：常用创建与画布模式入口保持紧凑，低频设置采用轻量弹层或侧栏；工具状态应明确，但不要让面板和装饰抢占节点内容。
2. **把状态反馈放在操作发生处**：选择框、连接预览、有效连接目标、拖拽命中、节点运行状态与错误提示应在画布对象附近形成连续反馈。连线与节点需保证缩放时仍可辨识。
3. **沿用现有主题变量**：借鉴其他项目的留白、层级和交互，不照搬其背景灰度或颜色。浅色/深色模式中的画布、节点、边、选中态和浮层均应经过主题变量统一校验。
4. **图标保持一致且可访问**：优先从 Lucide 已有图标集中选取；图标按钮同时提供 aria-label 与可见提示，禁用、选中和危险动作要有不同状态表达。
5. **按功能而非名气增加依赖**：画布引擎替换会牵涉数据格式、媒体节点、插件扩展、快捷键、缩放和持久化；只引入能解决明确缺口的部件，并先在一个窄流程验证。

## 当前项目检查

根目录 `web/package.json` 已声明 `lucide-react`、`radix-ui` 和 `shadcn`；`web/src/components/canvas/canvas-toolbar.tsx` 使用 Lucide 图标，`web/src/components/ui/select.tsx` 使用 Radix Select，`web/src/styles/globals.css` 引入 shadcn Tailwind 样式。当前没有从 `web/src` 发现 `reactflow`、`@xyflow/react`、`tldraw` 或 `@excalidraw/excalidraw` 的引用。建议依照现有架构做视觉实现，避免并行维护重复控件或画布引擎。

