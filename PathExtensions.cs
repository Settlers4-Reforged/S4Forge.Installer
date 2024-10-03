using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ForgeUpdater {
    public static class PathExtensions {
        internal const char DirectorySeparatorChar = '\\';
        internal const char AltDirectorySeparatorChar = '/';
        internal const char VolumeSeparatorChar = ':';

        public static bool IsPathFullyQualified(string path) {
            if (path.Length < 2) {
                // It isn't fixed, it must be relative.  There is no way to specify a fixed
                // path with one character (or less).
                return false;
            }

            if (IsDirectorySeparator(path[0])) {
                // There is no valid way to specify a relative path with two initial slashes or
                // \? as ? isn't valid for drive relative paths and \??\ is equivalent to \\?\
                return (path[1] == '?' || IsDirectorySeparator(path[1]));
            }

            // The only way to specify a fixed path that doesn't begin with two slashes
            // is the drive, colon, slash format- i.e. C:\
            return ((path.Length >= 3)
                     && (path[1] == VolumeSeparatorChar)
                     && IsDirectorySeparator(path[2])
                     // To match old behavior we'll check the drive character for validity as the path is technically
                     // not qualified if you don't have a valid drive. "=:\" is the "=" file's default data stream.
                     && IsValidDriveChar(path[0]));
        }

        /// <summary>
        /// True if the given character is a directory separator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDirectorySeparator(char c) {
            return c == DirectorySeparatorChar || c == AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Returns true if the given character is a valid drive letter
        /// </summary>
        internal static bool IsValidDriveChar(char value) {
            return (uint)((value | 0x20) - 'a') <= (uint)('z' - 'a');
        }
    }
}
