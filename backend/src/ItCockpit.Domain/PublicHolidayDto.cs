namespace ItCockpit.Domain;

public class PublicHolidayDto
{
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}

public class NagerHolidayDto
{
    public string Date { get; set; } = string.Empty;
    public string LocalName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}