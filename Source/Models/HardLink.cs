using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace GlosSIIntegration.Models
{
    internal class HardLink
    {
        #region Win32
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr lpSecurityAttributes);

        private const string ExtendMaxPathLimitPrefix = @"\\?\";
        #endregion Win32

        private static string GetExtendedPath(string path)
        {
            return ExtendMaxPathLimitPrefix + Path.GetFullPath(path);
        }

        /// <summary>
        /// Creates a hard link from one file to another.
        /// </summary>
        /// <param name="toPath">The path of the new file. 
        /// Note that all directories in the path must already exist.</param>
        /// <param name="fromPath">The path to the file to make a hard link from.</param>
        /// <exception cref="Win32Exception">If unable to create the hard link.</exception>
        public static void Create(string toPath, string fromPath)
        {
            if (!CreateHardLink(GetExtendedPath(toPath), GetExtendedPath(fromPath), IntPtr.Zero))
            {
                throw new Win32Exception();
            }
        }
    }
}
