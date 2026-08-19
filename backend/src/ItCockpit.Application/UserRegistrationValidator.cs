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
                return (false, "Password cannot be empty.");
            }

            if (password.Length < 8)
            {
                return (false, "Password must be at least 8 characters long.");
            }

            if (!password.Any(char.IsUpper))
            {
                return (false, "Password must contain at least one uppercase letter.");
            }

            bool hasNumber = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            if (!hasNumber && !hasSpecial)
            {
                return (false, "Password must contain at least one number or special character.");
            }

            return (true, string.Empty);
        }
    }
}
