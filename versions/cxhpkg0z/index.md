---
url: /versions/cxhpkg0z/index.md
---
# UniCon 版本更新记录

## \[v1.4.0] - 2026-05-16

### 更新内容

* **通讯契约重构 (Core Refactor)**:
  * 引入了 **结构化请求与响应模型** (`UniconRequest`, `UniconResponse`)，模拟 HTTP 设计思路，支持 Headers、Parameters 和 Body。
  * 统一了所有驱动的调用接口，能够灵活应对需要额外元数据的复杂通讯协议。
* **批量操作支持 (Batch Operations)**:
  * `IUniconDriver` 接口新增 `ReadBatchAsync` 和 `WriteBatchAsync` 方法。
  * `DriverBase` 提供了默认的循环读取实现，驱动子类可根据底层协议（如 Modbus 批量读、S7 多变量读）进行高性能重写。
* **架构一致性**:
  * `CommunicationJob` 与 `CommunicationService` 均已适配新的结构化契约。
  * 增强了 API 响应信息，Web 层现在可以返回包含协议级错误详情的完整响应结构。

### 与上一版本区别

* **灵活性**: 摆脱了只能传递单一 `address` 字符串的束缚，现在可以随请求传递认证信息、超时时间、槽位等任意扩展参数。
* **吞吐量**: 具备了批量处理能力，能够显著减少网络往返次数，提升大规模数据采集的效率。
* **标准化**: 将工业通讯抽象为类 Web 的 Request/Response 模式，极大降低了集成第三方异构系统的难度。
