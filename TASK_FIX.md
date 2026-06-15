# VRCX Server - 全面修复+功能完善

## 项目位置
- 后端: /workspace/vrcx-server/VrcxServer/
- 前端: /workspace/vrcx-server/frontend-src/src/
- VRCX参考: /workspace/vrcx-server/vrcx-reference/

## 关键BUG修复（必须全部修复）

### BUG1: 图片代理路由被SPA fallback拦截
当前路由 `/api/image-proxy/{encodedUrl}` 用base64路径参数，但前端api.js的proxyImage函数用 `?url=xxx` 查询参数。两者不匹配，请求落到SPA fallback返回index.html。
**修复**: 改Program.cs的路由为 `app.MapGet("/api/image-proxy", async (string url, ...) => {...})` 使用查询参数。确保在MapFallbackToFile之前。

### BUG2: 头像全部显示不出来
原因：图片代理路由不匹配（见BUG1）。

### BUG3: 状态颜色 - 4种状态
- join me → 蓝色(#2196f3)
- active → 绿色(#4caf50)  
- ask me → 黄橙色(#ff9800)
- busy → 红色(#f44336)
在App.vue全局style中设置。

### BUG4: 平台显示重复emoji "🥽 🥽 PC (VR)"
检查FriendsView和FriendDetailView的模板，确保platformIcon只调用一次。

### BUG5: 世界名称没有解析
FriendService.ResolveWorldNameAsync需要正常工作。

### BUG6: 世界图片显示不出来
Search.vue中世界图片需要用api.proxyImage()包装。

### BUG7: 世界详情显示"未找到活跃实例"
后端instances API需要正确返回VRChat世界的实例数据。

## 功能完善

### 1. 搜索页
- 删除Avatar搜索tab
- 世界搜索结果可点击，显示详情弹窗（名称、描述、作者、容量、图片、实例列表）

### 2. 好友收藏
- localStorage持久化
- Dashboard显示收藏好友

### 3. 世界实例
- 显示类型、人数、区域

## 注意
- 只修改文件不要build
- 参照vrcx-reference/源码
- 确保所有img src都通过api.proxyImage()
