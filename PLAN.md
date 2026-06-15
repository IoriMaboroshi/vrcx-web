# VRCX Server — Headless 前后端分离方案

> **目标**：将 VRCX（Electron 桌面应用）改造为 headless 服务端 + Web 前端架构，
> 部署在 Linux 服务器上，通过浏览器访问完整 VRCX 功能。

## 架构总览

```
浏览器 (任意设备)
    │
    │  HTTP REST API + WebSocket
    │
.NET Headless Server (Docker)
    ├── Kestrel HTTP Server (替代 Electron IPC)
    │   ├── /api/dotnet/{class}/{method}  ← 通用代理
    │   ├── /api/vrchat/*                 ← VRChat API 代理
    │   └── /api/auth                     ← 登录/2FA
    ├── WebSocket Server
    │   ├── /ws/vrchat     ← 转发 VRChat pipeline 事件
    │   └── /ws/vrcx       ← VRCX 内部事件推送
    ├── .NET 核心模块 (复用)
    │   ├── WebApi.cs       (VRChat HTTP 客户端)
    │   ├── AppApi*.cs      (精简版，去掉 Windows 特有)
    │   ├── SQLite 数据库
    │   └── Cookie/会话管理
    └── 静态文件服务
        └── Vue.js 前端 (dist/)
```

## 改造原则

1. **最大复用**：直接引用 VRCX 的 .NET 源码，不重写
2. **最小改动**：前端只改 IPC 调用层（1个文件）
3. **砍掉不需要的**：Windows 注册表、系统托盘、VR Overlay、游戏进程检测、命名管道
4. **Docker 部署**：一键启动，数据持久化

## 前端 Vue.js 改动

### 唯一需要改的文件：`src/services/request.js`

现有逻辑：
```javascript
// 通过 Electron IPC 调用 .NET
window.interopApi.callDotNetMethod(className, methodName, args)
```

改为：
```javascript
// 通过 HTTP 调用后端
fetch(`/api/dotnet/${className}/${methodName}`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(args)
})
```

### WebSocket 连接

前端已有直接连 VRChat WebSocket 的代码。改造后：
- **方案 A（推荐）**：前端直连 VRChat WebSocket（保持现有逻辑，需浏览器能访问外网）
- **方案 B**：后端代理 WebSocket（浏览器连后端，后端连 VRChat，适合内网场景）

初期采用方案 A，后续按需加方案 B。

## .NET 后端改造

### 新增文件

```
VrcxServer/
├── Program.cs              # ASP.NET Minimal API 入口
├── VrcxServer.csproj       # 项目文件
├── Controllers/
│   └── DotnetProxyController.cs   # 通用 .NET 方法代理
├── Services/
│   ├── DotnetInteropService.cs    # 封装现有 AppApi 调用
│   └── VrchatWebSocketService.cs  # VRChat WebSocket 管理
├── Middleware/
│   └── CorsMiddleware.cs          # 跨域支持
└── Dockerfile
```

### API 端点设计

```
POST /api/dotnet/{className}/{methodName}
Body: [arg1, arg2, ...]
Response: { success: true, result: ... }

GET  /api/config
GET  /api/status
POST /api/auth/login
POST /api/auth/2fa
WS   /ws/vrchat
WS   /ws/vrcx
```

### 砍掉的模块

| 模块 | 原因 |
|------|------|
| RegistryPlayerPrefs | Windows 注册表 |
| GameHandler | 游戏进程检测 |
| OverlayServer | VR 叠加层 |
| IPCServer | 命名管道 |
| Notification (系统) | 系统通知 |
| Update | 自动更新 |

### 保留的模块

| 模块 | 用途 |
|------|------|
| WebApi | VRChat API HTTP 代理 |
| AppApi (文件夹相关) | 路径查询 |
| SQLite | 数据持久化 |
| LogWatcher | 可选，游戏日志 |
| Discord | Discord Rich Presence |

## 实施步骤

### Phase 1: 项目脚手架
- [x] 创建项目目录结构
- [ ] Clone VRCX 源码作为 reference
- [ ] 创建 .NET 项目骨架
- [ ] 配置 Dockerfile

### Phase 2: .NET HTTP Server
- [ ] 实现 Kestrel HTTP 服务器
- [ ] 实现通用 .NET 方法代理 (`/api/dotnet/{class}/{method}`)
- [ ] 集成现有 WebApi.cs (VRChat API 代理)
- [ ] 实现登录/2FA 流程
- [ ] SQLite 数据库初始化

### Phase 3: 前端适配
- [ ] Clone VRCX 前端源码
- [ ] 修改 `request.js` IPC 调用层
- [ ] 构建 Vue.js 静态文件
- [ ] 配置静态文件服务

### Phase 4: WebSocket
- [ ] 实现 VRChat WebSocket 代理（可选）
- [ ] 实现 VRCX 内部事件推送

### Phase 5: Docker & 测试
- [ ] 编写 Dockerfile
- [ ] 编写 docker-compose.yml
- [ ] 端到端测试：登录 → 好友列表 → 实时事件

## 技术栈

- **后端**: ASP.NET 9 Minimal API (C#)
- **前端**: Vue.js 3 + Vite (复用 VRCX 现有代码)
- **数据库**: SQLite (复用 VRCX 现有 schema)
- **部署**: Docker + docker-compose
- **通信**: REST API + WebSocket

## 风险

1. .NET 版本兼容性 — VRCX 用 .NET 9，需确保 Docker 镜像支持
2. node-api-dotnet 依赖 — Electron 专用的 .NET/Node 互操作库不可用，需改用 HTTP
3. Cookie 会话 — VRChat 的认证 Cookie 需要在服务端管理
4. 前端构建 — Vite 构建可能需要 Node.js 环境
