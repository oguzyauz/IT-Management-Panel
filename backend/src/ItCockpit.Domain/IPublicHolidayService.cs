using System.Collections.Generic;
using System.Threading.Tasks;

namespace ItCockpit.Domain;

public interface IPublicHolidayService
{
    Task<List<PublicHolidayDto>> GetPublicHolidaysAsync(int year);
}