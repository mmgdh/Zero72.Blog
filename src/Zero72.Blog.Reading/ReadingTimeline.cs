using System.Globalization;

namespace Zero72.Blog.Reading;

public static class ReadingTimeline
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    public static IReadOnlyList<ReadingTimelineDay> FromRecords(IEnumerable<ReadingRecord> records)
    {
        return records
            .GroupBy(record => record.ReadDate)
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var dayRecords = group
                    .OrderBy(record => record.StartedAt)
                    .ThenBy(record => record.BookTitle)
                    .ToList();

                var insights = dayRecords
                    .SelectMany(record => record.Reflections)
                    .Where(reflection => !string.IsNullOrWhiteSpace(reflection))
                    .Take(4)
                    .ToList();

                return new ReadingTimelineDay(
                    group.Key,
                    GetWeekday(group.Key),
                    dayRecords,
                    insights);
            })
            .ToList();
    }

    private static string GetWeekday(DateOnly date)
    {
        return ChineseCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
    }
}
