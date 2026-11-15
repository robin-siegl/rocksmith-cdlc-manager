using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace CdlcManager
{
    sealed class Arrangement
    {
        internal string File, Name, Artist, Title, Part, Tuning;
    }

    static class ArrangementIndex
    {
        internal static string SavedExport { get { return Path.Combine(Library.DataFolder, "arrangements.csv"); } }

        internal static Arrangement[] Read(string file)
        {
            using (var csv = new TextFieldParser(file))
            {
                csv.Delimiters = new[] { "," };
                var headers = csv.ReadFields();
                if (headers == null) throw new InvalidDataException("The arrangement export is empty.");
                int path = Column(headers, "FilePath", "Path"), name = Column(headers, "FileName");
                int part = Column(headers, "Arrangement", "ArrangementName"), tuning = Column(headers, "Tuning");
                int artist = Column(headers, "Artist", "ArtistName"), title = Column(headers, "Title", "SongName");
                if ((path < 0 && name < 0) || part < 0 || tuning < 0)
                    throw new InvalidDataException("Include FilePath or FileName, Arrangement and Tuning in the Arrangement Analyzer export.");
                var rows = new List<Arrangement>();
                while (!csv.EndOfData)
                {
                    var row = csv.ReadFields();
                    string location = Value(row, path);
                    string filename = Value(row, name);
                    if (!location.EndsWith(".psarc", StringComparison.OrdinalIgnoreCase))
                        location = location.Length > 0 && filename.Length > 0 ? Path.Combine(location, filename) : "";
                    if (location.Length > 0) filename = Path.GetFileName(location);
                    if (!Library.PcFile.IsMatch(filename)) continue;
                    string arrangement = Value(row, part);
                    if (arrangement.Length == 0) continue;
                    rows.Add(new Arrangement {
                        File = Path.IsPathRooted(location) ? Path.GetFullPath(location) : "",
                        Name = filename,
                        Artist = Value(row, artist), Title = Value(row, title),
                        Part = arrangement,
                        Tuning = Value(row, tuning)
                    });
                }
                return rows.ToArray();
            }
        }

        internal static Dictionary<string, Arrangement[]> Match(string[] installed, Arrangement[] exported)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var names = installed.ToLookup(Path.GetFileName, comparer);
            var paths = new HashSet<string>(installed, comparer);
            var result = new Dictionary<string, Arrangement[]>(comparer);
            foreach (var group in exported.GroupBy(row => row.Name, comparer))
            {
                var candidates = names[group.Key].ToArray();
                var exact = group.Where(row => paths.Contains(row.File)).GroupBy(row => row.File, comparer).ToArray();
                foreach (var match in exact) result[match.Key] = match.ToArray();
                if (exact.Length == 0 && candidates.Length == 1 &&
                    group.Select(row => row.File).Where(path => path.Length > 0).Distinct(comparer).Take(2).Count() < 2)
                    result[candidates[0]] = group.ToArray();
            }
            return result;
        }

        static int Column(string[] fields, params string[] names)
        {
            return Array.FindIndex(fields, field => names.Contains(field.Replace(" ", "").Trim(), StringComparer.OrdinalIgnoreCase));
        }

        static string Value(string[] fields, int index)
        {
            return index >= 0 && index < fields.Length ? fields[index].Trim() : "";
        }
    }
}

