using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Data.Pdf;
using Windows.Storage;
using Kaya.Core.Models;
using Kaya.Core.Services;

namespace Kaya.Wpf;

public partial class PreviewWindow : Window
{
    private readonly SearchResult _result;
    private readonly FileService _fileService = new();
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private readonly TagsList _tagsList;
    private DispatcherTimer? _toastTimer;
    private PdfDocument? _pdfDocument;
    private uint _pdfCurrentPage;
    private uint _pdfTotalPages;

    public Action? OnSaveComplete { get; set; }

    public PreviewWindow(SearchResult result, SettingsService settingsService,
        CredentialService credentialService)
    {
        InitializeComponent();
        this.ApplyDarkTitleBar();

        _result = result;
        _settingsService = settingsService;
        _credentialService = credentialService;

        WindowTitleText.Text = result.DisplayTitle;

        // Load existing meta
        var metaService = new MetaService();
        var metaData = metaService.LoadLatestMeta(result.Filename);
        var initialTags = metaData?.Tags ?? [];
        _tagsList = new TagsList(initialTags);

        // Populate existing tags as pills
        foreach (var tag in initialTags)
            AddTagPill(tag);

        if (metaData is not null && metaData.Note.Length > 0)
            SidebarNoteText.Text = metaData.Note;

        SetupContentArea();
        SetupShare();

        Logger.Instance.Log($"🔵 INFO PreviewWindow initialized for \"{result.Filename}\"");
    }

    private void SetupContentArea()
    {
        switch (_result.Type)
        {
            case AngaType.Bookmark:
                SetupBookmarkContent();
                break;
            case AngaType.Note:
                SetupNoteContent();
                break;
            case AngaType.File:
                SetupFileContent();
                break;
        }
    }

    private void SetupBookmarkContent()
    {
        BookmarkPanel.Visibility = Visibility.Visible;
        try
        {
            var contents = _fileService.ReadAngaContents(_result.Filename);
            var url = ExtractUrlFromContents(contents);
            BookmarkUrlText.Text = url;
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to read bookmark: {e.Message}");
            BookmarkUrlText.Text = "Could not read bookmark";
            BookmarkVisitButton.IsEnabled = false;
        }
    }

    private void SetupNoteContent()
    {
        NotePanel.Visibility = Visibility.Visible;
        try
        {
            var contents = _fileService.ReadAngaContents(_result.Filename);
            NoteContentText.Text = contents;
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to read note: {e.Message}");
            NoteContentText.Text = "Could not read note";
        }
    }

    private void SetupFileContent()
    {
        var filename = new Filename(_result.Filename);

        if (filename.Extension == "svg")
        {
            SetupSvgContent();
            return;
        }

        if (filename.IsImage())
        {
            SetupRasterImageContent();
            return;
        }

        if (filename.IsPdf())
        {
            _ = SetupPdfContentAsync();
            return;
        }

        FilePanel.Visibility = Visibility.Visible;
        FileTypeLabel.Text = _result.DisplayTitle;
    }

    private void SetupRasterImageContent()
    {
        ImagePanel.Visibility = Visibility.Visible;
        try
        {
            var filePath = _fileService.GetAngaFilePath(_result.Filename);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            ImagePreview.Source = bitmap;
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to load image: {e.Message}");
            ImagePanel.Visibility = Visibility.Collapsed;
            FilePanel.Visibility = Visibility.Visible;
            FileTypeLabel.Text = _result.DisplayTitle;
        }
    }

    private void SetupSvgContent()
    {
        try
        {
            var filePath = _fileService.GetAngaFilePath(_result.Filename);
            var bitmap = SvgRenderer.RenderToBitmap(filePath);
            if (bitmap is not null)
            {
                ImagePanel.Visibility = Visibility.Visible;
                ImagePreview.Source = bitmap;
                return;
            }
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to render SVG: {e.Message}");
        }

        FilePanel.Visibility = Visibility.Visible;
        FileTypeLabel.Text = _result.DisplayTitle;
    }

    private void SetupShare()
    {
        if (!_settingsService.ShouldSync())
        {
            Logger.Instance.Log("🟢 DEBUG PreviewWindow share disabled: user not logged in");
            return;
        }

        var password = _credentialService.GetPassword();
        if (string.IsNullOrEmpty(password))
        {
            Logger.Instance.Log("🟢 DEBUG PreviewWindow share disabled: no password");
            return;
        }

        ShareButton.IsEnabled = true;
        ShareSectionLabel.Foreground = SystemColors.ControlTextBrush;
        Logger.Instance.Log("🟢 DEBUG PreviewWindow share enabled");
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnSidebarToggle(object sender, RoutedEventArgs e)
    {
        if (SidebarToggle.IsChecked == true)
        {
            SidebarColumn.Width = new GridLength(382, GridUnitType.Star);
            SidebarPanel.Visibility = Visibility.Visible;
        }
        else
        {
            SidebarColumn.Width = new GridLength(0);
            SidebarPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnVisitBookmark(object sender, RoutedEventArgs e)
    {
        var url = BookmarkUrlText.Text;
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to open URL: {ex.Message}");
                ShowToast($"Failed to open URL: {ex.Message}", isError: true);
            }
        }
    }

    private void OnOpenExternally(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = _fileService.GetAngaFilePath(_result.Filename);
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to open file: {ex.Message}");
            ShowToast($"Failed to open file: {ex.Message}", isError: true);
        }
    }

