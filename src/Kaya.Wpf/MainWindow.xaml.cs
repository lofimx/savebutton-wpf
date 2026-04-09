using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Kaya.Core.Models;
using Kaya.Core.Services;

namespace Kaya.Wpf;

public class SearchResultViewModel
{
    public string Filename { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
    public string ContentPreview { get; init; } = "";
    public string Date { get; init; } = "";
    public string TypeIcon { get; init; } = "";
    public Visibility TitleVisibility { get; init; } = Visibility.Visible;
}

public partial class MainWindow : Window
{
    private readonly SearchService _searchService = new();
    private readonly FileService _fileService = new();
    private DispatcherTimer? _searchDebounceTimer;
    private DispatcherTimer? _toastTimer;
    private Storyboard? _spinnerStoryboard;
    private bool _wasSyncing;

    private const int SearchDebounceMs = 300;

    public SyncManager? SyncManager { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        this.ApplyDarkTitleBar();
        _fileService.EnsureKayaDirectories();

        InputBindings.Add(new KeyBinding(ApplicationCommands.Close, Key.Q, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnPreferences(this, new RoutedEventArgs())),
            Key.OemComma, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Close()),
            Key.W, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OnNewSave(this, new RoutedEventArgs())),
            Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => SearchEntry.Focus()),
            Key.F, ModifierKeys.Control));

        SetupSpinnerAnimation();

        Loaded += (_, _) =>
        {
            PerformSearch();
            SearchEntry.Focus();
            SetupSyncSpinner();
        };

        Logger.Instance.Log("🔵 INFO EverythingWindow initialized");
    }

    private void SetupSpinnerAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            RepeatBehavior = RepeatBehavior.Forever
        };

        _spinnerStoryboard = new Storyboard();
        _spinnerStoryboard.Children.Add(animation);
        Storyboard.SetTarget(animation, SyncSpinner);
        Storyboard.SetTargetProperty(animation,
            new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
    }

    private void SetupSyncSpinner()
    {
        if (SyncManager is null) return;

        SyncManager.SettingsService.Changed += () =>
        {
            Dispatcher.Invoke(UpdateSyncSpinner);
        };

        // Check current state in case sync already started
        UpdateSyncSpinner();
    }

    private void UpdateSyncSpinner()
    {
        if (SyncManager is null) return;

        var syncing = SyncManager.SettingsService.SyncInProgress;

        if (syncing)
        {
            SyncSpinner.Visibility = Visibility.Visible;
            _spinnerStoryboard?.Begin();
        }
        else
        {
            SyncSpinner.Visibility = Visibility.Collapsed;
            _spinnerStoryboard?.Stop();
        }

        // Refresh search when sync completes
        if (_wasSyncing && !syncing)
            RefreshSearch();

        _wasSyncing = syncing;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (SearchEntry.IsFocused) return;
        if (Keyboard.Modifiers != ModifierKeys.None && Keyboard.Modifiers != ModifierKeys.Shift) return;

        if (e.Key >= Key.A && e.Key <= Key.Z ||
            e.Key >= Key.D0 && e.Key <= Key.D9 ||
            e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            SearchEntry.Focus();
        }
    }

    private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SearchDebounceMs)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            PerformSearch();
        };
        _searchDebounceTimer.Start();
    }

    private void PerformSearch()
    {
        var query = SearchEntry.Text.Trim();
        var results = _searchService.Search(query);

        if (results.Count == 0 && string.IsNullOrEmpty(query))
        {
            EmptyState.Visibility = Visibility.Visible;
            NoResultsState.Visibility = Visibility.Collapsed;
            ResultsList.Visibility = Visibility.Collapsed;
            return;
        }

        if (results.Count == 0)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            NoResultsState.Visibility = Visibility.Visible;
            ResultsList.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        NoResultsState.Visibility = Visibility.Collapsed;
        ResultsList.Visibility = Visibility.Visible;
        ResultsList.ItemsSource = results.Select(r => new SearchResultViewModel
        {
            Filename = r.Filename,
            DisplayTitle = r.DisplayTitle,
            ContentPreview = r.ContentPreview,
            Date = r.Date,
            TypeIcon = TypeIconFor(r.Type),
            TitleVisibility = SearchResultFactory.IsTitleVisible(r.Type)
                ? Visibility.Visible : Visibility.Collapsed
        }).ToList();
    }

    private static string TypeIconFor(AngaType type) => type switch
    {
        AngaType.Bookmark => "\U0001F517",  // link
        AngaType.Note => "\U0001F4CC",       // pushpin
        AngaType.File => "\U0001F4C4",       // page facing up
        _ => "\U0001F4C4"
    };

    private void RefreshSearch()
    {
        _searchService.InvalidateCache();
        PerformSearch();
        Logger.Instance.Log("🔵 INFO Search refreshed");
    }

    private void OnResultActivated(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ActivateSelectedResult();
    }

    private void OnResultsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelectedResult();
            e.Handled = true;
        }
    }

    private void ActivateSelectedResult()
    {
        if (ResultsList.SelectedItem is not SearchResultViewModel vm) return;

        var results = _searchService.Search(SearchEntry.Text.Trim());
        var result = results.FirstOrDefault(r => r.Filename == vm.Filename);
        if (result is null) return;

        OpenPreview(result);
    }

    private void OpenPreview(SearchResult result)
    {
        if (SyncManager is null) return;

        var previewWindow = new PreviewWindow(result,
            SyncManager.SettingsService,
            new CredentialService())
        {
            Owner = this,
            OnSaveComplete = RefreshSearch
        };
        previewWindow.ShowDialog();

        Logger.Instance.Log($"🔵 INFO Opening preview for \"{result.Filename}\"");
    }

    private void OnNewSave(object sender, RoutedEventArgs e)
    {
        var newSaveWindow = new NewSaveWindow
        {
            Owner = this,
            OnSaveComplete = () =>
            {
                RefreshSearch();
                // Trigger immediate sync
                SyncManager?.TriggerSync();
            }
        };
        newSaveWindow.ShowDialog();
    }

    private void OnPreferences(object sender, RoutedEventArgs e)
    {
        if (SyncManager is null) return;

        var prefs = new PreferencesWindow(SyncManager.SettingsService, new CredentialService())
        {
            Owner = this
        };
        prefs.ShowDialog();
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
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

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
