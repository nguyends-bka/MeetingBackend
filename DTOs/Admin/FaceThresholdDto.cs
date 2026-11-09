namespace MeetingBackend.DTOs.Admin;

public class FaceThresholdDto
{
    public double Threshold { get; set; }
    public double DefaultThreshold { get; set; }
    public double MinThreshold { get; set; }
    public double MaxThreshold { get; set; }
}

public class UpdateFaceThresholdRequestDto
{
    public double? Threshold { get; set; }
}
