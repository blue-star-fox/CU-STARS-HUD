# CU Stats HUD 使用统计

> 本文件由 GitHub Actions 每天自动更新，数据全部来自 GitHub 公开接口（Release 下载量、Star、Fork），**不涉及任何用户隐私**。

> 最后更新：等待首次自动运行（或手动触发 `使用统计` workflow）

## 仓库

| 指标 | 数值 |
|---|---|
| Star | — |
| Fork | — |
| Watch | — |
| 未关闭 Issue | — |

## 累计下载

**—** 次

（首次运行 workflow 后这里会自动填充各 Release 资源与下载量）

---

### 这些数字说明什么

- 下载量反映的是**发布包被取走的次数**，不等于活跃玩家数（重复下载、CI 拉取都会计入）。
- 这里全部是 GitHub 的公开数据；插件**不做任何联网上报**，因此没有游玩时长、功能使用频率这类统计。
- 插件也不会写入本仓库——那需要在客户端内置有写权限的 Token，反编译即可拿到并篡改仓库；
  所以这份统计完全由仓库侧的 Actions 生成。

### 如何手动刷新

在仓库的 **Actions** 页面选择 `使用统计` workflow，点 **Run workflow** 即可立刻更新一次
（生成逻辑在 [`tools/gen_stats.py`](../tools/gen_stats.py)，可以本地用任意 JSON 跑）。
