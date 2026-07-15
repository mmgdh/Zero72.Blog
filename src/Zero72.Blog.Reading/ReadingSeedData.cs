namespace Zero72.Blog.Reading;

public static class ReadingSeedData
{
    private static readonly Guid CleanCodeId = Guid.Parse("b2b7a23b-47db-4f4a-8cf9-52225ec22c01");
    private static readonly Guid MythicalManMonthId = Guid.Parse("26eaf43f-22d7-4d7b-81ef-08979ab0fa6b");
    private static readonly Guid ComputerSystemsId = Guid.Parse("4f8e692f-2f36-45c7-aa20-cdff167324a4");
    private static readonly Guid CleanArchitectureId = Guid.Parse("23e7e49a-cf70-4ae6-8859-229d4de2f9a0");
    private static readonly Guid DomainDrivenDesignId = Guid.Parse("51fc3cf8-4546-41c6-aa55-8d58139f3cb0");
    private static readonly Guid HighPerformanceMysqlId = Guid.Parse("b5e1f506-9b3d-4ba3-a958-a57c4f34912b");
    private static readonly Guid WaveId = Guid.Parse("f388523c-1466-46a5-b8c2-4cf1c39103d1");

    public static IReadOnlyList<ReadingBookEntity> CreateBooks()
    {
        return
        [
            NewBook(CleanCodeId, "代码整洁之道", "Robert C. Martin", "midnight"),
            NewBook(MythicalManMonthId, "人月神话", "Frederick P. Brooks Jr.", "paper"),
            NewBook(ComputerSystemsId, "深入理解计算机系统（原书第3版）", "Randal E. Bryant", "linen"),
            NewBook(CleanArchitectureId, "架构整洁之道", "Robert C. Martin", "cosmos"),
            NewBook(DomainDrivenDesignId, "领域驱动设计", "Eric Evans", "sparrow"),
            NewBook(HighPerformanceMysqlId, "高性能 MySQL（第4版）", "Silvia Botros", "sparrow"),
            NewBook(WaveId, "浪潮之巅（第四版）", "吴军", "ocean")
        ];
    }

    public static IReadOnlyList<ReadingRecordEntity> CreateRecords()
    {
        return
        [
            NewRecord(CleanCodeId, "2024-12-28", "第8章 - 第9章：函数", "09:00", "10:30",
            [
                "函数要小，只做一件事，并且把这件事做好。",
                "命名应该反映出它的意图，而不是实现。"
            ]),
            NewRecord(MythicalManMonthId, "2024-12-28", "第3章：系统组织的神话", "21:00", "23:00",
            [
                "管理的真正挑战不在于技术，而在于人。",
                "沟通成本远比想象中更高。"
            ]),
            NewRecord(ComputerSystemsId, "2024-12-27", "第6章：存储层次结构", "20:30", "22:45",
            [
                "局部性原理是计算机系统优化的核心依据。",
                "缓存的存在让速度有了数量级的提升。"
            ]),
            NewRecord(CleanArchitectureId, "2024-12-26", "第4章：组件的边界", "19:00", "20:48",
            [
                "好的架构应该让依赖关系朝向稳定的方向。",
                "组件的独立性是长期可维护性的基础。"
            ]),
            NewRecord(CleanCodeId, "2024-12-25", "第7章：错误处理", "09:00", "10:30",
            [
                "错误处理不应该掩盖意图，它决定了程序的健壮性。"
            ]),
            NewRecord(DomainDrivenDesignId, "2024-12-25", "第2部分：领域分析", "14:00", "16:00",
            [
                "领域模型需要从业务语言出发，而不是技术实现。"
            ]),
            NewRecord(MythicalManMonthId, "2024-12-25", "第4章：估算的神话", "21:00", "22:30",
            [
                "估算本质上是不确定的，关键在于透明沟通。"
            ]),
            NewRecord(HighPerformanceMysqlId, "2024-12-24", "第5章：索引", "20:00", "21:20",
            [
                "索引是空间换时间的典型应用。",
                "理解底层原理，才能更好地做性能优化。"
            ]),
            NewRecord(ComputerSystemsId, "2024-12-24", "第7章：链接", "21:30", "22:30",
            [
                "链接把分散的代码组织成可运行程序。"
            ]),
            NewRecord(WaveId, "2024-12-22", "第2章：英特尔", "10:00", "12:00",
            [
                "技术变革推动产业格局的变化。"
            ]),
            NewRecord(MythicalManMonthId, "2024-12-22", "第5章：进度安排的神话", "20:00", "21:12",
            [
                "进度安排需要考虑资源、沟通和风险。"
            ]),
            NewRecord(HighPerformanceMysqlId, "2024-12-21", "第6章：查询优化器", "22:00", "22:48",
            [
                "查询优化器在背后做了大量工作。",
                "写 SQL 时要站在优化器的角度思考。"
            ]),
            NewRecord(DomainDrivenDesignId, "2024-12-20", "第3部分：领域设计", "20:00", "22:30",
            [
                "领域设计的目标是表达业务的核心概念。",
                "聚合、实体和值对象的划分需要平衡。"
            ])
        ];
    }

    private static ReadingBookEntity NewBook(Guid id, string title, string author, string coverTone)
    {
        return new ReadingBookEntity
        {
            Id = id,
            Title = title,
            Author = author,
            CoverTone = coverTone
        };
    }

    private static ReadingRecordEntity NewRecord(
        Guid bookId,
        string readDate,
        string chapter,
        string startedAt,
        string finishedAt,
        IReadOnlyList<string> reflections)
    {
        var start = TimeOnly.Parse(startedAt);
        var finish = TimeOnly.Parse(finishedAt);

        return new ReadingRecordEntity
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            ReadDate = DateOnly.Parse(readDate),
            Chapter = chapter,
            StartedAt = start,
            FinishedAt = finish,
            DurationHours = Math.Round((decimal)(finish - start).TotalHours, 1),
            Reflections = reflections.ToArray()
        };
    }
}
