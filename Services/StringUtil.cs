using System.Globalization;
using System;
using System.Collections.Generic;
using System.Text;

namespace AppCore.Services
{
    public class StringUtil
    {
        public static string NormalizeString(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            return string.Concat(
                input.Normalize(NormalizationForm.FormD)
                     .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            ).ToLowerInvariant().Trim();
        }
        
        public static string GeneratePassword(string email, string nombre, string apellido)
		{
			string password = string.Empty;
			Random random = new Random();
			// Verifica si el email es válido (contiene un @ y tiene texto antes y después del @)
			if (!IsValidEmail(email))
			{
				string emailPrefix = email.Split('@')[0];
				// Si el prefijo tiene más de 3 caracteres, se recorta a los primeros 3
				if (emailPrefix.Length > 3)
				{
					emailPrefix = emailPrefix.Substring(0, 3);
				}

				int randomNumber = random.Next(100, 1000); // 3 dígitos
				password = $"{emailPrefix}{randomNumber}";
			}
			else
			{
				string firstNameInitial = !string.IsNullOrWhiteSpace(nombre) ? nombre.Substring(0, 1).ToUpper() : "X";
				string lastName = !string.IsNullOrWhiteSpace(apellido) ? apellido.Split(' ')[0].ToUpper() : "Y";

				int randomNumber = random.Next(10, 100);
				password = $"{firstNameInitial}{lastName}{randomNumber}";
			}
			return password;
		}

		public static string GenerateNickName(string nombres, string apellidos)
		{
			Random random = new Random();
			const string chars = "abcdefghijklmnopqrstuvwxyz0123456789"; // Caracteres a utilizar
			string letters = new string(Enumerable.Range(0, 5) // Generar 5 letras aleatorias
				.Select(_ => chars[random.Next(chars.Length)])
				.ToArray());

			int randomNumber = random.Next(10, 100); // Generar un número aleatorio entre 10 y 99
			string nickname = $"{letters}{randomNumber}"; // Concatenar letras y número

			return nickname.ToLower(); // Retornar en minúsculas
		}


		public static bool IsValidEmail(string email)
		{
			return !string.IsNullOrWhiteSpace(email) && email.Contains("@") && email.IndexOf("@") > 0 && email.IndexOf("@") < email.Length - 1;
		}
		public static List<string> GetNombres(string cadena)
		{
			var nombres = cadena.Split(' ').ToList();
			if (nombres.Count < 2)
			{
				nombres.Add(null);
			}

			return nombres;
		}
		public static string GenerateRandomPassword(int length = 8)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			var random = new Random();
			var password = new StringBuilder();

			for (int i = 0; i < length; i++)
			{
				password.Append(chars[random.Next(chars.Length)]);
			}

			return password.ToString();
		}
    }
}