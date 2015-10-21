using System;
using System.Text.RegularExpressions;

namespace GDLib.Utils {
    public static class PathUtils {
        /// <summary>
        /// Matches an absolute path that's valid for an entry.
        /// </summary>
        public static Regex EntryAbsolutePathRegex;

        /// <summary>
        /// Validates and parses a path.
        /// </summary>
        public static Regex PathValidationRegex;

        /// <summary>
        /// Checks a path for invalid characters.
        /// </summary>
        public static Regex SimplePathValidationRegex;

        static PathUtils() {
            EntryAbsolutePathRegex = new Regex(@"
                ^
                (?!$)
                (
                  [\/]
                  (?!\s)
                  [ !#-)+-.0-9;=@A-Z\[\]^-{}~]++
                  (?<![\s.])
                )+
                $
            ", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);

            PathValidationRegex = new Regex(@"
                ^
                (?!$)
                (?<root>\.{0,2}[\\\/])?
                [\\\/]*+
                (?<nodes>
                  (?!\s)
                  (?<node>
                    \.{1,2}[\\\/]|
                    [ !#-)+-.0-9;=@A-Z\[\]^-{}~]++
                    (?<![.\s])
                    [\\\/]?+
                  )
                  [\\\/]*
                )*
                $
            ", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);

            SimplePathValidationRegex = new Regex(@"^[ !#-)+-9;=@A-Z\[-\]^-{}~]*+$",
                RegexOptions.Compiled | RegexOptions.Singleline);
        }
    }
}
