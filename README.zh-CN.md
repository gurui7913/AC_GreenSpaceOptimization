# Comfort in Sight：绿地布局优化

Rui Gu · UCL Bartlett · Architectural Computation · BARC0034 Morphogenetic Programming

[English](README.md) · [项目展板](docs/boards/MorphoGenetic.pdf) · [审核记录](docs/AUDIT.md)

本仓库整理了课程中的 Rhino / Grasshopper 定义、C# 源码导出、最终提交资料，以及独立的 Python NSGA-II 示例。项目以广东的一处居住区为背景，探索绿地可视性与面积目标之间的权衡。

[![历史展板中的优化前后布局与候选方案对比](docs/images/results-board.jpg)](docs/images/results-board.jpg)

*原始展板展示了场地优化前后布局及候选方案对比。这些是历史展示结果，当前 Python 示例采用独立的合成网格。[查看完整 PDF](docs/boards/MorphoGenetic.pdf)。*

## 项目可视化

### 从场地建模到优化流程

下图把绿地边界处理、建筑排除、路径采样、网格编码和双目标优化串联起来，右侧展示可视性评价与布局演化逻辑。

[![场地处理、目标定义与布局适应流程](docs/images/methodology-board.jpg)](docs/images/methodology-board.jpg)

<details>
<summary>展开查看 NSGA-II 算法展板</summary>

原始算法展板展示了非支配排序、选择策略、染色体表示、交叉与变异。导出源码的实际状态请结合审核报告阅读。

[![NSGA-II 算法、基因编码及遗传算子](docs/images/algorithm-board.jpg)](docs/images/algorithm-board.jpg)

</details>

*预览图直接来自 `Final_Project/Boards/Export/MorphoGenetic.pdf`。点击图片可查看 2400 像素版本；[图示来源](docs/images/README.md)记录了原 PDF 页码。*

## 从哪里开始

- 看设计思路：[完整展板](docs/boards/MorphoGenetic.pdf)、[提交版展板](docs/boards/)、[演示文稿](docs/presentations/)。
- 运行优化示例：`src/python/green_space_optimization.py`，使用合成的 5 × 6 网格，无需 Rhino 或 GPU。
- 查看最终空间模型：[grasshopper/final-submission/](grasshopper/final-submission/)。同一目录中的 GH 文件应配合该目录的 Rhino 模型使用。
- 阅读算法：[src/grasshopper/00_NSGA-II/](src/grasshopper/00_NSGA-II/)。这些是保留原貌的历史源码，有已确认的问题，详见审核记录。
- 查看平时作业：[coursework/](coursework/)，保留 A1–A4 学生代码；教学任务书仍保留在本地原目录。

## 两套实现的区别

| 项目 | Grasshopper / C# | Python |
| --- | --- | --- |
| 输入 | 绿地边界、建筑轮廓、步行路径 | 合成矩形网格 |
| 编码 | 布尔基因 | 连续变量，用 `x > 0.5` 转成绿格 |
| 目标 | 最大化射线绿视率；最小化面积差值与区域惩罚之和 | 覆盖率、目标比例偏差、区域不均衡、孤立格比例四个目标 |
| 默认参数 | 种群 100、50 代、格长 5、目标面积 6476、30 条射线、射线半径 100 | 种群 100、300 代、seed=1、30 格、6 列、3 个区域、目标比例 0.5 |
| 当前验证情况 | 提取出的优化器已复现初始化越界；未在 Rhino 中验证 | 完整默认运行已通过 |

C# 参数取自主导出文件 `C_-1f522.cs`，不同 GH 定义与展板中的设置可能不同。Python 的第一目标是**绿地覆盖率**，没有观察点、射线或建筑遮挡，不能称为真实场地绿视率的复现。孤立格惩罚也不保证整个绿地形成一个连通区域。

## 运行 Python

验证环境：Python 3.10、NumPy 2.2.6、pymoo 0.6.1.6。`requirements.txt` 固定直接依赖版本，间接依赖由 pip 解析。

```powershell
git clone https://github.com/gurui7913/AC_GreenSpaceOptimization.git
cd AC_GreenSpaceOptimization
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
$env:PYTHONIOENCODING = "utf-8"
.\.venv\Scripts\python.exe "src/python/green_space_optimization.py"
```

脚本输出每代进度、部分 Pareto 解、折中解指标和字符网格。折中解按归一化后的目标向量到原点的欧氏距离选取。脚本不会保存数据文件或生成 CAD 几何。

当前默认运行结果见[审核记录](docs/AUDIT.md)。结果仅说明合成网格示例的运行情况，不证明真实场地的改善幅度。

修改参数需直接编辑脚本。末尾可视化固定使用 `reshape(5, 6)`，打印分母也固定为 30；改变网格大小时须同步调整。导入脚本会立即开始优化。

## 打开 Grasshopper 项目

1. 在 Rhino 中打开 `grasshopper/final-submission/Morpho_Project.3dm`。
2. 在 Grasshopper 中打开同目录的 `OptimizeGreenlandLayout_C#.gh`。
3. 检查几何引用、文档单位与组件输入，必要时重新指定曲线引用；先检查错误信息再启用优化。
4. 对照[源码审核](docs/AUDIT.md)，了解历史实现的问题。本次没有验证 GH 内嵌代码与导出源码是否完全一致，也没有完成 Rhino 几何运行。

提交目录中的两个 GH 文件字节相同，`ResultGenerated` 文件名不能证明其中另有生成结果。旧 `GreenSpaceOptimization_GH/` 包已从当前仓库移除，原文件保留在本地作业资料与 Git 历史中；当前请使用最终提交版。

历史 `.csproj` 保留了本机 Rhino DLL 绝对路径与 `net452` / C# 5 配置；源码还存在空包装器、重复类或不完整草稿，不能作为已验证的独立 C# 工程直接编译。`11/` 为另一份历史优化器导出；`00_GeneticAlgorithm/` 为不完整实验草稿。本次整理没有修改算法内容。

## 整理与来源

- `src/grasshopper/`：最终项目的 C# 源码和原工程配置。
- `grasshopper/final-submission/`：最终提交的 GH / Rhino 文件。
- `docs/boards/`、`docs/presentations/`、`docs/images/`：最终展示资料和选用图示。
- `docs/source-map.csv`：导入文件的原相对路径、仓库路径、大小和 SHA-256。
- `coursework/`：A1–A4 的代码与 GH 定义。

本地原文件完整保留。教学任务书、第三方教程、毕业论文资料、AI / PSD / INDD 编辑稿、旧版文件、缓存和备份未纳入此次公开仓库。未擅自添加开源许可证。

此前 README 中的 `+28%` 绿视率和 `+20%` 效率提升缺少仓库内可复核的计算记录，已不再作为验证结论展示。历史展板内容保留，但不等同于重新验证过的实验结果。
