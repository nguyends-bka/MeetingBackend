namespace MeetingBackend.DTOs.Admin;

public class AnalyticsSummaryDto
{
    public int TotalMeetings { get; set; }
    public int LiveMeetings { get; set; }
    public int EndedMeetings { get; set; }
    public int UpcomingMeetings { get; set; }
    public int CancelledMeetings { get; set; }
    public int TotalParticipants { get; set; }
    public int TotalUsers { get; set; }
    public int TotalRecordings { get; set; }
    /// <summary>Thời lượng trung bình (phút) của cuộc họp đã kết thúc.</summary>
    public double AvgDurationMinutes { get; set; }
    /// <summary>Số cuộc họp tạo trong khoảng thời gian đang xét.</summary>
    public int MeetingsInRange { get; set; }
    /// <summary>Số người dùng mới trong khoảng thời gian đang xét.</summary>
    public int NewUsersInRange { get; set; }
}

public class TimeSeriesPointDto
{
    public string Date { get; set; } = string.Empty; // yyyy-MM-dd
    public int Meetings { get; set; }
    public int NewUsers { get; set; }
}

public class StatusSliceDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopHostDto
{
    public string HostIdentity { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int MeetingCount { get; set; }
}

public class HourBucketDto
{
    public int Hour { get; set; } // 0..23
    public int Count { get; set; }
}

public class AnalyticsResponseDto
{
    public int RangeDays { get; set; }
    public AnalyticsSummaryDto Summary { get; set; } = new();
    public List<TimeSeriesPointDto> Series { get; set; } = [];
    public List<StatusSliceDto> StatusBreakdown { get; set; } = [];
    public List<TopHostDto> TopHosts { get; set; } = [];
    public List<HourBucketDto> HourDistribution { get; set; } = [];
}
