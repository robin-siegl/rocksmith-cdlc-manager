using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CdlcManager
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Any(arg => arg == "--bridge" || arg.StartsWith("chrome-extension://", StringComparison.Ordinal)))
            {
                Bridge.Run();
                return;
            }

            try
            {
                var app = new Application();
                var manager = new Manager();
                if (args.Length == 2 && args[0] == "--preview")
                {
                    manager.ExportPreview(args[1]);
                    return;
                }
                app.Run(manager.Window);
            }
            catch (Exception error)
            {
                if (args.Contains("--preview")) throw;
                MessageBox.Show(error.Message, "Rocksmith CDLC Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public sealed class Row
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        internal Song Song;
        internal string File;
    }

    sealed class Manager
    {
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        static readonly string[] Names = { "Download queue", "Import files", "Installed library", "Settings" };
        static readonly string[] Descriptions = {
            "Save songs now and download them when you’re ready.",
            "Select the Windows CDLC files you want to copy into Rocksmith.",
            "Find the CDLC files in your Rocksmith folder.",
            "Choose your folders and connect the Chrome extension."
        };

        readonly FrameworkElement[] pages;
        readonly Button[] tabs;
        readonly TextBlock status;
        readonly ListBox queue, imports, library;
        readonly TextBox link;
        string[] downloaded = new string[0], installed = new string[0];
        Settings state;
        int page, loadVersion;
        bool busy;

        internal Window Window { get; private set; }

        internal Manager()
        {
            using (var markup = Assembly.GetExecutingAssembly().GetManifestResourceStream("Shell.xaml"))
                Window = (Window)XamlReader.Load(markup);
            using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("App.ico"))
                Window.Icon = BitmapFrame.Create(icon, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            tabs = new[] { Get<Button>("QueueTab"), Get<Button>("ImportTab"), Get<Button>("LibraryTab"), Get<Button>("SettingsTab") };
            pages = new[] { Get<FrameworkElement>("QueuePage"), Get<FrameworkElement>("ImportPage"),
                Get<FrameworkElement>("LibraryPage"), Get<FrameworkElement>("SettingsPage") };
            status = Get<TextBlock>("Status");
            queue = Get<ListBox>("QueueItems");
            imports = Get<ListBox>("ImportItems");
            library = Get<ListBox>("LibraryItems");
            link = Get<TextBox>("LinkInput");

            for (int index = 0; index < tabs.Length; index++)
            {
                int destination = index;
                tabs[index].Click += (sender, args) => Navigate(destination);
            }
            Click("GlobalAction", () => Navigate(3));
            Click("AddLink", () => Start(AddLink));
            link.KeyDown += (sender, args) => { if (args.Key == Key.Enter) Start(AddLink); };
            Click("OpenSong", OpenSong);
            queue.MouseDoubleClick += (sender, args) => Safely(OpenSong);
            Click("InstallSong", () => Start(ImportForSong));
            Click("LinkSong", LinkExisting);
            Click("RemoveSong", RemoveSong);
            Click("Rescan", Refresh);
            Click("RefreshFiles", Refresh);
            Click("SelectAllFiles", () => imports.SelectAll());
            Click("ImportSelected", () => Start(ImportFiles));
            Click("RevealFile", RevealFile);
            library.MouseDoubleClick += (sender, args) => Safely(RevealFile);
            Click("ChooseDlc", () => ChooseFolder(true));
            Click("ChooseDownloads", () => ChooseFolder(false));
            Click("Connect", ConnectChrome);
            Get<TextBox>("ImportFilter").TextChanged += (sender, args) => ShowFiles(true);
            Get<TextBox>("LibraryFilter").TextChanged += (sender, args) => ShowFiles(false);
            queue.SelectionChanged += (sender, args) => UpdateActions();
            imports.SelectionChanged += (sender, args) => UpdateActions();
            library.SelectionChanged += (sender, args) => UpdateActions();
            Window.Activated += (sender, args) => { if (page == 0 && !busy) Refresh(); };

            state = Library.Access();
            Navigate(0, false);
            ShowQueue();
            if (!Directory.Exists(state.Dlc))
                status.Text = "Choose your Rocksmith DLC folder in Settings to start importing.";
        }

        T Get<T>(string name) where T : class { return (T)Window.FindName(name); }
        Song SelectedSong { get { var row = queue.SelectedItem as Row; return row == null ? null : row.Song; } }

        void Click(string name, Action action)
        {
            Get<Button>(name).Click += (sender, args) => Safely(action);
        }

        void Safely(Action action)
        {
            if (busy) return;
            try { action(); }
            catch (Exception error) { status.Text = error.Message; }
        }

        void Navigate(int destination, bool refresh = true)
        {
            if (page == destination && refresh) return;
            page = destination;
            for (int index = 0; index < pages.Length; index++)
            {
                bool active = index == page;
                pages[index].Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                tabs[index].Background = active ? new SolidColorBrush(Color.FromRgb(55, 53, 31)) : Brushes.Transparent;
                tabs[index].Foreground = active ? (Brush)Window.FindResource("Accent") : Brushes.WhiteSmoke;
                Automation.SetSelected(tabs[index], active);
            }
            Get<TextBlock>("PageTitle").Text = Names[page];
            Get<TextBlock>("PageDescription").Text = Descriptions[page];
            status.Text = "";
            if (page == 1 || page == 2) ShowFiles(page == 1);
            if (page == 3) ShowSettings();
            if (refresh) Refresh();
            UpdateActions();
        }

        async void Refresh()
        {
            int version = ++loadVersion;
            int requested = page;
            try
            {
                var next = await Task.Run(() => Library.Access());
                if (version != loadVersion || requested != page) return;
                state = next;
                switch (page)
                {
                    case 0:
                        ShowQueue();
                        break;
                    case 1:
                    case 2:
                        bool importing = page == 1;
                        string folder = importing ? state.Downloads : state.Dlc;
                        var files = await Task.Run(() => Library.Scan(folder, !importing));
                        if (version != loadVersion || requested != page) return;
                        if (importing) downloaded = files;
                        else installed = files;
                        ShowFiles(importing);
                        break;
                    case 3:
                        ShowSettings();
                        break;
                }
                UpdateActions();
            }
            catch (Exception error)
            {
                if (version == loadVersion) status.Text = error.Message;
            }
        }

        void ShowQueue()
        {
            string selected = SelectedSong == null ? null : SelectedSong.Id;
            var rows = state.Songs.Select(song => new Row {
                Song = song, Title = song.Title, Detail = Library.Installed(state, song) ? "Installed" : "Saved for later"
            }).ToArray();
            queue.ItemsSource = rows;
            queue.SelectedItem = rows.FirstOrDefault(row => row.Song.Id == selected);
            Get<TextBlock>("QueueEmpty").Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateActions();
        }

        void ShowFiles(bool importing)
        {
            var list = importing ? imports : library;
            var previous = list.SelectedItems.Cast<Row>().Select(row => row.File).ToArray();
            var term = Get<TextBox>(importing ? "ImportFilter" : "LibraryFilter").Text.Trim();
            var entries = importing ? downloaded : installed;
            var rows = entries.Where(file => Path.GetFileName(file).IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Select(file => new Row { File = file, Title = Path.GetFileNameWithoutExtension(file), Detail = ".psarc" }).ToArray();
            list.ItemsSource = rows;
            var selection = rows.Where(item => previous.Contains(item.File));
            if (importing)
                foreach (var row in selection) list.SelectedItems.Add(row);
            else list.SelectedItem = selection.FirstOrDefault();
            var empty = Get<TextBlock>(importing ? "ImportEmpty" : "LibraryEmpty");
            var folder = importing ? state.Downloads : state.Dlc;
            empty.Text = !Directory.Exists(folder) ? "Choose a folder in Settings." :
                term.Length > 0 ? "No files match this search." : importing ? "No Windows CDLC files found in Downloads." : "No Windows CDLC files found in this folder.";
            empty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateActions();
        }

        void ShowSettings()
        {
            Get<TextBlock>("DlcPath").Text = state.Dlc ?? "No folder selected";
            Get<TextBlock>("DlcPath").ToolTip = state.Dlc;
            Get<TextBlock>("DownloadsPath").Text = state.Downloads ?? "No folder selected";
            Get<TextBlock>("DownloadsPath").ToolTip = state.Downloads;
        }

        void UpdateActions()
        {
            if (state == null) return;
            bool hasSong = SelectedSong != null;
            Get<Button>("GlobalAction").Visibility = page != 3 && !Directory.Exists(state.Dlc) ? Visibility.Visible : Visibility.Collapsed;
            Get<StackPanel>("SongActions").Visibility = hasSong ? Visibility.Visible : Visibility.Hidden;
            Get<Button>("OpenSong").IsEnabled = !busy && hasSong;
            Get<Button>("InstallSong").IsEnabled = !busy && hasSong;
            Get<Button>("LinkSong").IsEnabled = !busy && hasSong;
            Get<Button>("RemoveSong").IsEnabled = !busy && hasSong && !Library.Installed(state, SelectedSong);
            Get<Button>("ImportSelected").IsEnabled = !busy && imports.SelectedItems.Count > 0;
            Get<Button>("SelectAllFiles").IsEnabled = !busy && imports.Items.Count > 0;
            Get<Button>("RevealFile").IsEnabled = !busy && (library.SelectedItem != null || Directory.Exists(state.Dlc));
            Get<Button>("AddLink").IsEnabled = !busy;
            Get<TextBlock>("ImportSelection").Text = imports.SelectedItems.Count + " selected";
        }

        async void Start(Func<Task<string>> action)
        {
            if (busy) return;
            busy = true;
            UpdateActions();
            try { status.Text = await action(); }
            catch (Exception error) { status.Text = error.Message; }
            finally
            {
                busy = false;
                UpdateActions();
                Refresh();
            }
        }

        async Task<string> AddLink()
        {
            Uri url;
            if (!Uri.TryCreate(link.Text.Trim(), UriKind.Absolute, out url) || url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0)
                throw new IOException("Paste an HTTPS link to an Ignition song or Windows CDLC file.");
            if (url.Host == "ignition4.customsforge.com" || url.Host == "www.ignition4.customsforge.com")
            {
                var match = Regex.Match(url.AbsolutePath, @"^/cdlc/(\d{1,12})/?$");
                if (!match.Success) throw new IOException("Use an Ignition song URL ending in /cdlc/ID.");
                Bridge.Save(new Song { Id = match.Groups[1].Value });
                link.Clear();
                return "Song saved to the queue.";
            }
            string folder = RequireDlc();
            status.Text = "Downloading Windows CDLC…";
            string file = await Fetch(url, folder);
            link.Clear();
            return "Installed " + file;
        }

        static async Task<string> Fetch(Uri url, string folder)
        {
            string temp = Path.GetTempFileName();
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
                using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                {
                    response.EnsureSuccessStatusCode();
                    var header = response.Content.Headers.ContentDisposition;
                    string name = header == null ? null : header.FileNameStar ?? header.FileName;
                    if (String.IsNullOrWhiteSpace(name)) name = Uri.UnescapeDataString(response.RequestMessage.RequestUri.Segments.Last());
                    name = name.Trim('"');
                    if (!Library.PcFile.IsMatch(name)) throw new IOException("This link did not return a Windows PSARC. Download it in Chrome and import the file.");
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = File.Create(temp))
                    {
                        var buffer = new byte[81920];
                        long total = 0;
                        int bytes;
                        while ((bytes = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0)
                        {
                            total += bytes;
                            if (total > 256 * 1024 * 1024) throw new IOException("This file exceeds the 256 MB download limit.");
                            await output.WriteAsync(buffer, 0, bytes, timeout.Token);
                        }
                    }
                    return await Task.Run(() => Library.Import(temp, name, folder));
                }
            }
            finally
            {
                try { File.Delete(temp); }
                catch (IOException) { }
            }
        }

        void OpenSong()
        {
            var song = SelectedSong;
            if (song != null) Open("https://ignition4.customsforge.com/cdlc/" + song.Id);
        }

        async Task<string> ImportForSong()
        {
            var song = SelectedSong;
            if (song == null) return "";
            string folder = RequireDlc();
            var dialog = FileDialog(state.Downloads);
            if (dialog.ShowDialog(Window) != true) return "";
            status.Text = "Importing file…";
            string file = await Task.Run(() => Library.Import(dialog.FileName, Path.GetFileName(dialog.FileName), folder));
            Associate(song, file);
            return "Installed " + file;
        }

        void LinkExisting()
        {
            var song = SelectedSong;
            if (song == null) return;
            var root = Path.GetFullPath(RequireDlc()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var dialog = FileDialog(root);
            if (dialog.ShowDialog(Window) != true) return;
            if (!dialog.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Library.PcFile.IsMatch(dialog.FileName))
                throw new IOException("Select a PC CDLC file inside your Rocksmith DLC folder.");
            Associate(song, dialog.FileName.Substring(root.Length));
            status.Text = "Linked " + Path.GetFileName(dialog.FileName);
            Refresh();
        }

        static void Associate(Song song, string file)
        {
            Library.Access(current =>
            {
                var entry = current.Songs.FirstOrDefault(item => item.Id == song.Id);
                if (entry == null) { entry = song; current.Songs.Add(entry); }
                entry.File = file;
            });
        }

        void RemoveSong()
        {
            var song = SelectedSong;
            if (song == null) return;
            Library.Access(current => current.Songs.RemoveAll(item => item.Id == song.Id && !Library.Installed(current, item)));
            status.Text = "Removed from the queue.";
            Refresh();
        }

        async Task<string> ImportFiles()
        {
            string folder = RequireDlc();
            var sources = imports.SelectedItems.Cast<Row>().Select(item => item.File).ToArray();
            status.Text = "Importing " + sources.Length + " files…";
            var errors = await Task.Run(() =>
            {
                var failures = new System.Collections.Generic.List<string>();
                foreach (var file in sources)
                {
                    try { Library.Import(file, Path.GetFileName(file), folder); }
                    catch (Exception error) { failures.Add(Path.GetFileName(file) + " — " + error.Message); }
                }
                return failures;
            });
            imports.UnselectAll();
            if (errors.Count > 0)
                MessageBox.Show(Window, String.Join(Environment.NewLine, errors), "Import results");
            return (sources.Length - errors.Count) + " files ready in Rocksmith." + (errors.Count == 0 ? "" : " " + errors.Count + " could not be imported.");
        }

        void RevealFile()
        {
            var selected = library.SelectedItem as Row;
            if (selected == null) { if (Directory.Exists(state.Dlc)) Open(state.Dlc); }
            else Process.Start("explorer.exe", "/select,\"" + selected.File + "\"");
        }

        void ChooseFolder(bool dlc)
        {
            using (var dialog = new Forms.FolderBrowserDialog {
                SelectedPath = (dlc ? state.Dlc : state.Downloads) ?? "",
                Description = dlc ? "Choose your Rocksmith DLC folder" : "Choose the folder Chrome downloads into",
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog(new DialogOwner(Window)) != Forms.DialogResult.OK) return;
                string folder = dialog.SelectedPath;
                state = Library.Access(current =>
                {
                    if (dlc)
                    {
                        if (!String.Equals(current.Dlc, folder, StringComparison.OrdinalIgnoreCase))
                            foreach (var song in current.Songs) song.File = null;
                        current.Dlc = folder;
                    }
                    else current.Downloads = folder;
                });
            }
            downloaded = new string[0];
            installed = new string[0];
            ShowSettings();
            status.Text = "Folder saved.";
        }

        void ConnectChrome()
        {
            var field = Get<TextBox>("ExtensionId");
            var id = field.Text.Trim();
            if (!Regex.IsMatch(id, @"^[a-p]{32}$")) throw new IOException("Copy the extension ID from chrome://extensions.");
            Directory.CreateDirectory(Library.DataFolder);
            string path = Path.Combine(Library.DataFolder, "chrome-host.json");
            var config = new {
                name = "local.rocksmith_cdlc_manager",
                description = "Rocksmith CDLC Manager desktop connection",
                path = Assembly.GetExecutingAssembly().Location,
                type = "stdio",
                allowed_origins = new[] { "chrome-extension://" + id + "/" }
            };
            File.WriteAllText(path, Library.Json.Serialize(config), new UTF8Encoding(false));
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\local.rocksmith_cdlc_manager"))
                key.SetValue("", path);
            status.Text = "Chrome connected. Open Download Later in Chrome and click Refresh.";
        }

        string RequireDlc()
        {
            if (!Directory.Exists(state.Dlc)) throw new IOException("Choose your Rocksmith DLC folder in Settings first.");
            return state.Dlc;
        }

        static OpenFileDialog FileDialog(string folder)
        {
            return new OpenFileDialog { Filter = "Rocksmith archives (*.psarc)|*.psarc", InitialDirectory = folder ?? "", CheckFileExists = true };
        }

        static void Open(string target)
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        internal void ExportPreview(string file)
        {
            var root = (FrameworkElement)Window.Content;
            root.Measure(new Size(Window.Width, Window.Height));
            root.Arrange(new Rect(0, 0, Window.Width, Window.Height));
            root.UpdateLayout();
            var image = new RenderTargetBitmap((int)Window.Width, (int)Window.Height, 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var output = File.Create(file)) encoder.Save(output);
        }

        sealed class DialogOwner : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            internal DialogOwner(Window window) { Handle = new System.Windows.Interop.WindowInteropHelper(window).Handle; }
        }
    }

    static class Automation
    {
        internal static void SetSelected(Button button, bool selected)
        {
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "Current page" : "");
        }
    }
}
