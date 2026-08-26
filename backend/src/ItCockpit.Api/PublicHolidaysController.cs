using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ItCockpit.Domain;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicHolidaysController : ControllerBase
{
    private readonly IPublicHolidayService _holidayService;

    public PublicHolidaysController(IPublicHolidayService holidayService)
    {
        _holidayService = holidayService;
    }

    [HttpGet("{year}")]
    public async Task<IActionResult> GetHolidays(int year)
    {
        var holidays = await _holidayService.GetPublicHolidaysAsync(year);
        return Ok(holidays);
    }
}