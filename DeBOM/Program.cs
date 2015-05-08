using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeBOM
{
    class Program
    {
        // The buffer size for stream copy.
        const int BUFFSIZE = 65536;
        // The BOM.
        static readonly byte[] BOM = { 0xEF, 0xBB, 0xBF };
        // The flag whether no file names should be displayed.
        static bool Quiet = false;
        // The flag whether recursive removal is enabled.
        static bool Recursive = false;
        // The search pattern to find all files.
        const string SearchAll = "*";
        // The filename of temporary file.
        static readonly string TempFilename = ".DeBOM";
        // The allowed wildcard specifiers in search pattern.
        static readonly char[] Wildcards = { '*', '?' };

        // Main entry point.
        static int Main(string[] args)
        {
            // Collect path arguments and search for options.
            List<string> paths = new List<string>();
            foreach (string arg in args)
            {
                switch (arg.ToUpperInvariant())
                {
                    case "/Q":
                        Quiet = true;
                        break;

                    case "/R":
                        Recursive = true;
                        break;

                    default:
                        if (arg[0] == '/')
                        {
                            Console.WriteLine(string.Format("Invalid switch - \"{0}\"", arg));

                            return Usage();
                        }

                        paths.Add(arg);
                        break;
                }
            }
            if (paths.Count < 1) return Usage();

            foreach (string path in paths)
                if (!Process(path))
                    return 1;

            return 0;
        }

        static bool Process(string path)
        {
            string pattern = null;

            // Check if filename is a search pattern.
            string filename = Path.GetFileName(path);
            if (filename.IndexOfAny(Wildcards) >= 0)
            {
                pattern = filename;
                path = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(path)) path = ".";
            }
            else if (File.Exists(path))
                return RemoveBOM(path);

            if (Directory.Exists(path))
            {
                // Traverse path using search pattern and/or recursive option.
                foreach (string file in Directory.EnumerateFiles(path, pattern ?? SearchAll, Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    if (!RemoveBOM(file))
                        return false;
                }
            }
            else
            {
                if (!Quiet) Console.WriteLine("No such file or directory: " + path);

                return false;
            }

            return true;
        }

        // Removes BOM from file.
        // Returns false if error occurs.
        static bool RemoveBOM(string file)
        {
            try
            {
                using (FileStream input = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
                {
                    // Read extract byte to determine whether copying is actualy necessary.
                    byte[] buf = new byte[BOM.Length + 1];
                    int n = input.Read(buf, 0, buf.Length);
                    if (n < BOM.Length) return true; // File is too small.

                    // Check for BOM.
                    if (buf[0] != BOM[0] || buf[1] != BOM[1] || buf[2] != BOM[2]) return true;

                    // BOM is all there is, just truncate file.
                    if (n == BOM.Length)
                    {
                        input.SetLength(0);

                        return true;
                    }
                    else // Seek after BOM.
                        input.Seek(BOM.Length, SeekOrigin.Begin);

                    // Get path of temporary file in same directory as file having BOM removed.
                    string tempFile = Path.Combine(Path.GetDirectoryName(file), TempFilename);

                    using (FileStream temp = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, BUFFSIZE))
                    {
                        // Copy remaining input.
                        input.CopyTo(temp);

                        // Close both streams.
                        input.Close();
                        temp.Close();

                        // Delete original file and rename temporary file.
                        File.Delete(input.Name);
                        File.Move(temp.Name, input.Name);
                    }
                }
            }
            catch (Exception e)
            {
                if (!Quiet) Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        // Displays command usage.
        static int Usage()
        {
            Console.WriteLine("Removes UTF-8 BOM from file(s)." + Environment.NewLine);
            Console.WriteLine("DeBOM [/Q] [/R] [drive:][path][filename]" + Environment.NewLine);
            Console.WriteLine("  /Q  Does not display any messages.");
            Console.WriteLine("  /R  Removes BOM from files in directories recursively.");

            return 1;
        }
    }
}
