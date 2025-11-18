using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LayoutsFromModel
{
    /// <summary>
    /// Вспомогательный класс для работы сравнений
    /// </summary>
    internal class CompareHelper
    {
        /// <summary>
        /// Метод сортировки номеров листов
        /// </summary>
        /// <param name="input">Коллекция листов в строковом виде</param>
        /// <returns>Отсортированную коллекцию листов в строковом виде</returns>
        public static string Alphanumeric(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Разделяем число и буквенный суффикс
            Match match = Regex.Match(input, @"^(\d+)([а-яёa-z]*)$");
            if (match.Success)
            {
                string numberPart = match.Groups[1].Value.PadLeft(10, '0');
                string letterPart = match.Groups[2].Value;
                
                return numberPart + letterPart; // Буквы остаются как есть
            }
            
            return input; // Если не соответствует шаблону
        }

        /// <summary>
        /// Метод сортировки номеров листов (stackoverflow)
        /// © https://stackoverflow.com/questions/5093842/alphanumeric-sorting-using-linq
        /// </summary>
        /// <param name="input">Коллекция листов в строковом виде</param>
        /// <returns>Отсортированную коллекцию листов в строковом виде</returns>
        public static string AlphanumericCompare(string input)
        {
            return Regex.Replace(input, "[0-9.]+", match => match.Value.PadLeft(10, '0'));
        }
    }
}
