# BioInc-AA 自行上传指南

> 当前进度（已完成，无需重复）：
> ✅ git 已配置（身份：逐梦之子-晓夕 / wsxxx_2021@qq.com）
> ✅ 本地仓库已建好，3个提交已就位，标签 v1.3.0 已打在最新提交上
> ✅ GitHub 远程仓库 DRXiaoXi/BioInc-AA 已创建（Public、空仓库）
> ✅ GitHub 认证已通过（凭据存在 Windows 凭据管理器里）
>
> **只剩两步：推送代码 → 发布 Release**

---

## 第一步：推送代码到 GitHub

### 方法A：GitHub Desktop（推荐，纯点鼠标）

1. 打开 **GitHub Desktop**
2. 如果显示欢迎页：点 **Sign in to GitHub.com** → 浏览器会打开并显示已登录 DRXiaoXi → 点绿色 **Continue/Authorize** → 浏览器问"打开 Electron？"点 **打开**
   （之前已授权过，这次会很快）
3. 登录后：菜单 **File → Add local repository...** → 点 **Choose...** 选文件夹
   `D:\BioInc-AA`
   （如提示 "This directory appears to be a git repository"，点 **Add this repository** 确认即可）
4. 左上角仓库选中后，点顶部 **Push origin** 按钮（可能显示 "Push 3 commits"）
5. 等待推送完成（右上角小圈转完）

### 方法B：命令行（一条命令）

打开 CMD：
```
cd /d "D:\BioInc-AA"
git push -u origin main
```
> 注：git 已配置走本机代理 127.0.0.1:7897（Clash Verge），推送时保持 Clash 开启。
> 如果卡住不动，Ctrl+C 后重跑一次即可（代理连接偶发中断）。

推送成功后刷新 https://github.com/DRXiaoXi/BioInc-AA 就能看到全部代码。

## 第二步：发布 v1.3.0 Release（自动出安装包）

1. 打开 https://github.com/DRXiaoXi/BioInc-AA/releases/new
2. **Choose a tag** 输入框里输入 `v1.3.0` → 出现提示后选 **Create new tag: v1.3.0 on main**
3. Title 填 `BioInc AA v1.3.0`，描述随意（如：首个发布版本）
4. 点绿色 **Publish release**

发布后 GitHub Actions 会自动：构建插件 → 打包 `BioIncModSetup.zip` → 挂到这个 Release 下。
进度看 https://github.com/DRXiaoXi/BioInc-AA/actions （构建约1-2分钟）。
完成后 Release 页面就有 `BioIncModSetup.zip` 供下载，即为对外分发安装包。

> 若 Actions 没有自动运行：进 Actions 页 → 左侧 Build & Release → **Run workflow** 手动跑一次，
> 跑完后回到 Release 页刷新即可看到附件。

## 以后更新版本的流程

1. 改代码（`BioIncAA/Plugin.cs` 等），构建确认没问题
2. GitHub Desktop：左侧会自动列出改动 → 填一句提交说明 → **Commit to main** → **Push origin**
3. 网页上发新 Release：tag 输入新版本号（如 v1.3.1，选在 main 上新建）→ Publish
4. CI 自动出对应安装包

## 注意事项

- **不要**把 `decompiled/`（游戏反编译源码）传上去——.gitignore 已排除，别手动绕过
- `BioIncAA/lib/` 里的 DLL 是 CI 构建必需的引用，保持提交状态
- 推送需要 Clash Verge 运行（git 走 127.0.0.1:7897）；换代理端口的话执行：
  `git config --global http.proxy http://127.0.0.1:新端口`（https.proxy 同理）
- 仓库地址：https://github.com/DRXiaoXi/BioInc-AA
