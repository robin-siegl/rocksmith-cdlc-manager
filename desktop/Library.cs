using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace CdlcManager
{
    public sealed class Song
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string File { get; set; }
        public string DownloadedFile { get; set; }
        public bool Removed { get; set; }
        public string QueuedAt { get; set; }
    }

    public sealed class Settings
    {
        public string Dlc { get; set; }
        public string Downloads { get; set; }
        public List<Song> Songs { get; set; }

        public Settings()
        {
            Downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Songs = new List<Song>();
        }
    }

    static class Library
    {
        internal static readonly Regex PcFile = new Regex(@"_p(?: \(\d+\))?\.psarc$", RegexOptions.IgnoreCase);
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8388608 };
        internal static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RocksmithCdlcManager");
        static readonly string StateFile = Path.Combine(DataFolder, "library.json");

        internal static Settings Access(Action<Settings> change = null)
        {
            using (var mutex = new Mutex(false, @"Local\RocksmithCdlcManager.State"))
            {
                try
                {
                    if (!mutex.WaitOne(TimeSpan.FromSeconds(8))) throw new IOException("The library is busy. Try again.");
                }
                catch (AbandonedMutexException) { }
                try
                {
                    var state = File.Exists(StateFile) ? Json.Deserialize<Settings>(File.ReadAllText(StateFile)) : new Settings();
                    if (change != null)
                    {
                        change(state);
                        Directory.CreateDirectory(DataFolder);
                        var temp = StateFile + ".new";
                        File.WriteAllText(temp, Json.Serialize(state));
                        if (File.Exists(StateFile)) File.Replace(temp, StateFile, null);
                        else File.Move(temp, StateFile);
                    }
                    return state;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        internal static string[] Scan(string directory, bool recursive)
        {
            if (!Directory.Exists(directory)) return new string[0];
            var files = new List<string>();
            var folders = new Stack<string>();
            folders.Push(directory);
            while (folders.Count > 0)
            {
                var folder = folders.Pop();
                files.AddRange(Directory.EnumerateFiles(folder, "*.psarc").Where(file => PcFile.IsMatch(Path.GetFileName(file))));
                if (recursive)
                    foreach (var child in Directory.EnumerateDirectories(folder))
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) folders.Push(child);
            }
            return files.OrderBy(file => Path.GetFileName(file), StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        internal static bool Installed(Settings state, Song song)
        {
            return Directory.Exists(state.Dlc) && !String.IsNullOrWhiteSpace(song.File)
                && File.Exists(Path.Combine(state.Dlc, song.File));
        }

        internal static string Import(string source, string name, string folder)
        {
            if (!Directory.Exists(folder)) throw new IOException("Select your Rocksmith DLC folder first.");
            if (name != Path.GetFileName(name) || !PcFile.IsMatch(name) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new IOException("Choose a Windows CDLC file ending in _p.psarc.");
            string temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".part");
            try
            {
                using (var input = File.OpenRead(source))
                {
                    if (input.ReadByte() != 'P' || input.ReadByte() != 'S' ||
                        input.ReadByte() != 'A' || input.ReadByte() != 'R')
                        throw new IOException("This download is not a PSARC archive.");
                    input.Position = 0;
                    using (var output = new FileStream(temporary, FileMode.CreateNew))
                        input.CopyTo(output);
                }

                for (int number = 0; ; number++)
                {
                    string candidate = number == 0 ? name : PcFile.Replace(name, "") + " (" + number + ")_p.psarc";
                    string target = Path.Combine(folder, candidate);
                    try { File.Move(temporary, target); return candidate; }
                    catch (IOException)
                    {
                        if (!File.Exists(target)) throw;
                        if (SameFile(temporary, target)) return candidate;
                    }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static HashSet<string> FindImported(string[] downloads, string folder)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (downloads.Length == 0 || !Directory.Exists(folder)) return result;
            var candidates = Scan(folder, true).Select(path => new FileInfo(path))
                .Where(file => file.Exists).ToLookup(file => file.Length);
            foreach (var path in downloads)
            {
                try
                {
                    var source = new FileInfo(path);
                    if (source.Exists && candidates[source.Length].Any(candidate =>
                        !String.Equals(candidate.FullName, source.FullName, StringComparison.OrdinalIgnoreCase)
                        && SameFile(candidate.FullName, source.FullName))) result.Add(path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return result;
        }

        internal static int Recycle(string[] files, Settings state, bool importedOnly)
        {
            if (!Directory.Exists(state.Downloads)) return 0;
            var downloads = Path.GetFullPath(state.Downloads).TrimEnd(Path.DirectorySeparatorChar);
            var dlc = Directory.Exists(state.Dlc) ? Path.GetFullPath(state.Dlc).TrimEnd(Path.DirectorySeparatorChar) : "";
            if (dlc.Length > 0 && (String.Equals(downloads, dlc, StringComparison.OrdinalIgnoreCase) ||
                downloads.StartsWith(dlc + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Choose a Downloads folder outside the Rocksmith DLC folder to delete files.");
            if (HasRedirect(downloads) || (dlc.Length > 0 && HasRedirect(dlc)))
                throw new IOException("Downloads cleanup requires folder paths without junctions or symbolic links.");
            var verified = importedOnly ? FindImported(files, state.Dlc) : null;
            int count = 0;
            foreach (string path in files)
            {
                if (!String.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), downloads, StringComparison.OrdinalIgnoreCase)
                    || !PcFile.IsMatch(path) || !File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
                    || (verified != null && !verified.Contains(path))) continue;
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                    Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                count++;
            }
            return count;
        }

        static bool HasRedirect(string path)
        {
            for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return true;
            return false;
        }

        static bool SameFile(string left, string right)
        {
            using (var a = File.OpenRead(left))
            using (var b = File.OpenRead(right))
            using (var sha = SHA256.Create())
                return a.Length == b.Length && sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b));
        }
    }
}

