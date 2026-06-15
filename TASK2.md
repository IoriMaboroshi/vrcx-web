# VRCX Server - New Features (参照VRCX源码)

## 项目位置
- 前端源码: /workspace/vrcx-server/frontend-src/src/
- 后端源码: /workspace/vrcx-server/VrcxServer/
- VRCX参考: /workspace/vrcx-server/vrcx-reference/

## 任务1: 删除搜索页的Avatar搜索
Search.vue中去掉avatar搜索tab，只保留Users和Worlds。

## 任务2: 世界搜索结果可点击 + 实例详情
当前世界搜索结果是不可点击的。需要：
- 搜索结果点击后显示世界详情（名称、描述、容量、作者）
- 显示该世界的实例列表（房间号、在线人数、实例类型）
- 参照VRCX的api_world.js和store_world.js

后端需要添加：
- GET /api/worlds/{worldId}/instances 获取世界实例列表
- VRChat API: worlds/{worldId} 返回实例信息

## 任务3: 好友分组/收藏功能
参照VRCX的favoriteStore：
- 好友可以分组（收藏夹）
- Dashboard显示收藏好友的在线状态
- 前端添加收藏按钮

## 任务4: 群组功能（基础）
参照VRCX的groupStore：
- 显示用户加入的群组列表
- 群组成员列表

## 注意
- 只修改文件不要build
- 参照vrcx-reference/下的VRCX源码实现
- 前端用Vue 3 Composition API

