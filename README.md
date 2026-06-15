# VRCX Server — Headless VRChat Companion

无头 VRCX 服务器版本，无需 Electron/GUI，通过浏览器访问。

## 快速部署

### 方式一：Docker（推荐）

```bash
# 克隆项目
git clone <repo-url> vrcx-server
cd vrcx-server/VrcxServer

# 构建并启动
docker build -t vrcx-server .
docker run -d \
  --name vrcx-server \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  --restart unless-stopped \
  vrcx-server

# 或使用 docker-compose
docker compose up -d
```

访问 `http://your-server:8080`

### 方式二：本地运行（需要 .NET 9 SDK）

```bash
cd VrcxServer
dotnet restore
dotnet run
```

## 功能

- ✅ VRChat 登录（用户名/密码 + 2FA）
- ✅ 好友列表（头像、状态、位置、平台）
- ✅ 实时 WebSocket 推送（好友上下线通知）
- ✅ 通知面板
- ✅ 暗色主题（VRChat 风格）
- ✅ 响应式布局（手机可用）
- ✅ SQLite 数据持久化

## 架构

```
浏览器 → HTTP/WebSocket → .NET Server → VRChat API
```

- **前端**：Vue 3 + Vite（静态文件）
- **后端**：ASP.NET 9 Minimal API
- **数据库**：SQLite
- **通信**：REST API + WebSocket

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | /api/auth/login | VRChat 登录 |
| POST | /api/auth/2fa | 两步验证 |
| GET | /api/friends | 好友列表 |
| GET | /api/friends/{id} | 好友详情 |
| GET | /api/worlds/{id} | 世界信息 |
| GET | /api/notifications | 通知 |
| GET | /api/friend-events | 事件记录 |
| WS | /ws | 实时事件推送 |

## 开发

```bash
# 前端开发（热重载）
cd frontend-src
npm install
npm run dev

# 后端开发
cd VrcxServer
dotnet run
```

## 配置

环境变量：
- `ASPNETCORE_URLS`：监听地址（默认 `http://+:8080`）
- `DataDirectory`：数据目录（默认 `/app/data`）
- `Logging__LogLevel__Default`：日志级别

## 与原版 VRCX 的区别

| 功能 | 原版 VRCX | VRCX Server |
|------|-----------|-------------|
| 运行环境 | Windows/macOS/Linux 桌面 | Docker/Linux 服务器 |
| GUI | Electron 窗口 | 浏览器 |
| 好友监控 | ✅ | ✅ |
| 实时通知 | ✅ | ✅ |
| VR Overlay | ✅ | ❌ |
| 游戏日志 | ✅ | ❌（可选） |
| Discord RP | ✅ | ❌（可选） |
| Windows 特有 | ✅ | ❌ |

## License

MIT
