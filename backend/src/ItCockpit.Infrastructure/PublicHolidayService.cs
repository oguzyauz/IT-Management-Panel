using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ItCockpit.Domain;

namespace ItCockpit.Infrastructure;

public class PublicHolidayService : IPublicHolidayService
{
    private readonly HttpClient _httpClient;

    public PublicHolidayService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PublicHolidayDto>> GetPublicHolidaysAsync(int year)
    {
        try
        {
            // Nager.Date ücretsiz API adresi
            var url = $"https://date.nager.at/api/v3/PublicHolidays/{year}/TR";

            var response = await _httpClient.GetFromJsonAsync<List<NagerHolidayDto>>(url);

            if (response == null) return new List<PublicHolidayDto>();

            // Dışarıdan gelen veriyi, kendi kullanacağımız sade modele çeviriyoruz
            return response.Select(h => new PublicHolidayDto
            {
                Title = h.LocalName,
                Date = h.Date
            }).ToList();
        }
        catch (Exception)
        {
            // Eğer internet koparsa veya API yanıt vermezse takvim patlamasın diye boş liste dönüyoruz
            return new List<PublicHolidayDto>();
        }
    }
}