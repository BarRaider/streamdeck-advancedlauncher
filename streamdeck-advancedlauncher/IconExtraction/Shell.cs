using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.IconExtraction
{
    internal sealed class Shell : NativeMethods
    {
        #region OfExtension

        ///<summary>
        /// Get the icon of an extension
        ///</summary>
        ///<param name="filename">filename</param>
        ///<param name="overlay">bool symlink overlay</param>
        ///<returns>Icon</returns>
        public static Icon OfExtension(string filename, bool overlay = false)
        {
            string filepath;
            string[] extension = filename.Split('.');
            string dirpath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "cache");
            Directory.CreateDirectory(dirpath);
            if (String.IsNullOrEmpty(filename) || extension.Length == 1)
            {
                filepath = Path.Combine(dirpath, "dummy_file");
            }
            else
            {
                filepath = Path.Combine(dirpath, String.Join(".", "dummy", extension[extension.Length - 1]));
            }
            if (File.Exists(filepath) == false)
            {
                File.Create(filepath);
            }
            Icon icon = OfPath(filepath, false, true, overlay);
            return icon;
        }
        #endregion

        #region OfFolder

        ///<summary>
        /// Get the icon of an extension
        ///</summary>
        ///<returns>Icon</returns>
        ///<param name="overlay">bool symlink overlay</param>
        public static Icon OfFolder(bool overlay = false)
        {
            string dirpath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "cache", "dummy");
            Directory.CreateDirectory(dirpath);
            Icon icon = OfPath(dirpath, true, true, overlay);
            return icon;
        }
        #endregion

        #region OfPath

        ///<summary>
        /// Get the normal,small assigned icon of the given path
        ///</summary>
        ///<param name="filepath">physical path</param>
        ///<param name="small">bool small icon</param>
        ///<param name="checkdisk">bool fileicon</param>
        ///<param name="overlay">bool symlink overlay</param>
        ///<returns>Icon</returns>
        public static Icon OfPath(string filepath, bool small = true, bool checkdisk = true, bool overlay = false)
        {
            Icon clone;
            SHGFI_Flag flags;
            SHFILEINFO shinfo = new SHFILEINFO();
            if (small)
            {
                flags = SHGFI_Flag.SHGFI_ICON | SHGFI_Flag.SHGFI_SMALLICON;
            }
            else
            {
                flags = SHGFI_Flag.SHGFI_ICON | SHGFI_Flag.SHGFI_LARGEICON;
            }
            if (checkdisk == false)
            {
                flags |= SHGFI_Flag.SHGFI_USEFILEATTRIBUTES;
            }
            if (overlay)
            {
                flags |= SHGFI_Flag.SHGFI_LINKOVERLAY;
            }
            if (SHGetFileInfo(filepath, 0, ref shinfo, Marshal.SizeOf(shinfo), flags) == 0)
            {
                throw (new FileNotFoundException());
            }
            Icon tmp = Icon.FromHandle(shinfo.hIcon);
            clone = (Icon)tmp.Clone();
            tmp.Dispose();
            if (DestroyIcon(shinfo.hIcon) != 0)
            {
                return clone;
            }
            return clone;
        }




        ///<summary>
        /// Get the jumbo assigned icon of the given path
        ///</summary>
        ///<param name="filepath">physical path</param>
        ///<param name="checkdisk">bool fileicon</param>
        ///<param name="overlay">bool symlink overlay</param>
        ///<returns>Icon</returns>
        public static Icon JumboOfPath(string filepath, bool checkdisk = true, bool overlay = false)
        {
            // Todo: Does not work properly if exe doesn't have a JumboIcon. Moved to ThumbnailProvider instead.

            Icon clone;
            SHGFI_Flag flags;
            SHFILEINFO shinfo = new SHFILEINFO();
            flags = SHGFI_Flag.SHGFI_SYSICONINDEX | SHGFI_Flag.SHGFI_LARGEICON | SHGFI_Flag.SHGFI_USEFILEATTRIBUTES;

            if (SHGetFileInfo(filepath, 0, ref shinfo, Marshal.SizeOf(shinfo), flags) == 0)
            {
                throw (new FileNotFoundException());
            }

            IImageList spiml = null;
            Guid guil = new Guid(IID_IImageList2);//or IID_IImageList

            SHGetImageList(SHIL_JUMBO, ref guil, ref spiml);
            IntPtr hJumboIcon = IntPtr.Zero;
            spiml.GetIcon(shinfo.iIcon, ILD_TRANSPARENT | ILD_IMAGE | ILD_SCALE, ref hJumboIcon); //

            Icon tmp = Icon.FromHandle(hJumboIcon);
            clone = (Icon)tmp.Clone();
            tmp.Dispose();
            DestroyIcon(shinfo.hIcon);
            DestroyIcon(hJumboIcon);
            return clone;
        }
        #endregion
    }
}