<p align="center">
  <img src="web/public/logo.svg" width="96" alt="无限画布">
</p>

# 无限画布 Windows 桌面版（Beta）

本仓库基于 [basketikun/infinite-canvas](https://github.com/basketikun/infinite-canvas) 继续开发，将多模态创作画布与 Windows WPF 启动器结合。提示词、参考素材、模型配置和生成结果可以组织成相互连接的节点。

上游项目由 basketikun 开发；本仓库维护 Windows 启动器及相关适配，不是上游官方桌面发行版。

## 核心功能

- 无限画布：文本、图片、视频、音频、生成配置与分组节点，支持连线、缩放、撤销重做和项目导入导出。
- 多渠道模型配置：使用自己的模型服务、Base URL 和 API Key，组合文本和图片生成工作流。
- 本地素材、提示词与生成记录；可自行配置 WebDAV 同步。
- Windows 启动器：管理本地 Vite 服务，通过 Microsoft Edge App 模式打开画布，同步画布配置并提供服务日志。

启动器基础启动与配置同步、图片生成链路、文字生成、画布节点重启保留及 API Key 保存逻辑已经由维护者验收。视频、音频以及不同服务商接口的适配范围请以实际测试为准；一次渠道验收不代表所有模型服务都兼容。

## 快速开始：Windows 便携包

当前发行路线为 **Windows x64 完整便携包**，暂不提供正式安装器或自动更新。发布附件以本仓库的 [Releases](https://github.com/tongxuedashu-design/infinite-canvas-desktop/releases) 为准；没有附件时请勿把 GitHub 源码 ZIP 当作可运行包。

1. 安装 [Microsoft Edge](https://www.microsoft.com/edge)。用户电脑不需要另装 Node.js 或 .NET。
2. 下载完整便携 ZIP，全部解压到当前用户可写的本地目录，例如 D:\Apps\InfiniteCanvas；不要直接在压缩包内运行。
3. 双击 InfiniteCanvasDesktop.exe，等待本地服务启动并打开画布；未自动打开时点击启动器的「打开画布」。
4. 在画布右上角「配置与偏好」添加自己的渠道、模型及 API Key，选择对应能力的模型并保存，再创建画布开始使用。
5. 重要项目定期导出；关闭启动器窗口会隐藏到托盘，需要停止服务时从托盘选择「退出」。

**请保留整个文件夹。单独 exe 不能运行**，它还需要 web/、web/node_modules/、runtime/node.exe、VERSION、CHANGELOG.md 和 .infinite-canvas-portable 标记。

首次启动会自动在便携目录内创建 EdgeData/。画布和素材保存在此 Edge 独立浏览器数据目录；不会随账号自动云同步。桌面 API Key 保存于当前 Windows 用户的凭据管理器，换电脑或 Windows 用户后需重新填写。复制文件夹不能自动迁移这些密钥。

桌面启动器目前只支持 Microsoft Edge。Chrome 和其他浏览器不纳入桌面包支持范围；普通 Web 入口使用浏览器自己的数据和配置，不能替代桌面配置同步入口。本轮不新增浏览器选择或切换数据功能。

使用、升级、备份与故障处理见 [用户使用说明](FIRST_RUN.md)。

## 快速开始：源码 Web 模式

开发者需要 Git 和 Bun：

~~~bash
git clone https://github.com/tongxuedashu-design/infinite-canvas-desktop.git
cd infinite-canvas-desktop/web
bun install
bun run dev
~~~

访问 http://localhost:3000。开发机编译启动器及准备便携包见 [桌面端维护说明](desktop/README.md)。

普通 Web 模式中，项目、素材和 API Key 默认保存在所用浏览器本地。桌面源码启动则使用项目根目录上一级的 EdgeData/，例如 D:\无限画布\EdgeData；便携包使用自身的 EdgeData/，两者不会自动复制或覆盖。

模型请求通常由前端直接发送到配置的服务；自行开启本地代理后按代理配置转发。服务商可能收到提示词、参考素材和凭据，请只使用可信服务、插件和调用脚本。备份、导出包和浏览器数据目录应按敏感资料保管。

## 文档与许可证

- [用户使用说明](FIRST_RUN.md)
- [发布交接与检查清单](docs/windows-release.md)
- [功能介绍](docs/content/docs/overview/features.zh-CN.mdx)
- [画布节点操作手册](docs/content/docs/canvas/canvas-node-manual.zh-CN.mdx)
- [画布快捷键](docs/content/docs/canvas/canvas-shortcuts.zh-CN.mdx)
- [本地 Canvas Agent（可选，未随便携包启动）](canvas-agent/README.md)
- [待办事项](docs/content/docs/progress/todo.zh-CN.mdx)

代码按 [MIT License](LICENSE) 发布，保留原有 Copyright (c) 2026 basketikun。MIT 允许使用、修改及商业分发，并要求随软件保留版权与许可文本。打包的 Node.js、.NET 和 npm 依赖遵循各自许可证；根目录 MIT 不替代它们的许可要求。归属及分发材料检查见 [发布交接](docs/windows-release.md)。
