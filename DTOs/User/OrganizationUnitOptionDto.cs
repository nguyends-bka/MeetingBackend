namespace MeetingBackend.DTOs.User;

public class OrganizationUnitOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}