    private async Task SetupPdfContentAsync()
    {
        try
        {
            var filePath = _fileService.GetAngaFilePath(_result.Filename);
            var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
            _pdfDocument = await PdfDocument.LoadFromFileAsync(storageFile);
            _pdfTotalPages = _pdfDocument.PageCount;
            _pdfCurrentPage = 0;

            PdfPanel.Visibility = Visibility.Visible;
            await RenderPdfPageAsync(_pdfCurrentPage);
            UpdatePdfNavigation();

            Logger.Instance.Log(
                $"🔵 INFO PreviewWindow loaded PDF \"{_result.Filename}\" with {_pdfTotalPages} pages");
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR PreviewWindow failed to load PDF: {e.Message}");
            FilePanel.Visibility = Visibility.Visible;
            FileTypeIcon.Text = "\U0001F4C4";
            FileTypeLabel.Text = _result.DisplayTitle;
        }
    }

    private async Task RenderPdfPageAsync(uint pageIndex)
    {
        if (_pdfDocument is null) return;

        using var page = _pdfDocument.GetPage(pageIndex);
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream.AsStream();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        PdfPageImage.Source = bitmap;
    }

    private void UpdatePdfNavigation()
    {
        PdfPageLabel.Text = $"Page {_pdfCurrentPage + 1} of {_pdfTotalPages}";
        PdfPrevButton.IsEnabled = _pdfCurrentPage > 0;
        PdfNextButton.IsEnabled = _pdfCurrentPage < _pdfTotalPages - 1;
    }

    private async void OnPdfPrev(object sender, RoutedEventArgs e)
    {
        if (_pdfCurrentPage > 0)
        {
            _pdfCurrentPage--;
            await RenderPdfPageAsync(_pdfCurrentPage);
            UpdatePdfNavigation();
        }
    }

    private async void OnPdfNext(object sender, RoutedEventArgs e)
    {
        if (_pdfCurrentPage < _pdfTotalPages - 1)
        {
            _pdfCurrentPage++;
            await RenderPdfPageAsync(_pdfCurrentPage);
            UpdatePdfNavigation();
        }
    }

    private void OnTagsPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.OemComma)
        {
            FinalizeTagEntry();
            e.Handled = true;
        }
        else if (e.Key == Key.Back && TagsEntry.Text.Length == 0 && _tagsList.Length > 0)
        {
            RemoveLastTag();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && TagsEntry.Text.Trim().Length > 0)
        {
            FinalizeTagEntry();
            e.Handled = true;
        }
    }

    private void FinalizeTagEntry()
    {
        var text = TagsEntry.Text.Trim();
        if (text.Length > 0)
        {
            _tagsList.Add(text);
            AddTagPill(text);
            TagsEntry.Text = "";
        }
    }

    private void AddTagPill(string tagText)
    {
        var pill = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(2),
            Child = new TextBlock
            {
                Text = tagText,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var entryIndex = TagsPanel.Children.IndexOf(TagsEntry);
        TagsPanel.Children.Insert(entryIndex, pill);
    }

    private void RemoveLastTag()
    {
        var removed = _tagsList.RemoveLast();
        if (removed is null) return;

        var entryIndex = TagsPanel.Children.IndexOf(TagsEntry);
        if (entryIndex > 0)
            TagsPanel.Children.RemoveAt(entryIndex - 1);
    }

    private async void OnShare(object sender, RoutedEventArgs e)
    {
        ShareButton.IsEnabled = false;
        ShareButton.Content = "Sharing\u2026";

        Logger.Instance.Log($"🔵 INFO PreviewWindow sharing \"{_result.Filename}\"");

        var shareService = new ShareService(_settingsService, _credentialService);
        var result = await shareService.ShareAsync(_result.Filename);

        if (result.Ok)
        {
            Clipboard.SetText(result.ShareUrl);
            ShowToast("URL copied to clipboard!");
            Logger.Instance.Log($"🔵 INFO PreviewWindow share succeeded: {result.ShareUrl}");
        }
        else
        {
            ShowToast($"Failed to share to {_settingsService.ServerUrl}", isError: true);
            Logger.Instance.Log($"🟠 WARN PreviewWindow share failed for \"{_result.Filename}\"");
        }

        ShareButton.IsEnabled = true;
        ShareButton.Content = "Share";
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Logger.Instance.Log($"🔵 INFO PreviewWindow on_save fired for \"{_result.Filename}\"");

        var noteText = SidebarNoteText.Text.Trim();
        var tags = _tagsList.WithPending(TagsEntry.Text);

        if (noteText.Length > 0 || tags.Length > 0)
        {
            try
            {
                var clock = new SystemClock();
                var metaFile = new Meta(_result.Filename, noteText, tags, clock).ToMetaFile();
                _fileService.SaveMeta(metaFile);
                Logger.Instance.Log($"🔵 INFO PreviewWindow saved meta for \"{_result.Filename}\"");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"🔴 ERROR PreviewWindow: {ex.Message}");
                ShowToast($"Error: {ex.Message}", isError: true);
                return;
            }
        }

        OnSaveComplete?.Invoke();
        Close();
    }

    private static string ExtractUrlFromContents(string contents)
    {
        var lines = contents.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("URL="))
                return line[4..].Trim();
        }
        return "";
    }

    private void ShowToast(string message, bool isError = false)
    {
        ToastText.Text = message;
        ToastBorder.Background = new SolidColorBrush(isError
            ? Color.FromRgb(0xF4, 0x43, 0x36)
            : Color.FromRgb(0x4C, 0xAF, 0x50));
        ToastBorder.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            ToastBorder.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };
        _toastTimer.Start();
    }
}
