using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Core;

namespace SircleToSearch;

public partial class ResultWindow : Window
{
    private byte[]? _jpegBytes;
    private bool _closing;
    private bool _busy;
    private bool _revealed;
    private byte[]? _pendingJpegBytes;
    private MorphingLoader? _loader;
    private double _targetLeft;
    private double _targetTop;
    private const string MobileUserAgent =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0.0.0 Mobile Safari/537.36";

    public ResultWindow()
    {
        InitializeComponent();

        Width = 460;
        var screenHeight = SystemParameters.WorkArea.Height;
        Height = Math.Min(760, screenHeight * 0.82);
        _targetLeft = SystemParameters.WorkArea.Right - Width - 24;
        _targetTop = SystemParameters.WorkArea.Bottom - Height;

        Left = -5000;
        Top = _targetTop;

        Loaded += ResultWindow_Loaded;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) EscapeRequested?.Invoke();
        };
    }

    public event Action? EscapeRequested;

    private void ResultWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        _loader = new MorphingLoader(SpinnerShape, radius: 24);
    }

    public async Task PreWarmAsync()
    {
        try
        {
            if (AppSettings.Current.Engine == SearchEngine.Google && !AppSettings.Current.FastSearch)
                await EnsureOnGoogleAsync();
            else
                await EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            AppLog.Error("Прогрев WebView2 не удался", ex);
        }
    }

    public bool IsResultVisible => _revealed;

    public void ShowSearch(byte[] jpegBytes)
    {
        _jpegBytes = jpegBytes;
        if (!_revealed)
        {
            _revealed = true;
            Reveal();
        }

        if (_busy)
        {
            _pendingJpegBytes = jpegBytes;
            return;
        }
        _ = RunSearchAsync(jpegBytes);
    }

    public void HideResult()
    {
        if (!_revealed) return;
        _revealed = false;
        var slideDown = new DoubleAnimation(Top, SystemParameters.WorkArea.Bottom, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        BeginAnimation(TopProperty, slideDown);
    }

    private void Reveal()
    {
        Left = _targetLeft;
        Top = SystemParameters.WorkArea.Bottom;
        var slideUp = new DoubleAnimation(SystemParameters.WorkArea.Bottom, _targetTop,
            TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(TopProperty, slideUp);
    }

    private async Task RunSearchAsync(byte[] jpegBytes)
    {
        _busy = true;
        SetStatus(searching: true);
        ShowLoading();
        string? errorMessage = null;

        try
        {
            if (AppSettings.Current.Engine == SearchEngine.Yandex)
                await NavigateToYandexResultsAsync(jpegBytes);
            else if (AppSettings.Current.FastSearch)
                await NavigateToLensResultsFastAsync(jpegBytes);
            else
                await NavigateToLensResultsAsync(jpegBytes);
        }
        catch (Exception ex)
        {
            AppLog.Error("Загрузка картинки не удалась", ex);
            errorMessage = ex is GoogleCaptchaException
                ? Strings.Get("ResultErrorCaptcha")
                : Strings.Get("ResultErrorGeneric");
        }
        finally
        {
            await HideLoadingAsync(errorMessage);
            _busy = false;
            SetStatus(searching: false);
        }

        if (_pendingJpegBytes is { } pending)
        {
            _pendingJpegBytes = null;
            await RunSearchAsync(pending);
        }
    }

    private void ShowLoading()
    {
        Browser.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        LoadingOverlay.BeginAnimation(OpacityProperty, null);
        LoadingOverlay.Opacity = 1;
        LoadingOverlay.Visibility = Visibility.Visible;
        _loader?.Start();
    }

    private Task HideLoadingAsync(string? errorMessage)
    {
        var tcs = new TaskCompletionSource();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
        fade.Completed += (_, _) =>
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            if (errorMessage is not null)
            {
                ErrorText.Text = errorMessage;
                ErrorOverlay.Visibility = Visibility.Visible;
                Browser.Visibility = Visibility.Collapsed;
            }
            else
            {
                Browser.Visibility = Visibility.Visible;
            }
            _loader?.Stop();
            tcs.TrySetResult();
        };
        LoadingOverlay.BeginAnimation(OpacityProperty, fade);
        return tcs.Task;
    }

    private sealed class GoogleCaptchaException : Exception;

    private static async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout, string what)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)) != task)
            throw new TimeoutException($"{what}: нет ответа за {timeout.TotalSeconds:0}с.");
        await task;
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string what)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)) != task)
            throw new TimeoutException($"{what}: нет ответа за {timeout.TotalSeconds:0}с.");
        return await task;
    }

    private static bool IsCaptchaUrl(string? url) =>
        url is not null && url.Contains("google.com/sorry/", StringComparison.OrdinalIgnoreCase);

    private void SetStatus(bool searching)
    {
        if (searching)
        {
            StatusText.Text = Strings.Get("ResultHeaderSearching");
            var pulse = new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(700))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            StatusDot.BeginAnimation(OpacityProperty, pulse);
        }
        else
        {
            StatusDot.BeginAnimation(OpacityProperty, null);
            StatusDot.Opacity = 1;
            StatusText.Text = Strings.Get("ResultHeaderIdle");
        }
    }

    private async Task EnsureCoreWebView2Async()
    {
        if (Browser.CoreWebView2 is not null) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SircleToSearch", "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await Browser.EnsureCoreWebView2Async(env);

        Browser.CoreWebView2!.Settings.UserAgent = MobileUserAgent;
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

        Browser.CoreWebView2.PermissionRequested += (_, e) =>
        {
            if (e.PermissionKind is CoreWebView2PermissionKind.ClipboardRead)
            {
                e.State = CoreWebView2PermissionState.Deny;
                e.Handled = true;
            }
        };

        await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("""
            (function() {
                const style = document.createElement('style');
                style.textContent = `
                    html, body { overflow-x: hidden !important; }
                    img, table, pre, code, video, iframe { max-width: 100% !important; }
                    * { word-wrap: break-word !important; overflow-wrap: anywhere !important; }
                `;
                document.documentElement.appendChild(style);

                try {
                    if (window.navigator && navigator.clipboard) {
                        const blocked = () => Promise.reject(new DOMException('Blocked by SircleToSearch', 'NotAllowedError'));
                        navigator.clipboard.writeText = blocked;
                        navigator.clipboard.write = blocked;
                    }
                } catch (err) {}
            })();
            """);

        AppLog.Info($"[perf] EnsureCoreWebView2: {sw.ElapsedMilliseconds}ms");
    }

    private async Task EnsureOnGoogleAsync()
    {
        await EnsureCoreWebView2Async();

        var onGoogle = Uri.TryCreate(Browser.CoreWebView2.Source, UriKind.Absolute, out var currentUri)
            && currentUri.Host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase)
            && !IsCaptchaUrl(currentUri.AbsoluteUri);
        if (!onGoogle)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var navigated = new TaskCompletionSource();
            void OnNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => navigated.TrySetResult();
            Browser.CoreWebView2.NavigationCompleted += OnNavCompleted;
            Browser.CoreWebView2.Navigate("https://www.google.com/");
            await WaitWithTimeoutAsync(navigated.Task, TimeSpan.FromSeconds(20), "Переход на google.com");
            Browser.CoreWebView2.NavigationCompleted -= OnNavCompleted;
            AppLog.Info($"[perf] Navigate to google.com: {sw.ElapsedMilliseconds}ms");
        }
        else
        {
            AppLog.Info("[perf] Navigate to google.com: skipped (already there)");
        }
    }

    private async Task NavigateToLensResultsFastAsync(byte[] jpegBytes)
    {
        await EnsureCoreWebView2Async();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (resultUrl, cookies) = await UploadViaHttpAsync(jpegBytes);
        AppLog.Info($"[perf] Upload fetch: {sw.ElapsedMilliseconds}ms");

        var cookieManager = Browser.CoreWebView2.CookieManager;
        foreach (var cookie in cookies)
        {
            var wvCookie = cookieManager.CreateCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path);
            wvCookie.IsSecure = cookie.Secure;
            wvCookie.IsHttpOnly = cookie.HttpOnly;
            cookieManager.AddOrUpdateCookie(wvCookie);
        }

        sw.Restart();
        var resultsLoaded = new TaskCompletionSource();
        void OnResultsNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => resultsLoaded.TrySetResult();
        Browser.CoreWebView2.NavigationCompleted += OnResultsNavCompleted;
        Browser.CoreWebView2.Navigate(resultUrl);
        await WaitWithTimeoutAsync(resultsLoaded.Task, TimeSpan.FromSeconds(20), "Загрузка страницы результатов");
        Browser.CoreWebView2.NavigationCompleted -= OnResultsNavCompleted;
        AppLog.Info($"[perf] Navigate to results page: {sw.ElapsedMilliseconds}ms");

        if (IsCaptchaUrl(Browser.CoreWebView2.Source))
            throw new GoogleCaptchaException();
    }

    private static async Task<(string ResultUrl, System.Collections.Generic.List<Cookie> Cookies)> UploadViaHttpAsync(byte[] jpegBytes)
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false,
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(MobileUserAgent);

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(jpegBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "encoded_image", "screenshot.jpg");
        content.Add(new StringContent(""), "image_url");
        content.Add(new StringContent("th"), "sbisrc");

        using var response = await client.PostAsync("https://www.google.com/searchbyimage/upload", content);

        if ((int)response.StatusCode is < 300 or >= 400 || response.Headers.Location is null)
            throw new InvalidOperationException($"Google не вернул редирект на результат (статус {(int)response.StatusCode}).");

        var location = response.Headers.Location;
        var resultUrl = location.IsAbsoluteUri ? location.ToString() : "https://www.google.com" + location;
        var cookies = cookieContainer.GetCookies(new Uri("https://www.google.com")).Cast<Cookie>().ToList();

        return (resultUrl, cookies);
    }

    private async Task NavigateToYandexResultsAsync(byte[] jpegBytes)
    {
        await EnsureCoreWebView2Async();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (resultUrl, cookies) = await UploadViaYandexAsync(jpegBytes);
        AppLog.Info($"[perf] Yandex upload: {sw.ElapsedMilliseconds}ms");

        var cookieManager = Browser.CoreWebView2.CookieManager;
        foreach (var cookie in cookies)
        {
            var wvCookie = cookieManager.CreateCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path);
            wvCookie.IsSecure = cookie.Secure;
            wvCookie.IsHttpOnly = cookie.HttpOnly;
            cookieManager.AddOrUpdateCookie(wvCookie);
        }

        sw.Restart();
        var resultsLoaded = new TaskCompletionSource();
        void OnResultsNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => resultsLoaded.TrySetResult();
        Browser.CoreWebView2.NavigationCompleted += OnResultsNavCompleted;
        Browser.CoreWebView2.Navigate(resultUrl);
        await WaitWithTimeoutAsync(resultsLoaded.Task, TimeSpan.FromSeconds(20), "Загрузка страницы результатов");
        Browser.CoreWebView2.NavigationCompleted -= OnResultsNavCompleted;
        AppLog.Info($"[perf] Navigate to Yandex results page: {sw.ElapsedMilliseconds}ms");
    }

    private static async Task<(string ResultUrl, System.Collections.Generic.List<Cookie> Cookies)> UploadViaYandexAsync(byte[] jpegBytes)
    {
        const string baseUrl = "https://yandex.com/images/search";

        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookieContainer };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(MobileUserAgent);

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(jpegBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "upfile", "screenshot.jpg");

        var requestParam = Uri.EscapeDataString("{\"blocks\":[{\"block\":\"b-page_type_search-by-image__link\"}]}");
        var uploadUrl = $"{baseUrl}?rpt=imageview&format=json&request={requestParam}";

        using var response = await client.PostAsync(uploadUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var cbirId = doc.RootElement.GetProperty("blocks")[0].GetProperty("params").GetProperty("cbirId").GetString();
        if (string.IsNullOrEmpty(cbirId))
            throw new InvalidOperationException("Yandex не вернул идентификатор результата поиска.");

        var resultUrl = $"{baseUrl}?rpt=imageview&cbir_id={Uri.EscapeDataString(cbirId)}";
        var cookies = cookieContainer.GetCookies(new Uri(baseUrl)).Cast<Cookie>().ToList();

        return (resultUrl, cookies);
    }

    private async Task NavigateToLensResultsAsync(byte[] jpegBytes)
    {
        await EnsureOnGoogleAsync();

        var uploadDone = new TaskCompletionSource<string>();
        void OnMessage(object? s, CoreWebView2WebMessageReceivedEventArgs e) =>
            uploadDone.TrySetResult(e.WebMessageAsJson);
        Browser.CoreWebView2.WebMessageReceived += OnMessage;

        var base64 = Convert.ToBase64String(jpegBytes);
        var script = $$"""
            (async () => {
                try {
                    const byteChars = atob("{{base64}}");
                    const byteNumbers = new Array(byteChars.length);
                    for (let i = 0; i < byteChars.length; i++) byteNumbers[i] = byteChars.charCodeAt(i);
                    const blob = new Blob([new Uint8Array(byteNumbers)], { type: "image/jpeg" });
                    const fd = new FormData();
                    fd.append("encoded_image", blob, "screenshot.jpg");
                    fd.append("image_url", "");
                    fd.append("sbisrc", "th");
                    const resp = await fetch("https://www.google.com/searchbyimage/upload", {
                        method: "POST",
                        body: fd,
                        credentials: "include",
                    });
                    window.chrome.webview.postMessage({ ok: true, url: resp.url });
                } catch (err) {
                    window.chrome.webview.postMessage({ ok: false, error: String(err) });
                }
            })();
            """;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Browser.CoreWebView2.ExecuteScriptAsync(script);
        var messageJson = await WaitWithTimeoutAsync(uploadDone.Task, TimeSpan.FromSeconds(20), "Загрузка картинки");
        Browser.CoreWebView2.WebMessageReceived -= OnMessage;
        AppLog.Info($"[perf] Upload fetch: {sw.ElapsedMilliseconds}ms");

        using var doc = JsonDocument.Parse(messageJson);
        var root = doc.RootElement;
        if (!root.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException($"Google отклонил загрузку: {root.GetProperty("error").GetString()}");

        var resultUrl = root.GetProperty("url").GetString();
        if (string.IsNullOrEmpty(resultUrl))
            throw new InvalidOperationException("Google не вернул URL результата поиска.");

        sw.Restart();
        var resultsLoaded = new TaskCompletionSource();
        void OnResultsNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => resultsLoaded.TrySetResult();
        Browser.CoreWebView2.NavigationCompleted += OnResultsNavCompleted;
        Browser.CoreWebView2.Navigate(resultUrl);
        await WaitWithTimeoutAsync(resultsLoaded.Task, TimeSpan.FromSeconds(20), "Загрузка страницы результатов");
        Browser.CoreWebView2.NavigationCompleted -= OnResultsNavCompleted;
        AppLog.Info($"[perf] Navigate to results page: {sw.ElapsedMilliseconds}ms");

        if (IsCaptchaUrl(Browser.CoreWebView2.Source))
            throw new GoogleCaptchaException();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        ((App)System.Windows.Application.Current).OpenSettings();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseOnce();

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }
}
