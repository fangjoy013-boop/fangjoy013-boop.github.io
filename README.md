# Joy 的博客 · 技术笔记

这是一个纯 HTML/CSS 博客起点，无需安装依赖，无需构建。仓库包含 GitHub Pages 自动部署工作流。

## 打开博客

双击 `index.html`，用 Chrome 或 Edge 打开。文章链接可直接访问。用记事本打开文件进行编辑，保存为 UTF-8，再刷新浏览器查看修改。

## 文件说明

- `index.html`：首页、笔记列表和关于简介。
- `styles.css`：配色、字号、布局和手机适配。
- `posts/first-page.html`：明确标注的示例笔记，可直接替换为自己的内容。

## 修改内容

1. 在 `index.html` 中修改博客介绍和关于简介。
2. 在 `posts/first-page.html` 中修改文章标题、描述和正文；同时更新首页对应文章的标题与摘要。
3. 将示例内容替换为自己的文章后，可移除两处“示例文章”标记。
4. 修改 `styles.css` 顶部的 `:root` 变量可以统一更换配色。

## 新增笔记

1. 复制 `posts/first-page.html`，在同一目录中另存为英文文件名，例如 `css-layout.html`。
2. 修改新文件的 `<title>`、description、文章标题、标签和正文。
3. 在首页复制整个 `<article class="note-entry">…</article>` 区块。
4. 将新增条目的链接改为 `posts/css-layout.html`，并更新标题、摘要和标签。
5. 更新“01 篇”计数。阅读时间为手动估计值，也可删除。

文件名和链接大小写应一致。文章里的 `../styles.css` 表示使用上一层的公共样式。正文中展示 HTML 代码时，把 `<` 写成 `&lt;`、`>` 写成 `&gt;`、`&` 写成 `&amp;`。

## 自动发布

仓库的 Settings → Pages → Source 应设置为 **GitHub Actions**。

`.github/workflows/deploy.yml` 在每次推送到 `main` 分支后运行，也支持在 Actions 页通过 **Run workflow** 手动触发。工作流检查并打包网页，再部署到 GitHub Pages。只发布 `index.html`、`styles.css` 和 `posts/`，不会把修改说明或工作流文件放进网站。

不需要自行创建 PAT、部署密钥或仓库 Secret；工作流使用 GitHub 自动提供的临时凭据。打包任务仅有代码读取权限，部署任务使用 Pages 发布和身份验证权限。

## 发布前检查

在电脑和窄窗口中检查首页、文章、返回链接和文字。发布文件为 `index.html`、`styles.css` 和 `posts/`，本地无需 Node.js 或构建工具。README 是修改说明，不是网页页面。新增网站资源时，请同时更新工作流中的复制命令。

当前没有评论、搜索、订阅或后台编辑功能。所有内容直接在 HTML 文件中维护。
