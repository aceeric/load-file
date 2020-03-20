using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace load_file
{
    /// <summary>
    /// Various extension methods
    /// </summary>

    static class Extensions
    {
        /// <summary>
        /// Count occurrences of a character in a string
        /// </summary>
        /// <param name="this">The string to search</param>
        /// <param name="ToFind">The character to search for</param>
        /// <returns></returns>
        public static int CountOf(this string @this, char ToFind)
        {
            int Cnt = 0;
            foreach (char c in @this)
            {
                if (c == ToFind) ++Cnt;
            }
            return Cnt;
        }

        /// <summary>
        /// Returns true if an enumerable is null or empty
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="this">the enumerable</param>
        /// <returns>trye if null or empty</returns>

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> @this)
        {
            return @this == null || @this.Count() == 0;
        }

        /// <summary>
        /// Looks up a character in a char array and returns the corresponding string representation. E.g.
        /// 'x'.Xlat(new char[] { 'x', 'y', 'z' }, new string[] { "a", "b", "c" }) will produce string "a"
        /// </summary>
        /// <param name="this">this character</param>
        /// <param name="Lookup">the char array to search</param>
        /// <param name="ReplaceWith">the string to replace this character with</param>
        /// <returns>the replacement string, as described, or null if no match found</returns>

        public static string Xlat(this char @this, char[] Lookup, string [] ReplaceWith)
        {
            for (int i = 0; i < Lookup.Length; ++i)
            {
                if (@this == Lookup[i])
                {
                    return ReplaceWith[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Looks up a string in a string array and returns the corresponding character representation. E.g.
        /// "x".Xlat(new string[] { "x", "y", "z" }, new char[] { 'a', 'b', 'c' }) will produce character 'a'. A
        /// case-insensitive comparison is performed, but the translation character is returned exactly as is.
        /// </summary>
        /// <param name="this">this string</param>
        /// <param name="Lookup">the string array to search</param>
        /// <param name="ReplaceWith">the character to replace this string with</param>
        /// <returns>the replacement character, as described, or zero if no match found</returns>

        public static char Xlat(this string @this, string [] Lookup, char [] ReplaceWith)
        {
            for (int i = 0; i < Lookup.Length; ++i)
            {
                if (@this.ToLower() == Lookup[i].ToLower())
                {
                    return ReplaceWith[i];
                }
            }
            return (char) 0;
        }

        /// <summary>
        /// returns the rightmost number of characaters from a string. E.g. "meetwo".right(3) produces "two"
        /// </summary>
        /// <param name="this">the string to process</param>
        /// <param name="Chars">number of characters from the right to return</param>
        /// <returns></returns>

        public static string Right(this string @this, int Chars)
        {
            if (@this.Length < Chars)
            {
                return @this;
            }
            else
            {
                return @this.Substring(@this.Length - Chars);
            }
        }

        /// <summary>
        /// Like the SQL STUFF function: returns a string of input length filled with the replacement character.
        /// E.g. "hello".Stuff("-") returns: "-----" 
        /// </summary>
        /// <param name="this"></param>
        /// <param name="Repl"></param>
        /// <returns></returns>

        public static string Stuff(this string @this, char Repl)
        {
            return Regex.Replace(@this, ".", Repl.ToString());
        }

        /// <summary>
        /// Returns TRUE if the string is found in the specified list. This is a case-insensitive comparison
        /// </summary>
        /// <param name="this">string to search</param>
        /// <param name="List">list to search for this string in</param>
        /// <returns></returns>

        public static bool In(this string @this, params string[] List)
        {
            foreach (string s in List)
            {
                if (@this.ToLower() == s.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns TRUE if the character is found in the specified list. This is a case-sensitive comparison
        /// </summary>
        /// <param name="this"></param>
        /// <param name="List"></param>
        /// <returns></returns>

        public static bool In(this char @this, params char[] List)
        {
            foreach (char c in List)
            {
                if (@this == c)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns TRUE if the string is a valid GUID. Otherwise returns FALSE
        /// </summary>
        /// <param name="this">The string to validate</param>
        /// <returns></returns>

        public static bool IsGuid(this string @this)
        {
            Guid TmpGuid;
            return Guid.TryParse(@this, out TmpGuid);
        }

        /// <summary>
        /// Gets the scale of a decimal value (digits after decimal point)
        /// </summary>
        /// <param name="this">the value</param>
        /// <returns>E.g. Scale(1.234) returns 3</returns>

        public static int Scale(this decimal @this)
        {
            if (@this == 0)
                return 0;
            int[] bits = decimal.GetBits(@this);
            return (bits[3] >> 16) & 0x7F;
        }

        /// <summary>
        /// Gets the precision of a decimal value (total digits)
        /// </summary>
        /// <param name="this">the value</param>
        /// <returns>E.g. Precision(1.234) returns 4</returns>

        public static int Precision(this decimal @this)
        {
            if (@this == 0)
                return 0;
            int[] bits = decimal.GetBits(@this);
            // We will use false for the sign (false =  positive), because we don't care about it.
            // We will use 0 for the last argument instead of bits[3] to eliminate the fraction point.
            decimal d = new Decimal(bits[0], bits[1], bits[2], false, 0);
            return (int)Math.Floor(Math.Log10((double)d)) + 1;
        }
    }
}
