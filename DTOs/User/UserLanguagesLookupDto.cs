using System.Collections.Generic;

namespace MeetingBackend.DTOs.User
{
    public class UserLanguagesLookupDto
    {
        public string PreferredLanguage { get; set; } = string.Empty;
        public List<string> Languages { get; set; } = new();
    }
}
