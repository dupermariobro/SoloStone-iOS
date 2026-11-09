using Foundation;
using UIKit;
using WebKit;
using SoloStone.Mobile;

UIApplication.Main(args, null, typeof(AppDelegate));

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;
}

[Register("SoloStoneSceneDelegate")]
public sealed class SoloStoneSceneDelegate : UIWindowSceneDelegate
{
    public override UIWindow? Window { get; set; }
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene) return;
        Window = new UIWindow(windowScene) { RootViewController = new SoloStoneViewController() };
        Window.MakeKeyAndVisible();
    }
}

public sealed class SoloStoneViewController : UIViewController, IWKScriptMessageHandler
{
    private WKWebView? web;
    private readonly SemaphoreSlim queue = new(1);
    private readonly MobileHost host = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoloStone"));

    public override bool PrefersStatusBarHidden() => true;
    public override UIRectEdge PreferredScreenEdgesDeferringSystemGestures => UIRectEdge.Bottom;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        var config = new WKWebViewConfiguration();
        config.UserContentController.AddScriptMessageHandler(this, "solo");
        web = new WKWebView(View!.Bounds, config) {
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            Opaque = false, BackgroundColor = UIColor.FromRGB(12, 24, 28),
            NavigationDelegate = new LocalNavigation()
        };
        web.ScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        web.ScrollView.Bounces = false;
        View.AddSubview(web);
        var root = Path.Combine(NSBundle.MainBundle.BundlePath, "Web");
        web.LoadFileUrl(NSUrl.FromFilename(Path.Combine(root, "index.html")), NSUrl.FromFilename(root));
    }

    public async void DidReceiveScriptMessage(WKUserContentController controller, WKScriptMessage message)
    {
        if (!message.FrameInfo.MainFrame || message.Body is not NSString body) return;
        string request = body.ToString();
        await queue.WaitAsync();
        string reply;
        try { reply = await Task.Run(() => host.Handle(request)); }
        finally { queue.Release(); }
        // A web process may be discarded while iOS suspends the app. The saved engine state remains recoverable.
        try { if (web != null) await web.EvaluateJavaScriptAsync($"window.receive({reply})"); }
        catch (NSErrorException) { }
    }

    private sealed class LocalNavigation : WKNavigationDelegate
    {
        public override void DecidePolicy(WKWebView webView, WKNavigationAction action, Action<WKNavigationActionPolicy> decisionHandler)
            => decisionHandler(action.Request.Url?.IsFileUrl == true
                ? WKNavigationActionPolicy.Allow : WKNavigationActionPolicy.Cancel);
    }
}
