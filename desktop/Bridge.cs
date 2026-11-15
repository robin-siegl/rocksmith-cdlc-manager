using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CdlcManager
{
    static class Bridge
    {
        internal static void Run()
        {
            using (var input = new BinaryReader(Console.OpenStandardInput()))
            using (var output = new BinaryWriter(Console.OpenStandardOutput()))
            {
                object response;
                try
                {
                    int length = input.ReadInt32();
                    if (length < 1 || length > 65536) throw new IOException("Invalid message length.");
                    byte[] bytes = input.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    response = Respond(Library.Json.Deserialize<Message>(Encoding.UTF8.GetString(bytes)));
                }
                catch (Exception ex) { response = new { ok = false, error = ex.Message }; }
                byte[] payload = Encoding.UTF8.GetBytes(Library.Json.Serialize(response));
                output.Write(payload.Length);
                output.Write(payload);
            }
        }

        public sealed class Message
        {
            public string Type { get; set; }
            public Song Song { get; set; }
        }

        static object Respond(Message message)
        {
            if (message.Type == "status")
            {
                var state = Library.Access();
                return new {
                    ok = true,
                    installed = state.Songs.Where(song => Library.Installed(state, song)).Select(song => song.Id).ToArray(),
                    queue = state.Songs.Where(song => !Library.Installed(state, song))
                        .Select(song => new { id = song.Id, title = song.Title }).ToArray()
                };
            }
            var entry = message.Song;
            if (entry == null || !Regex.IsMatch(entry.Id ?? "", @"^\d{1,12}$"))
                throw new IOException("Expected an Ignition song ID.");
            if (message.Type == "save") Save(entry);
            else if (message.Type == "remove")
                Library.Access(state => state.Songs.RemoveAll(song => song.Id == entry.Id && !Library.Installed(state, song)));
            else throw new IOException("Unknown message.");
            return new { ok = true };
        }

        internal static void Save(Song entry)
        {
            Library.Access(state =>
            {
                var fallback = "CDLC " + entry.Id;
                var title = String.IsNullOrWhiteSpace(entry.Title) ? fallback : entry.Title.Substring(0, Math.Min(250, entry.Title.Length));
                var existing = state.Songs.FirstOrDefault(song => song.Id == entry.Id);
                if (existing != null)
                {
                    if (existing.Title == fallback) existing.Title = title;
                    return;
                }
                state.Songs.Add(new Song { Id = entry.Id, Title = title, Url = "https://ignition4.customsforge.com/cdlc/" + entry.Id });
            });
        }
    }
}
