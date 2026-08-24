using System;
using System.Linq;

namespace ItCockpit.Application
{
    public class UserRegistrationValidator
    {
        public static (bool IsValid, string ErrorMessage) ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Parola boş olamaz.");
            }

            var errors = new System.Collections.Generic.List<string>();

            if (password.Length < 8)
                errors.Add("en az 8 karakter");

            if (!password.Any(char.IsUpper))
                errors.Add("en az bir büyük harf");

            if (!password.Any(char.IsDigit))
                errors.Add("en az bir rakam");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                errors.Add("en az bir noktalama işareti (örn: !, @, #, .)");

            if (errors.Count > 0)
            {
                var message = "Parola şu gereksinimleri karşılamıyor: " + string.Join(", ", errors) + ".";
                return (false, message);
            }

            return (true, string.Empty);
        }
    }
}
